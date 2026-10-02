namespace TreeGraph.Shared.Eav.Dtos;

public class CustomTableValue
{
    public string TableName { get; set; } = "";
    public List<CustomTableRowValue> Rows { get; set; } = new();
}

public class CustomTableRowValue
{
    /// <summary>已有行有 GUID 字符串 Id；新行为 null。</summary>
    public string? RowId { get; set; }

    public int RowOrder { get; set; }
    public Dictionary<string, object?> Fields { get; set; } = new();
}
