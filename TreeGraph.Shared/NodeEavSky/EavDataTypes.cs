namespace TreeGraph.Shared.NodeEavSky;

/// <summary>EAV 类型层级。</summary>
public enum EavTypeLayer
{
    /// <summary>标量叶子：值直接存 attribute_values 各类型列，可放任何宿主。</summary>
    Scalar,

    /// <summary>引用叶子：叶子值，但依赖外部定义（选项集）。</summary>
    RefLeaf,

    /// <summary>容器类型：自身不存值，引用外部类型定义（字段集/列集）装其他类型。仅顶层。</summary>
    Container,

    /// <summary>特殊叶子：值走独立存储通道（文件/坐标表），仅顶层。</summary>
    SpecialLeaf,
}

/// <summary>类型所需的引用定义种类。</summary>
public enum EavRefKind
{
    None,
    OptionSet,
    CompositeType,
    TableDefinition,
}

/// <summary>类型允许出现的宿主位置。</summary>
[Flags]
public enum EavHost
{
    TopLevel = 1,
    CompositeField = 2,
    TableColumn = 4,
}

/// <summary>单个 dataType 的元数据：层级 / 引用需求 / 可用宿主。</summary>
public record EavDataTypeInfo(string Value, EavTypeLayer Layer, EavRefKind Ref, EavHost Hosts);

/// <summary>
/// EAV 数据类型目录：13 个类型的唯一权威定义，前后端规则全部由此派生。
/// 核心规则：
///   - 容器不进容器：composite / table 仅顶层属性可用，禁止嵌套（无递归、无环）
///   - file / location 走独立存储通道，仅顶层
///   - single_choice 不进表列（表内枚举需求将来走表列接选项集的全链路方案）
/// </summary>
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

    private const EavHost Everywhere = EavHost.TopLevel | EavHost.CompositeField | EavHost.TableColumn;
    private const EavHost TopOnly = EavHost.TopLevel;
    private const EavHost NoTableColumn = EavHost.TopLevel | EavHost.CompositeField;

    /// <summary>13 个类型的元数据目录；插入顺序即前端下拉展示顺序。</summary>
    public static readonly IReadOnlyDictionary<string, EavDataTypeInfo> Catalog =
        new Dictionary<string, EavDataTypeInfo>(StringComparer.Ordinal)
        {
            [String]       = new(String,       EavTypeLayer.Scalar,      EavRefKind.None,            Everywhere),
            [Int]          = new(Int,          EavTypeLayer.Scalar,      EavRefKind.None,            Everywhere),
            [Decimal]      = new(Decimal,      EavTypeLayer.Scalar,      EavRefKind.None,            Everywhere),
            [Bool]         = new(Bool,         EavTypeLayer.Scalar,      EavRefKind.None,            Everywhere),
            [Datetime]     = new(Datetime,     EavTypeLayer.Scalar,      EavRefKind.None,            Everywhere),
            [Date]         = new(Date,         EavTypeLayer.Scalar,      EavRefKind.None,            Everywhere),
            [Time]         = new(Time,         EavTypeLayer.Scalar,      EavRefKind.None,            Everywhere),
            [SingleChoice] = new(SingleChoice, EavTypeLayer.RefLeaf,     EavRefKind.OptionSet,       NoTableColumn),
            [Location]     = new(Location,     EavTypeLayer.SpecialLeaf, EavRefKind.None,            TopOnly),
            [File]         = new(File,         EavTypeLayer.SpecialLeaf, EavRefKind.None,            TopOnly),
            [Composite]    = new(Composite,    EavTypeLayer.Container,   EavRefKind.CompositeType,   TopOnly),
            [Table]        = new(Table,        EavTypeLayer.Container,   EavRefKind.TableDefinition, TopOnly),
            [Json]         = new(Json,         EavTypeLayer.Scalar,      EavRefKind.None,            Everywhere),
        };

    /// <summary>全部类型（由目录派生）。</summary>
    public static readonly HashSet<string> All = [.. Catalog.Keys];

    /// <summary>组合字段可用类型（8 标量 + single_choice）。</summary>
    public static readonly HashSet<string> CompositeFieldAllowed = ForHost(EavHost.CompositeField);

    /// <summary>表列可用类型（8 标量）。</summary>
    public static readonly HashSet<string> TableColumnAllowed = ForHost(EavHost.TableColumn);

    /// <summary>取类型元数据；未知类型抛 KeyNotFoundException（调用方先用 All/Catalog 校验）。</summary>
    public static EavDataTypeInfo InfoOf(string dataType) => Catalog[dataType];

    public static bool IsContainer(string dataType)
        => Catalog.TryGetValue(dataType, out var info) && info.Layer == EavTypeLayer.Container;

    /// <summary>按宿主位置过滤出可用类型集合。</summary>
    public static HashSet<string> ForHost(EavHost host)
        => Catalog.Values.Where(t => t.Hosts.HasFlag(host)).Select(t => t.Value).ToHashSet();
}
