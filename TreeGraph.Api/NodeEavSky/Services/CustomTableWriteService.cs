using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Shared.NodeEavSky;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Api.NodeEavSky.Services;

/// <summary>自定义表写入服务：整表替换（默认）+ 行级增量更新</summary>
public class CustomTableWriteService
{
    private readonly TreeGraphDbContext _db;
    private readonly ICustomTableCache _tableCache;
    private readonly CustomTableValidationService _validator;
    private readonly CompositeValueService _composite;
    private readonly EntityOwnerGuardRegistry _ownerGuards;

    public CustomTableWriteService(
        TreeGraphDbContext db,
        ICustomTableCache tableCache,
        CustomTableValidationService validator,
        CompositeValueService composite,
        EntityOwnerGuardRegistry ownerGuards)
    {
        _db = db;
        _tableCache = tableCache;
        _validator = validator;
        _composite = composite;
        _ownerGuards = ownerGuards;
    }

    /// <summary>整表替换：删除旧行，写入新行</summary>
    public async Task ReplaceAsync(
        string parentEntityId, string parentEntityType,
        string attributeId, string tableDefinitionId,
        CustomTableValue value, CancellationToken ct = default)
    {
        // 子表行必须附属于已存在的宿主实体
        await _ownerGuards.EnsureOwnerExistsAsync(parentEntityType, parentEntityId, ct);

        var table = _tableCache.GetTable(tableDefinitionId);

        foreach (var row in value.Rows)
            NormalizeRow(row, table);

        var result = _validator.Validate(value, tableDefinitionId);
        if (!result.IsValid)
            throw new EavValidationException(result.Errors);

        var oldRows = await _db.CustomTableRows
            .Where(r => r.ParentEntityType == parentEntityType
                     && r.ParentEntityId == parentEntityId
                     && r.AttributeId == attributeId)
            .ToListAsync(ct);
        _db.CustomTableRows.RemoveRange(oldRows);

        var now = DateTimeOffset.UtcNow;
        for (int i = 0; i < value.Rows.Count; i++)
        {
            var row = value.Rows[i];
            _db.CustomTableRows.Add(new CustomTableRow
            {
                TableDefinitionId = tableDefinitionId,
                AttributeId = attributeId,
                ParentEntityId = parentEntityId,
                ParentEntityType = parentEntityType,
                RowData = SerializeRow(row, table),
                RowOrder = i,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// 新增或更新单行（RowId 为 null 时新增；更新时校验归属）。
    ///
    /// ★ 修复：写入前必须走验证管道（必填/类型/行内唯一），
    ///   并额外做一次跨行唯一性检查（DB 已存在的其它行）。
    /// </summary>
    public async Task UpsertRowAsync(
        string parentEntityId, string parentEntityType,
        string attributeId, string tableDefinitionId,
        CustomTableRowValue rowValue, CancellationToken ct = default)
    {
        // 子表行必须附属于已存在的宿主实体
        await _ownerGuards.EnsureOwnerExistsAsync(parentEntityType, parentEntityId, ct);

        var table = _tableCache.GetTable(tableDefinitionId);
        NormalizeRow(rowValue, table);

        // ① 复用整表验证器：检查必填 / 类型 / 行内唯一
        var validationValue = new CustomTableValue
        {
            TableName = table.TableName,
            Rows = new List<CustomTableRowValue> { rowValue }
        };
        var validationResult = _validator.Validate(validationValue, tableDefinitionId);
        if (!validationResult.IsValid)
            throw new EavValidationException(validationResult.Errors);

        // ② 跨行唯一性：与 DB 中同一父实体、同一属性下的其它行比对
        await CheckCrossRowUniquenessAsync(
            parentEntityId, parentEntityType, attributeId,
            table, rowValue, ct);

        if (rowValue.RowId is null)
        {
            _db.CustomTableRows.Add(new CustomTableRow
            {
                TableDefinitionId = tableDefinitionId,
                AttributeId = attributeId,
                ParentEntityId = parentEntityId,
                ParentEntityType = parentEntityType,
                RowData = SerializeRow(rowValue, table),
                RowOrder = rowValue.RowOrder,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            // ★ 修复 P0-3：校验归属，防止越权修改其它实体的行
            var existing = await _db.CustomTableRows
                .FirstOrDefaultAsync(r =>
                    r.RowId == rowValue.RowId
                 && r.ParentEntityId == parentEntityId
                 && r.ParentEntityType == parentEntityType
                 && r.AttributeId == attributeId
                 && r.TableDefinitionId == tableDefinitionId, ct)
                ?? throw new KeyNotFoundException(
                    $"行不存在或不属于当前实体: {rowValue.RowId}");

            existing.RowData = SerializeRow(rowValue, table);
            existing.RowOrder = rowValue.RowOrder;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>删除单行（★ 修复 P0-3：带归属校验）</summary>
    public async Task DeleteRowAsync(
        string rowId, string parentEntityId, string parentEntityType,
        string attributeId, CancellationToken ct = default)
    {
        var row = await _db.CustomTableRows
            .FirstOrDefaultAsync(r =>
                r.RowId == rowId
             && r.ParentEntityId == parentEntityId
             && r.ParentEntityType == parentEntityType
             && r.AttributeId == attributeId, ct);
        if (row is null) return;
        _db.CustomTableRows.Remove(row);
        await _db.SaveChangesAsync(ct);
    }

    // ---------- 跨行唯一性 ----------

    /// <summary>
    /// 对表定义中所有 IsUnique = true 的列，检查当前行值是否与
    /// 同一父实体、同一属性下的其它行冲突。
    ///
    /// 实现要点：先把当前行按落库格式序列化为 JSON，再与 DB 中
    /// 其它行的 RowData 逐字段做 JSON 字面量比较，保证与存储格式一致。
    /// </summary>
    private async Task CheckCrossRowUniquenessAsync(
        string parentEntityId, string parentEntityType, string attributeId,
        CustomTableDefinition table, CustomTableRowValue rowValue,
        CancellationToken ct)
    {
        var uniqueCols = table.Columns.Where(c => c.IsUnique).ToList();
        if (uniqueCols.Count == 0) return;

        // 查询同一父实体、同一属性下、除当前行以外的其它行数据
        IQueryable<CustomTableRow> query = _db.CustomTableRows
            .Where(r => r.ParentEntityType == parentEntityType
                     && r.ParentEntityId == parentEntityId
                     && r.AttributeId == attributeId);

        if (rowValue.RowId is string excludeId)
            query = query.Where(r => r.RowId != excludeId);

        var otherRows = await query
            .Select(r => r.RowData)
            .AsNoTracking()
            .ToListAsync(ct);

        if (otherRows.Count == 0) return;

        // 用与落库一致的格式序列化当前行，保证字面量比较可靠
        using var currentDoc = SerializeRow(rowValue, table);
        var currentRoot = currentDoc.RootElement;

        var errors = new List<ValidationError>();
        foreach (var col in uniqueCols)
        {
            if (!currentRoot.TryGetProperty(col.ColumnName, out var curElem))
                continue;
            if (curElem.ValueKind == JsonValueKind.Null) continue;

            var key = curElem.GetRawText();  // JSON 字面量表示（含引号）

            foreach (var otherDoc in otherRows)
            {
                if (otherDoc.RootElement.TryGetProperty(col.ColumnName, out var otherElem)
                    && otherElem.ValueKind != JsonValueKind.Null
                    && otherElem.GetRawText() == key)
                {
                    errors.Add(new ValidationError(
                        col.ColumnName,
                        $"值 {key} 在列中已存在（跨行唯一）"));
                    break;
                }
            }
        }

        if (errors.Count > 0)
            throw new EavValidationException(errors);
    }

    // ---------- 请求值规范化 ----------

    private void NormalizeRow(CustomTableRowValue row, CustomTableDefinition table)
    {
        foreach (var col in table.Columns)
        {
            if (!row.Fields.TryGetValue(col.ColumnName, out var raw) || raw is null)
                continue;
            row.Fields[col.ColumnName] = NormalizeFieldValue(raw, col);
        }
    }

    private object? NormalizeFieldValue(object raw, CustomTableColumn col)
    {
        if (raw is not JsonElement elem) return raw;
        if (elem.ValueKind == JsonValueKind.Null) return null;

        if (col.DataType == EavDataTypes.Composite)
        {
            using var doc = JsonDocument.Parse(elem.GetRawText());
            return _composite.Deserialize(doc, col.RefCompositeTypeId!);
        }

        return col.DataType switch
        {
            EavDataTypes.Int => elem.ValueKind == JsonValueKind.Number
                ? (object)elem.GetInt64()
                : long.TryParse(elem.GetString(), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var l) ? l : (object)elem.GetRawText(),
            EavDataTypes.Decimal => elem.ValueKind == JsonValueKind.Number
                ? (object)elem.GetDecimal()
                : decimal.TryParse(elem.GetString(), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var d) ? d : (object)elem.GetRawText(),
            EavDataTypes.Bool => elem.GetBoolean(),
            EavDataTypes.Datetime => elem.GetDateTimeOffset(),
            EavDataTypes.Date => DateOnly.Parse(elem.GetString()!),
            EavDataTypes.Time => TimeOnly.Parse(elem.GetString()!),
            EavDataTypes.Json or EavDataTypes.File => JsonDocument.Parse(elem.GetRawText()),
            _ => elem.ValueKind == JsonValueKind.String ? elem.GetString() : elem.GetRawText()
        };
    }

    // ---------- 行序列化 ----------

    private JsonDocument SerializeRow(CustomTableRowValue row, CustomTableDefinition table)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var col in table.Columns)
        {
            row.Fields.TryGetValue(col.ColumnName, out var raw);
            dict[col.ColumnName] = SerializeFieldValue(raw, col);
        }
        return JsonDocument.Parse(JsonSerializer.Serialize(dict));
    }

    private object? SerializeFieldValue(object? value, CustomTableColumn col)
    {
        if (value is null) return null;

        if (col.DataType == EavDataTypes.Composite && value is DynamicCompositeValue cv)
        {
            var doc = _composite.Serialize(cv, col.RefCompositeTypeId!);
            return doc.RootElement.Clone();
        }

        return col.DataType switch
        {
            EavDataTypes.Datetime when value is DateTimeOffset dto => dto,
            EavDataTypes.Date when value is DateOnly d => d.ToString("yyyy-MM-dd"),
            EavDataTypes.Time when value is TimeOnly t => t.ToString("HH:mm:ss"),
            _ => value
        };
    }
}
