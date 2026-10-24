namespace TreeGraph.Shared.NodeEavSky;

/// <summary>EAV 基础数据类型常量</summary>
public static class EavDataTypes
{
    public const string String = "string";
    public const string Int = "int";
    public const string Decimal = "decimal";
    public const string Bool = "bool";
    public const string Datetime = "datetime";
    public const string Date = "date";
    public const string Time = "time";
    public const string File = "file";
    public const string Json = "json";
    public const string Composite = "composite";
    public const string Table = "table";
    public const string SingleChoice = "single_choice";
    /// <summary>
    /// 位置（经纬度坐标）：用于给节点设置地理位置，供地图导航软件定位。
    /// 值存独立表 location_attribute_values，单属性单坐标点，坐标系统一 GCJ-02。
    /// </summary>
    public const string Location = "location";

    /// <summary>
    /// 关系（图边）：一条记录 = 一条有向边（宿主实体 → 目标节点）。
    /// 值存独立表 relation_attribute_values，单属性单目标；图边类型取自属性定义的 relation_type。
    /// </summary>
    public const string Relation = "relation";

    public static readonly HashSet<string> All = new()
    {
        String, Int, Decimal, Bool, Datetime, Date, Time,
        File, Json, Composite, Table, SingleChoice, Location, Relation
    };
}
