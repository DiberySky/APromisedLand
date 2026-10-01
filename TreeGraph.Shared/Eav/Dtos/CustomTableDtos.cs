namespace TreeGraph.Shared.Eav.Dtos;

/// <summary>自定义表运行时值（整表容器）</summary>
public class CustomTableValue
{
    public string TableName { get; set; } = "";
    public List<CustomTableRowValue> Rows { get; set; } = new();
}

/// <summary>自定义表行运行时值</summary>
public class CustomTableRowValue
{
    /// <summary>已有行有 Id，新行为 null</summary>
    public long? RowId { get; set; }
    public int RowOrder { get; set; }
    public Dictionary<string, object?> Fields { get; set; } = new();
}
