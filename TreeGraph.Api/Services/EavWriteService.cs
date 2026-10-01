using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.Entities;
using TreeGraph.Shared.Eav;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Api.Services;

/// <summary>写入服务：验证 -> 插入/更新/删除 -> 审计（同一事务提交）</summary>
public class EavWriteService
{
    private readonly EavDbContext _db;
    private readonly IAttributeCache _attrCache;
    private readonly EavValidationService _validator;
    private readonly CompositeValueService _composite;
    private readonly IUnitCache _unitCache;
    private readonly UnitConverter _converter;
    private readonly IOptionSetCache _optionSetCache;

    public EavWriteService(
        EavDbContext db,
        IAttributeCache attrCache,
        EavValidationService validator,
        CompositeValueService composite,
        IUnitCache unitCache,
        UnitConverter converter,
        IOptionSetCache optionSetCache)
    {
        _db = db;
        _attrCache = attrCache;
        _validator = validator;
        _composite = composite;
        _unitCache = unitCache;
        _converter = converter;
        _optionSetCache = optionSetCache;
    }

    public async Task SaveAsync(
        long entityId, string entityType,
        Dictionary<string, object?> values,
        string changedBy, string? correlationId = null,
        CancellationToken ct = default)
    {
        var definitions = _attrCache.GetDefinitions(entityType)
            .ToDictionary(d => d.AttributeName);

        // ★ 修复 P0-2：必填缺失也校验（不再只校验 values 提供的属性）
        var validationErrors = new List<ValidationError>();
        foreach (var def in definitions.Values)
        {
            values.TryGetValue(def.AttributeName, out var rawValue);

            if (def.IsRequired && rawValue is null)
            {
                // 单选类型有默认选项时放行（写入端稍后自动填充）
                if (def.DataType == EavDataTypes.SingleChoice
                    && def.RefOptionSetId is { } sid
                    && _optionSetCache.GetSet(sid).Items.Any(i => i.IsDefault))
                    continue;

                validationErrors.Add(new(def.AttributeName, "必填字段未提供"));
                continue;
            }

            if (rawValue is null) continue;

            if (def.DataType == EavDataTypes.Composite && rawValue is DynamicCompositeValue cv)
            {
                var r = _composite.Validate(cv, def.RefCompositeTypeId!.Value);
                if (!r.IsValid) validationErrors.AddRange(r.Errors);
            }
            else
            {
                var r = _validator.ValidateBaseValue(def, rawValue);
                if (!r.IsValid) validationErrors.AddRange(r.Errors);
            }
        }
        if (validationErrors.Count > 0)
            throw new EavValidationException(validationErrors);

        var existing = await _db.AttributeValues
            .Where(v => v.EntityType == entityType && v.EntityId == entityId)
            .ToListAsync(ct);
        var existingMap = existing.ToDictionary(v => v.AttributeId);

        var audits = new List<AttributeAuditLog>();
        var now = DateTimeOffset.UtcNow;

        foreach (var (name, rawValue) in values)
        {
            if (!definitions.TryGetValue(name, out var def)) continue;

            var valueToWrite = rawValue;
            if (def.DataType == EavDataTypes.SingleChoice && valueToWrite is null
                && def.RefOptionSetId is { } osid)
            {
                valueToWrite = _optionSetCache.GetSet(osid)
                    .Items.FirstOrDefault(i => i.IsDefault)?.Value;
            }

            existingMap.TryGetValue(def.AttributeId, out var existingValue);

            // ★ 修复 P0-4：显式 null 走删除路径，不留全空行
            if (valueToWrite is null)
            {
                if (existingValue is not null)
                {
                    _db.AttributeValues.Remove(existingValue);
                    audits.Add(CreateAudit(entityId, entityType, def,
                        SerializeForAudit(existingValue, def), null, "Delete",
                        changedBy, correlationId, now));
                }
                continue;
            }

            if (existingValue is null)
            {
                existingValue = new AttributeValue
                {
                    EntityId = entityId,
                    EntityType = entityType,
                    AttributeId = def.AttributeId,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                SetTypedValue(existingValue, def, valueToWrite);
                _db.AttributeValues.Add(existingValue);
                audits.Add(CreateAudit(entityId, entityType, def,
                    null, SerializeForAudit(existingValue, def),
                    "Insert", changedBy, correlationId, now));
            }
            else
            {
                var oldSerialized = SerializeForAudit(existingValue, def);
                ClearValueColumns(existingValue);
                SetTypedValue(existingValue, def, valueToWrite);
                existingValue.UpdatedAt = now;

                var newSerialized = SerializeForAudit(existingValue, def);
                if (oldSerialized != newSerialized)
                {
                    audits.Add(CreateAudit(entityId, entityType, def,
                        oldSerialized, newSerialized, "Update",
                        changedBy, correlationId, now));
                }
            }
        }

        // 处理未提供的属性（删除）
        foreach (var def in definitions.Values)
        {
            if (!values.ContainsKey(def.AttributeName)
                && existingMap.TryGetValue(def.AttributeId, out var oldVal))
            {
                _db.AttributeValues.Remove(oldVal);
                audits.Add(CreateAudit(entityId, entityType, def,
                    SerializeForAudit(oldVal, def), null, "Delete",
                    changedBy, correlationId, now));
            }
        }

        _db.AttributeAuditLogs.AddRange(audits);
        await _db.SaveChangesAsync(ct);
    }

    private void SetTypedValue(
        AttributeValue target, AttributeDefinition def, object? value)
    {
        if (value is null) return;

        switch (def.DataType)
        {
            case EavDataTypes.String:
                target.ValueString = value.ToString();
                break;
            case EavDataTypes.SingleChoice:
                target.ValueString = value.ToString();
                break;
            case EavDataTypes.Int:
            case EavDataTypes.Decimal:
            {
                var (numericValue, inputUnitId) = UnwrapNumeric(value);
                if (numericValue is null)
                    throw new InvalidOperationException("数值格式错误");

                decimal normalized = numericValue.Value;
                if (def.UnitId is { } baseUnitId && inputUnitId is { } iu)
                {
                    normalized = _converter.ToBase(numericValue.Value, iu, baseUnitId);
                    target.UnitId = iu;
                }
                else
                {
                    target.UnitId = null;
                }

                if (def.DataType == EavDataTypes.Int)
                    target.ValueInt = (long)normalized;
                else
                    target.ValueDecimal = normalized;
                break;
            }
            case EavDataTypes.Bool:
                target.ValueBool = Convert.ToBoolean(value);
                break;
            case EavDataTypes.Datetime:
                target.ValueDatetime = value is DateTimeOffset dto
                    ? dto
                    : DateTimeOffset.Parse(value.ToString()!,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                break;
            case EavDataTypes.Date:
                target.ValueDateOnly = value is DateOnly d
                    ? d
                    : DateOnly.Parse(value.ToString()!, CultureInfo.InvariantCulture);
                break;
            case EavDataTypes.Time:
                target.ValueTime = value is TimeOnly t
                    ? t
                    : TimeOnly.Parse(value.ToString()!, CultureInfo.InvariantCulture);
                break;
            case EavDataTypes.File:
                target.ValueFileMeta = value is JsonDocument fd
                    ? fd
                    : JsonDocument.Parse(JsonSerializer.Serialize(value));
                break;
            case EavDataTypes.Json:
                target.ValueJsonb = value is JsonDocument jd
                    ? jd
                    : JsonDocument.Parse(JsonSerializer.Serialize(value));
                break;
            case EavDataTypes.Composite:
                if (value is not DynamicCompositeValue cv)
                    throw new InvalidOperationException("composite 值类型错误");
                target.ValueJsonb = _composite.Serialize(cv, def.RefCompositeTypeId!.Value);
                break;
        }
    }

    private static void ClearValueColumns(AttributeValue v)
    {
        v.ValueString = null;
        v.ValueInt = null;
        v.ValueDecimal = null;
        v.ValueBool = null;
        v.ValueDatetime = null;
        v.ValueDateOnly = null;
        v.ValueTime = null;
        v.ValueFileMeta = null;
        v.ValueJsonb = null;
        v.UnitId = null;
    }

    private static (decimal? Value, Guid? UnitId) UnwrapNumeric(object value)
    {
        return value switch
        {
            NumericValue nv => (nv.Value, nv.UnitId),
            decimal d => (d, null),
            int i => (i, null),
            long l => (l, null),
            double db => ((decimal)db, null),
            string s when decimal.TryParse(s, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var parsed) => (parsed, null),
            _ => (null, null)
        };
    }

    private string? SerializeForAudit(AttributeValue v, AttributeDefinition def)
    {
        return def.DataType switch
        {
            EavDataTypes.String => v.ValueString,
            EavDataTypes.SingleChoice => FormatSingleChoice(v.ValueString, def),
            EavDataTypes.Int when v.ValueInt is { } i => FormatNumeric(i, v.UnitId),
            EavDataTypes.Decimal when v.ValueDecimal is { } d => FormatNumeric(d, v.UnitId),
            EavDataTypes.Int => null,
            EavDataTypes.Decimal => null,
            EavDataTypes.Bool => v.ValueBool?.ToString(),
            EavDataTypes.Datetime => v.ValueDatetime?.ToString("o"),
            EavDataTypes.Date => v.ValueDateOnly?.ToString("yyyy-MM-dd"),
            EavDataTypes.Time => v.ValueTime?.ToString("HH:mm:ss"),
            EavDataTypes.File => v.ValueFileMeta?.RootElement.GetRawText(),
            EavDataTypes.Json => v.ValueJsonb?.RootElement.GetRawText(),
            EavDataTypes.Composite => v.ValueJsonb?.RootElement.GetRawText(),
            _ => null
        };
    }

    private string FormatSingleChoice(string? value, AttributeDefinition def)
    {
        if (value is null) return "";
        if (def.RefOptionSetId is not { } sid) return value;

        try
        {
            var item = _optionSetCache.GetSet(sid).Items
                .FirstOrDefault(i => i.Value == value);
            return item is null ? value : $"{value} ({item.Label})";
        }
        catch (KeyNotFoundException)
        {
            return value;
        }
    }

    private string FormatNumeric(decimal value, Guid? originalUnitId)
    {
        if (originalUnitId is not { } uid)
            return value.ToString(CultureInfo.InvariantCulture);

        try
        {
            var unit = _unitCache.Get(uid);
            return $"{value} {unit.Symbol}";
        }
        catch (InvalidOperationException)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }

    private static AttributeAuditLog CreateAudit(
        long entityId, string entityType, AttributeDefinition def,
        string? oldValue, string? newValue, string changeType,
        string changedBy, string? correlationId, DateTimeOffset now)
    {
        return new AttributeAuditLog
        {
            EntityId = entityId,
            EntityType = entityType,
            AttributeId = def.AttributeId,
            AttributeName = def.AttributeName,
            OldValue = oldValue,
            NewValue = newValue,
            ChangeType = changeType,
            ChangedBy = changedBy,
            ChangedAt = now,
            CorrelationId = correlationId
        };
    }
}
