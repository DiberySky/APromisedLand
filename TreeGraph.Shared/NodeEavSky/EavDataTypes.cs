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
    /// <summary>记录：table 的衍生类型，首列 date、次列 time，行数据同走子表端点</summary>
    public const string Record = "record";
    public const string SingleChoice = "single_choice";
    /// <summary>
    /// 位置（经纬度坐标）：用于给节点设置地理位置，供地图导航软件定位。
    /// 值存独立表 location_attribute_values，单属性单坐标点，坐标系统一 GCJ-02。
    /// </summary>
    public const string Location = "location";

    public static readonly HashSet<string> All = new()
    {
        String, Int, Decimal, Bool, Datetime, Date, Time,
        File, Json, Composite, Table, Record, SingleChoice, Location
    };
}
