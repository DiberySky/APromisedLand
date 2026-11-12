using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Shared.NodeEavSky;

namespace TreeGraph.Api.GraphSyncSky.SpaceNode;

/// <summary>投影出的 Tag 属性列（已规范化列名 + Nebula 类型 + 原始字段名，值映射精确回源）。</summary>
public sealed record ProjectedColumn(string Name, string NebulaType, string SourceField);

/// <summary>被跳过的字段（name = 规范化后列名或原字段名，reason = 跳过原因）。</summary>
public sealed record SkippedColumn(string Name, string Reason);

/// <summary>
/// Tag 物化 schema 快照（存 tag_projections.schema_snapshot，jsonb）：
/// 消费者/启动对齐以此为 DDL 唯一依据（PG 为唯一事实源）。
/// </summary>
public sealed record TagProjectionSchema(
    IReadOnlyList<ProjectedColumn> Columns,
    IReadOnlyList<SkippedColumn> Skipped)
{
    public static readonly TagProjectionSchema Empty = new([], []);

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public string ToJson() => JsonSerializer.Serialize(this, JsonOpts);

    public static TagProjectionSchema FromJson(string json)
        => JsonSerializer.Deserialize<TagProjectionSchema>(json, JsonOpts) ?? Empty;
}

/// <summary>
/// 空间节点派生 Tag 投影器（纯函数，无状态）：
/// 1 个组合属性（DataType=composite 的 AttributeDefinition）→ 1 个图 Tag；
/// Tag 属性列 = 所引组合类型的顶层可投影标量字段（字段级筛选）。
///
/// 命名规则（统一小写）：
///   Tag 名 = {EntityType}__{AttributeName}，非法字符（非 [a-z0-9_]）→ '_'，数字开头加 "t_"，
///   超 96 字符截断并追加 "_" + 8 位 hash（hash 取 SourceAttributeId，确定性）；
///   不同来源规范化后同名 → 追加 "_" + 8 位 hash；仍冲突 → 拒绝（由调用方 409）。
///   命中系统 Tag（<see cref="GraphNaming.AllNodeTags"/>）→ 拒绝投影（调用方记日志、账本不建行）。
/// 列名同套规范化；与系统列 name/node_type/is_archived 冲突 → 跳过该列（其余列保留）；
///   跳过后零列 → 整个 Tag 不建（创建时）；列名互相冲突 → 后到列加 "_" + 8 位 hash（hash 取 FieldId）。
/// 全部列 NULLABLE（不设 NOT NULL/DEFAULT）。
/// </summary>
public static class SpaceNodeTagProjector
{
    /// <summary>Tag 名最大长度（超限截断 + "_" + 8 位 hash）。</summary>
    public const int MaxTagNameLength = 96;

    /// <summary>列名最大长度（同套规则）。</summary>
    public const int MaxColumnNameLength = 64;

    /// <summary>系统列（派生 Tag 列名与之冲突时跳过该列）。</summary>
    public static readonly HashSet<string> SystemColumns =
        new(StringComparer.Ordinal) { "name", "node_type", "is_archived" };

    /// <summary>字段类型 → Nebula 列类型；不在表内的类型（json 及任何未知类型）不投影。</summary>
    private static readonly IReadOnlyDictionary<string, string> TypeMap =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [EavDataTypes.String] = "string",
            [EavDataTypes.SingleChoice] = "string",
            [EavDataTypes.Int] = "int64",
            [EavDataTypes.Decimal] = "double",
            [EavDataTypes.Bool] = "bool",
            [EavDataTypes.Date] = "date",
            [EavDataTypes.Time] = "time",
            [EavDataTypes.Datetime] = "timestamp"
        };

    /// <summary>组合字段可投影性：类型在映射表内、非数组、未删除。</summary>
    public static bool IsProjectable(CompositeFieldDefinition field)
        => !field.IsDeleted && !field.IsArray && TypeMap.ContainsKey(field.DataType);

    /// <summary>规范化后的 Tag 名是否命中系统 Tag（命中 → 拒绝投影）。</summary>
    public static bool IsSystemTag(string normalizedTagName)
        => GraphNaming.AllNodeTags.Contains(normalizedTagName, StringComparer.Ordinal);

    /// <summary>
    /// 计算 Tag 名（确定性）。
    /// isTaken：名称已被其他来源占用（含账本中任意空间的同名行）时返回 true。
    /// 返回 null = 基础名与加 hash 后缀均冲突（调用方 409）。
    /// </summary>
    public static string? ComputeTagName(
        string entityType, string attributeName, string sourceAttributeId,
        Func<string, bool> isTaken)
    {
        var baseName = Normalize($"{entityType}__{attributeName}", MaxTagNameLength - 9);
        if (!isTaken(baseName)) return baseName;

        var hashed = $"{baseName}_{Hash8(sourceAttributeId)}";
        return isTaken(hashed) ? null : hashed;
    }

    /// <summary>
    /// 投影组合类型字段 → Tag schema 快照。
    /// 顶层字段级筛选：仅 <see cref="IsProjectable"/> 的字段入列；
    /// 系统列冲突跳过该列并记入 Skipped；列名互相冲突后到列加 "_" + 8 位 hash（FieldId）。
    /// </summary>
    public static TagProjectionSchema ProjectSchema(IEnumerable<CompositeFieldDefinition> fields)
    {
        var columns = new List<ProjectedColumn>();
        var skipped = new List<SkippedColumn>();
        var taken = new HashSet<string>(StringComparer.Ordinal);

        foreach (var field in fields.OrderBy(f => f.DisplayOrder).ThenBy(f => f.FieldId))
        {
            if (field.IsDeleted) continue;

            if (!IsProjectable(field))
            {
                skipped.Add(new SkippedColumn(field.FieldName, "unsupported_type"));
                continue;
            }

            var name = Normalize(field.FieldName, MaxColumnNameLength - 9);
            if (SystemColumns.Contains(name))
            {
                skipped.Add(new SkippedColumn(name, "system_column"));
                continue;
            }
            if (taken.Contains(name))
            {
                var hashed = $"{name}_{Hash8(field.FieldId)}";
                if (taken.Contains(hashed))
                {
                    skipped.Add(new SkippedColumn(name, "name_conflict"));
                    continue;
                }
                name = hashed;
            }

            taken.Add(name);
            columns.Add(new ProjectedColumn(name, TypeMap[field.DataType], field.FieldName));
        }

        return new TagProjectionSchema(columns, skipped);
    }

    /// <summary>规范化：小写；非 [a-z0-9_] → '_'；数字开头加 "t_"；超 maxLength 截断。</summary>
    public static string Normalize(string raw, int maxLength)
    {
        var sb = new StringBuilder(raw.Length);
        foreach (var c in raw.ToLowerInvariant())
            sb.Append(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' ? c : '_');

        var name = sb.ToString();
        if (name.Length == 0) name = "t";
        if (name[0] >= '0' && name[0] <= '9') name = "t_" + name;
        return name.Length <= maxLength ? name : name[..maxLength];
    }

    /// <summary>确定性 8 位 hash（SHA-256 前 4 字节 hex），用于同名冲突后缀。</summary>
    public static string Hash8(string input)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)))[..8].ToLowerInvariant();

    // ============================================================
    // nGQL DDL（消费者与启动对齐共用）
    // ============================================================

    /// <summary>CREATE TAG IF NOT EXISTS（全部列 NULLABLE）。</summary>
    public static string BuildCreateTagNgql(string tagName, IReadOnlyList<ProjectedColumn> columns)
        => $"CREATE TAG IF NOT EXISTS `{tagName}`({string.Join(", ", columns.Select(ColumnDef))})";

    /// <summary>ALTER TAG ADD（补缺失列）。</summary>
    public static string BuildAlterAddNgql(string tagName, IEnumerable<ProjectedColumn> columns)
        => $"ALTER TAG `{tagName}` ADD ({string.Join(", ", columns.Select(ColumnDef))})";

    /// <summary>ALTER TAG DROP（移除快照外多余列；受管 Tag 以快照为准）。</summary>
    public static string BuildAlterDropNgql(string tagName, IEnumerable<string> columnNames)
        => $"ALTER TAG `{tagName}` DROP ({string.Join(", ", columnNames.Select(c => $"`{c}`"))})";

    /// <summary>DROP TAG IF EXISTS。</summary>
    public static string BuildDropTagNgql(string tagName)
        => $"DROP TAG IF EXISTS `{tagName}`";

    private static string ColumnDef(ProjectedColumn c) => $"`{c.Name}` {c.NebulaType} NULL";
}
