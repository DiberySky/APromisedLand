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

    public static readonly HashSet<string> All = new()
    {
        String, Int, Decimal, Bool, Datetime, Date, Time,
        File, Json, Composite, Table, SingleChoice
    };
}
