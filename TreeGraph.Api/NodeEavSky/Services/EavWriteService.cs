using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Shared.NodeEavSky;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Api.NodeEavSky.Services;

/// <summary>写入服务：验证 -> 插入/更新/删除 -> 审计（同一事务提交）</summary>
public class EavWriteService
{
    private readonly TreeGraphDbContext _db;
    private readonly IAttributeCache _attrCache;
    private readonly EavValidationService _validator;
    private readonly CompositeValueService _composite;
    private readonly IUnitCache _unitCache;
    private readonly UnitConverter _converter;
    private readonly IOptionSetCache _optionSetCache;
    private readonly EntityOwnerGuardRegistry _ownerGuards;

    public EavWriteService(
        TreeGraphDbContext db,
        IAttributeCache attrCache,
        EavValidationService validator,
        CompositeValueService composite,
        IUnitCache unitCache,
        UnitConverter converter,
        IOptionSetCache optionSetCache,
        EntityOwnerGuardRegistry ownerGuards)
    {
        _db = db;
        _attrCache = attrCache;
        _validator = validator;
        _composite = composite;
        _unitCache = unitCache;
        _converter = converter;
        _optionSetCache = optionSetCache;
        _ownerGuards = ownerGuards;
    }

    /// <summary>
    /// 保存实体属性（全量替换语义）。
    ///
    /// 语义：
    ///   - values 中出现的键：验证 + 写入
    ///   - values 中值为 null 的键：删除该属性
    ///   - values 中未出现的键（但 catalog 中定义的属性）：**删除**
    ///
    /// ★ 乐观锁：expectedUpdatedAt 非 null 时，比对 DB 中该实体的
    ///   max(AttributeValue.UpdatedAt)。**用 UtcDateTime 比较**，
    ///   避免客户端回传时携带不同 offset（如本地 +08:00）导致误判。
    ///
    /// ★ 未知属性：values 中出现 catalog 未定义的属性名时返回 400，
    ///   而非静默丢弃（防客户端拼写错误导致数据丢失）。
    /// </summary>
    public Task SaveAsync(
        string entityId, string entityType,
        Dictionary<string, object?> values,
        string changedBy, string? correlationId = null,
        CancellationToken ct = default,
        DateTimeOffset? expectedUpdatedAt = null)
        => SaveCoreAsync(entityId, entityType, values,
            changedBy, correlationId, ct, expectedUpdatedAt,
            fullReplace: true);

    /// <summary>
    /// 部分更新实体属性（PATCH 语义）。
    ///
    /// 语义：
    ///   - values 中出现的键：验证 + 写入
    ///   - values 中值为 null 的键：删除该属性
    ///   - values 中未出现的键：**保持原值不动**
    ///
    /// 与 SaveAsync 的差异仅在"未提供的属性"处理上。
    /// 乐观锁、未知属性检查、验证流程与 SaveAsync 完全一致。
    /// </summary>
    public Task PatchAsync(
        string entityId, string entityType,
        Dictionary<string, object?> values,
        string changedBy, string? correlationId = null,
        CancellationToken ct = default,
        DateTimeOffset? expectedUpdatedAt = null)
        => SaveCoreAsync(entityId, entityType, values,
            changedBy, correlationId, ct, expectedUpdatedAt,
            fullReplace: false);

    /// <summary>
    /// Save / Patch 的公共核心逻辑。
    ///
    /// - fullReplace = true  → 未提供的键（catalog 中定义）将被删除（PUT 语义）
    /// - fullReplace = false → 未提供的键保持不动（PATCH 语义）
    /// </summary>
    private async Task SaveCoreAsync(
        string entityId, string entityType,
        Dictionary<string, object?> values,
        string changedBy, string? correlationId,
        CancellationToken ct,
        DateTimeOffset? expectedUpdatedAt,
        bool fullReplace)
    {
        // ---- -1. Entity Id 格式校验（GUID 字符串）----
        if (!Guid.TryParse(entityId, out _))
            throw new EavValidationException(new List<ValidationError>
                { new("id", "Entity Id 必须是 GUID 格式") });

        // ---- -1b. 归属校验：受外部主表约束的类型，entityId 必须指向已存在的宿主 ----
        //   防止属性值脱离宿主实体凭空生成（孤儿数据）。
        await _ownerGuards.EnsureOwnerExistsAsync(entityType, entityId, ct);

        var definitions = _attrCache.GetDefinitions(entityType)
            .ToDictionary(d => d.AttributeName);

        // ---- 0. 未知属性检查 ----
        var unknownKeys = values.Keys
            .Where(k => !definitions.ContainsKey(k))
            .ToList();
        if (unknownKeys.Count > 0)
        {
            throw new EavValidationException(unknownKeys
                .Select(k => new ValidationError(k, "未知属性"))
                .ToList());
        }

        // ---- 1. 乐观锁检测（用 UtcDateTime 语义比较）----
        if (expectedUpdatedAt is { } expected)
        {
            var currentMax = await _db.AttributeValues
                .Where(v => v.EntityType == entityType && v.EntityId == entityId)
                .MaxAsync(v => (DateTimeOffset?)v.UpdatedAt, ct);

            // ★ 修复：DateTimeOffset.!= 会比较 instant + offset，
            //   同一瞬间但 offset 不同会误判冲突。改用 UtcDateTime。
            if (currentMax is null
                || currentMax.Value.UtcDateTime != expected.UtcDateTime)
            {
                throw new EavConcurrencyException(
                    expected, currentMax ?? DateTimeOffset.MinValue);
            }
        }

        // ---- 2. 验证提供的值 ----
        var validationErrors = new List<ValidationError>();
        foreach (var def in definitions.Values)
        {
            values.TryGetValue(def.AttributeName, out var rawValue);

            // PATCH 模式下：未提供的键不参与必填检查
            var provided = values.ContainsKey(def.AttributeName);
            if (!fullReplace && !provided) continue;

            if (def.IsRequired && rawValue is null)
            {
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
                var r = _composite.Validate(cv, def.RefCompositeTypeId!);
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

        // ---- 3. 处理显式提供的值 ----
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

            // 显式 null 走删除路径
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
                    // ValueId 为空字符串 → 触发 DB DEFAULT gen_random_uuid()（HasSentinel 语义）
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

        // ---- 4. PUT 语义：未提供的属性 → 删除 ----
        if (fullReplace)
        {
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

                // ★ int 无单位 → ValueInt；int 有单位 → ValueDecimal；decimal → ValueDecimal
                if (def.DataType == EavDataTypes.Int && def.UnitId is null)
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
                target.ValueJsonb = _composite.Serialize(cv, def.RefCompositeTypeId!);
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

            // ★ int 无单位：ValueInt
            EavDataTypes.Int when v.ValueInt is { } i && def.UnitId is null
                => i.ToString(CultureInfo.InvariantCulture),
            // ★ int 有单位：ValueDecimal（归一化后的值 + 原始单位符号）
            EavDataTypes.Int when v.ValueDecimal is { } di
                => FormatNumeric(di, v.UnitId),
            EavDataTypes.Int => null,

            EavDataTypes.Decimal when v.ValueDecimal is { } d => FormatNumeric(d, v.UnitId),
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
        string entityId, string entityType, AttributeDefinition def,
        string? oldValue, string? newValue, string changeType,
        string changedBy, string? correlationId, DateTimeOffset now)
    {
        return new AttributeAuditLog
        {
            // AuditId 为空字符串 → 触发 DB DEFAULT gen_random_uuid()（HasSentinel 语义）
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

    /// <summary>删除实体（物理删除所有属性值 + 自定义表行，写审计）。</summary>
    public async Task<bool> DeleteEntityAsync(
        string entityId, string entityType,
        string changedBy, string? correlationId = null,
        CancellationToken ct = default)
    {
        var hasValues = await _db.AttributeValues
            .AnyAsync(v => v.EntityType == entityType && v.EntityId == entityId, ct);

        var hasRows = await _db.CustomTableRows
            .AnyAsync(r => r.ParentEntityType == entityType
                        && r.ParentEntityId == entityId, ct);

        if (!hasValues && !hasRows)
            return false;

        var strategy = _db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var now = DateTimeOffset.UtcNow;

            var values = await _db.AttributeValues
                .Where(v => v.EntityType == entityType && v.EntityId == entityId)
                .ToListAsync(ct);

            if (values.Count > 0)
            {
                var definitions = _attrCache.GetDefinitions(entityType)
                    .ToDictionary(d => d.AttributeId);

                var audits = new List<AttributeAuditLog>(values.Count);
                foreach (var v in values)
                {
                    if (!definitions.TryGetValue(v.AttributeId, out var def)) continue;

                    audits.Add(new AttributeAuditLog
                    {
                        EntityId = entityId,
                        EntityType = entityType,
                        AttributeId = def.AttributeId,
                        AttributeName = def.AttributeName,
                        OldValue = SerializeForAudit(v, def),
                        NewValue = null,
                        ChangeType = "Delete",
                        ChangedBy = changedBy,
                        ChangedAt = now,
                        CorrelationId = correlationId
                    });
                }
                _db.AttributeAuditLogs.AddRange(audits);
            }

            _db.AttributeValues.RemoveRange(values);

            var rows = await _db.CustomTableRows
                .Where(r => r.ParentEntityType == entityType
                         && r.ParentEntityId == entityId)
                .ToListAsync(ct);
            _db.CustomTableRows.RemoveRange(rows);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });

        return true;
    }

    /// <summary>
    /// 批量删除多个实体（硬删除：属性值 + 自定义表行）。
    ///
    /// 语义：
    ///   - 请求的 ID 去重后处理
    ///   - 不存在的 ID 计入 NotFound（不报错）
    ///   - 单个事务内完成所有删除 + 审计
    ///   - 与 DeleteEntityAsync 共享"删除 = 删两张表 + 写审计"的策略
    /// </summary>
    public async Task<BatchDeleteResult> DeleteEntitiesAsync(
        IReadOnlyList<string> entityIds,
        string entityType,
        string changedBy,
        string? correlationId = null,
        CancellationToken ct = default)
    {
        if (entityIds.Count == 0)
            return new BatchDeleteResult(
                Array.Empty<string>(), Array.Empty<string>(), 0);

        var requested = entityIds.Distinct().ToList();

        // 存在性：任一表命中即认为存在
        var hasValues = await _db.AttributeValues
            .Where(v => v.EntityType == entityType && requested.Contains(v.EntityId))
            .Select(v => v.EntityId)
            .Distinct()
            .ToListAsync(ct);

        var hasRows = await _db.CustomTableRows
            .Where(r => r.ParentEntityType == entityType
                     && requested.Contains(r.ParentEntityId))
            .Select(r => r.ParentEntityId)
            .Distinct()
            .ToListAsync(ct);

        var existingSet = hasValues.Union(hasRows).ToHashSet();
        var toDelete = requested.Where(id => existingSet.Contains(id)).ToList();
        var notFound = requested.Where(id => !existingSet.Contains(id)).ToList();

        if (toDelete.Count == 0)
            return new BatchDeleteResult(
                Array.Empty<string>(), notFound, 0);

        var strategy = _db.Database.CreateExecutionStrategy();
        int totalAttributesDeleted = 0;

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            var now = DateTimeOffset.UtcNow;

            // 1. 加载待删属性值（用于审计 + 计数）
            var values = await _db.AttributeValues
                .Where(v => v.EntityType == entityType
                         && toDelete.Contains(v.EntityId))
                .ToListAsync(ct);

            // 2. 写审计
            if (values.Count > 0)
            {
                var definitions = _attrCache.GetDefinitions(entityType)
                    .ToDictionary(d => d.AttributeId);

                var audits = new List<AttributeAuditLog>(values.Count);
                foreach (var v in values)
                {
                    if (!definitions.TryGetValue(v.AttributeId, out var def)) continue;
                    audits.Add(new AttributeAuditLog
                    {
                        EntityId = v.EntityId,
                        EntityType = entityType,
                        AttributeId = def.AttributeId,
                        AttributeName = def.AttributeName,
                        OldValue = SerializeForAudit(v, def),
                        NewValue = null,
                        ChangeType = "Delete",
                        ChangedBy = changedBy,
                        ChangedAt = now,
                        CorrelationId = correlationId
                    });
                }
                _db.AttributeAuditLogs.AddRange(audits);
            }

            totalAttributesDeleted = values.Count;

            // 3. 删除属性值
            _db.AttributeValues.RemoveRange(values);

            // 4. 删除自定义表行
            var rows = await _db.CustomTableRows
                .Where(r => r.ParentEntityType == entityType
                         && toDelete.Contains(r.ParentEntityId))
                .ToListAsync(ct);
            _db.CustomTableRows.RemoveRange(rows);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });

        return new BatchDeleteResult(toDelete, notFound, totalAttributesDeleted);
    }
}

/// <summary>
/// 批量删除结果。
/// </summary>
public record BatchDeleteResult(
    IReadOnlyList<string> Deleted,
    IReadOnlyList<string> NotFound,
    int TotalAttributesDeleted);

/// <summary>
/// 并发冲突异常。EavWriteService.SaveAsync 在乐观锁检测失败时抛出。
/// EavController 捕获后返回 409 Conflict + 当前 UpdatedAt。
/// </summary>
public class EavConcurrencyException : Exception
{
    public DateTimeOffset ExpectedUpdatedAt { get; }
    public DateTimeOffset CurrentUpdatedAt { get; }

    public EavConcurrencyException(
        DateTimeOffset expectedUpdatedAt, DateTimeOffset currentUpdatedAt)
        : base("并发冲突：实体已被其他用户修改")
    {
        ExpectedUpdatedAt = expectedUpdatedAt;
        CurrentUpdatedAt = currentUpdatedAt;
    }
}
