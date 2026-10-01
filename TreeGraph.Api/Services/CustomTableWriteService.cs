using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.Entities;
using TreeGraph.Shared.Eav;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Api.Services;

/// <summary>自定义表写入服务：整表替换（默认）+ 行级增量更新</summary>
public class CustomTableWriteService
{
    private readonly EavDbContext _db;
    private readonly ICustomTableCache _tableCache;
    private readonly CustomTableValidationService _validator;
    private readonly CompositeValueService _composite;

    public CustomTableWriteService(
        EavDbContext db,
        ICustomTableCache tableCache,
        CustomTableValidationService validator,
        CompositeValueService composite)
    {
        _db = db;
        _tableCache = tableCache;
        _validator = validator;
        _composite = composite;
    }

    /// <summary>整表替换：删除旧行，写入新行</summary>
    public async Task ReplaceAsync(
        long parentEntityId, string parentEntityType,
        long attributeId, long tableDefinitionId,
        CustomTableValue value, CancellationToken ct = default)
    {
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

    /// <summary>新增或更新单行（RowId 为 null 时新增；更新时校验归属）</summary>
    public async Task UpsertRowAsync(
        long parentEntityId, string parentEntityType,
        long attributeId, long tableDefinitionId,
        CustomTableRowValue rowValue, CancellationToken ct = default)
    {
        var table = _tableCache.GetTable(tableDefinitionId);
        NormalizeRow(rowValue, table);

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
        long rowId, long parentEntityId, string parentEntityType,
        long attributeId, CancellationToken ct = default)
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
            return _composite.Deserialize(doc, col.RefCompositeTypeId!.Value);
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
            var doc = _composite.Serialize(cv, col.RefCompositeTypeId!.Value);
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
