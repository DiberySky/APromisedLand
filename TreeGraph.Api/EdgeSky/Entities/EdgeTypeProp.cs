using System.Text.Json;

namespace TreeGraph.Api.EdgeSky.Entities;

/// <summary>
/// EdgeType 属性定义（镜像 NebulaGraph 边属性 Schema）：
/// 属性名 + 数据类型（<see cref="TreeGraph.Shared.EdgeSky.EdgePropDataTypes"/>）+
/// NULL/NOT NULL + DEFAULT + 注释。
/// Nebula 约束：NOT NULL 必须配 DEFAULT（DB CHECK 兜底）。
/// </summary>
public class EdgeTypeProp
{
    /// <summary>所属边类型名（FK → edge_types，Restrict）。</summary>
    public string EdgeTypeName { get; set; } = string.Empty;

    /// <summary>属性名（类型内唯一，与 EdgeTypeName 组成复合主键）。</summary>
    public string PropName { get; set; } = string.Empty;

    /// <summary>数据类型（STRING/INT/DOUBLE/BOOL/TIMESTAMP/DATE 大写常量）。</summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>是否可为空（false = NOT NULL，此时 DefaultValue 必填）。</summary>
    public bool Nullable { get; set; } = true;

    /// <summary>默认值（jsonb 字面量；null = 无默认值）。</summary>
    public JsonElement? DefaultValue { get; set; }

    /// <summary>属性注释。</summary>
    public string? Comment { get; set; }

    /// <summary>中文显示名称（仅 UI 展示用）。</summary>
    public string? DisplayName { get; set; }

    /// <summary>展示序（仅 UI 排序用，不参与图 Schema）。</summary>
    public int DisplayOrder { get; set; }

    public EdgeType EdgeType { get; set; } = null!;
}
