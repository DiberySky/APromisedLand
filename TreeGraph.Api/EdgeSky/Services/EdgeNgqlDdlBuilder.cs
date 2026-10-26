using System.Text;
using System.Text.Json;
using TreeGraph.Api.EdgeSky.Entities;
using TreeGraph.Shared.EdgeSky;

namespace TreeGraph.Api.EdgeSky.Services;

/// <summary>
/// Edge DDL（nGQL）构建器：CREATE EDGE / 列定义 / 字面量格式化。
/// NebulaEdgeSchemaGateway（运行期 DDL）与 GraphSchemaInitializer（启动对齐）共用。
/// </summary>
internal static class EdgeNgqlDdlBuilder
{
    public static string BuildCreateEdgeNgql(EdgeType type)
    {
        var sb = new StringBuilder($"CREATE EDGE IF NOT EXISTS `{type.Name}`(");
        sb.Append(string.Join(", ", type.Props.OrderBy(p => p.DisplayOrder).Select(BuildColumnDef)));
        sb.Append(')');
        if (!string.IsNullOrEmpty(type.Comment))
            sb.Append($" COMMENT = '{type.Comment.Replace("\\", "\\\\").Replace("'", "\\'")}'");
        if (type.TtlCol is not null)
            sb.Append($" TTL_DURATION = {type.TtlDurationSeconds}, TTL_COL = \"{type.TtlCol}\"");
        return sb.ToString();
    }

    public static string BuildColumnDef(EdgeTypeProp p)
    {
        var def = $"`{p.PropName}` {p.DataType} {(p.Nullable ? "NULL" : "NOT NULL")}";
        if (p.DefaultValue is { } dv)
            def += $" DEFAULT {FormatLiteral(p.DataType, dv)}";
        return def;
    }

    /// <summary>把 jsonb 默认值格式化为 nGQL 字面量（值合法性由服务层先行校验）。</summary>
    public static string FormatLiteral(string dataType, JsonElement value) => dataType switch
    {
        EdgePropDataTypes.String => $"\"{EscapeString(value.GetString() ?? "")}\"",
        EdgePropDataTypes.Bool => value.GetBoolean() ? "true" : "false",
        EdgePropDataTypes.Int or EdgePropDataTypes.Double => value.GetRawText(),
        EdgePropDataTypes.Timestamp => value.ValueKind == JsonValueKind.Number
            ? value.GetRawText()
            : $"\"{EscapeString(value.GetString() ?? "")}\"",
        EdgePropDataTypes.Date => $"\"{value.GetString()}\"",
        _ => value.GetRawText()
    };

    private static string EscapeString(string s)
        => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
