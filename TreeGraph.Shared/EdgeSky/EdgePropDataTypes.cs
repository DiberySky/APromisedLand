namespace TreeGraph.Shared.EdgeSky;

/// <summary>
/// EdgeType 属性数据类型（镜像 NebulaGraph 边属性支持的数据类型子集）。
/// 存储值统一用此处的大写常量（与 nGQL DDL 关键字一致）。
/// </summary>
public static class EdgePropDataTypes
{
    public const string String = "STRING";
    public const string Int = "INT";
    public const string Double = "DOUBLE";
    public const string Bool = "BOOL";
    public const string Timestamp = "TIMESTAMP";
    public const string Date = "DATE";

    public static readonly string[] All =
        [String, Int, Double, Bool, Timestamp, Date];

    /// <summary>TTL 列只允许 INT / TIMESTAMP（Nebula 约束）。</summary>
    public static bool IsTtlCompatible(string dataType)
        => dataType is Int or Timestamp;

    public static bool IsValid(string? dataType)
        => dataType is not null && All.Contains(dataType, StringComparer.Ordinal);
}
