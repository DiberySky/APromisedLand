using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.Entities;
using TreeGraph.Shared.Eav;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Api.Services;

/// <summary>自定义表读取服务：按父实体 + 属性加载整表</summary>
public class CustomTableReadService
{
    private readonly EavDbContext _db;
    private readonly ICustomTableCache _tableCache;
    private readonly CompositeValueService _composite;

    public CustomTableReadService(
        EavDbContext db,
        ICustomTableCache tableCache,
        CompositeValueService composite)
    {
        _db = db;
        _tableCache = tableCache;
        _composite = composite;
    }

    public async Task<CustomTableValue> LoadAsync(
        string parentEntityId, string parentEntityType,
        string attributeId, string tableDefinitionId,
        CancellationToken ct = default)
    {
        var table = _tableCache.GetTable(tableDefinitionId);
        var rows = await _db.CustomTableRows
            .Where(r => r.ParentEntityType == parentEntityType
                     && r.ParentEntityId == parentEntityId
                     && r.AttributeId == attributeId)
            .OrderBy(r => r.RowOrder)
            .AsNoTracking()
            .ToListAsync(ct);

        return new CustomTableValue
        {
            TableName = table.TableName,
            Rows = rows.Select(r => DeserializeRow(r, table)).ToList()
        };
    }

    private CustomTableRowValue DeserializeRow(
        CustomTableRow row, CustomTableDefinition table)
    {
        var result = new CustomTableRowValue
        {
            RowId = row.RowId,
            RowOrder = row.RowOrder
        };
        var root = row.RowData.RootElement;

        foreach (var col in table.Columns)
        {
            if (!root.TryGetProperty(col.ColumnName, out var elem)) continue;
            result.Fields[col.ColumnName] = DeserializeFieldValue(elem, col);
        }
        return result;
    }

    private object? DeserializeFieldValue(JsonElement elem, CustomTableColumn col)
    {
        if (elem.ValueKind == JsonValueKind.Null) return null;

        if (col.DataType == EavDataTypes.Composite)
        {
            using var doc = JsonDocument.Parse(elem.GetRawText());
            return _composite.Deserialize(doc, col.RefCompositeTypeId!);
        }

        return col.DataType switch
        {
            EavDataTypes.Int => elem.GetInt64(),
            EavDataTypes.Decimal => elem.GetDecimal(),
            EavDataTypes.Bool => elem.GetBoolean(),
            EavDataTypes.Datetime => elem.GetDateTimeOffset(),
            EavDataTypes.Date => DateOnly.Parse(elem.GetString()!),
            EavDataTypes.Time => TimeOnly.Parse(elem.GetString()!),
            _ => elem.GetString()
        };
    }
}
