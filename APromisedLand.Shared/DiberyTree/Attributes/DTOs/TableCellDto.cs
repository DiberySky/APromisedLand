using APromisedLand.Shared.DiberyTree.Attributes.Models;

namespace APromisedLand.Shared.DiberyTree.Attributes.DTOs;


/// <summary>行内单个单元格（列值）。</summary>
public class TableCellDto
{
    public string ColumnId { get; set; } = null!;
    // public string? ColumnName { get; set; }
    public AttributeDefinition? ColumnDef { get; set; }
    public object? Value { get; set; }
}