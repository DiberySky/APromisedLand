using System.Text.Json;

namespace APromisedLand.Shared.DiberyTree.Attributes.DTOs;

/// <summary>动态表行实例返回 DTO（含各列值）。</summary>
public class TableRowDto
{
    public string RowId { get; set; } = null!;
    public int RowNo { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public List<TableCellDto> Values { get; set; } = new();
}

/// <summary>行内单个单元格（列值）。</summary>
public class TableCellDto
{
    public string ColumnId { get; set; } = null!;
    public string? ColumnName { get; set; }
    public object? Value { get; set; }
}

/// <summary>新增行 DTO：列定义 Id -> 列值（JSON，由 ValueValidator 按列类型解析）。</summary>
public class AddTableRowDto
{
    public Dictionary<string, JsonElement> Values { get; set; } = new();
}

/// <summary>更新行 DTO：列定义 Id -> 列值（JSON）。</summary>
public class UpdateTableRowDto
{
    public Dictionary<string, JsonElement> Values { get; set; } = new();
}
