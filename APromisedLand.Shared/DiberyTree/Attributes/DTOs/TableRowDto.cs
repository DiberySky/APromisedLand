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
