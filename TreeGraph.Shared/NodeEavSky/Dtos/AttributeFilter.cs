namespace TreeGraph.Shared.NodeEavSky.Dtos;

/// <summary>动态属性过滤条件</summary>
public class AttributeFilter
{
    public string AttributeName { get; set; } = "";

    /// <summary>组合类型字段路径，如 "address.city"</summary>
    public string? FieldPath { get; set; }

    /// <summary>eq / neq / gt / gte / lt / lte / between / in / like / startswith / endswith / near / bbox</summary>
    public string Operator { get; set; } = "eq";

    public object? Value { get; set; }

    /// <summary>between 的第二个值</summary>
    public object? Value2 { get; set; }
}
