using System.Text.Json;

namespace TreeGraph.Shared.EdgeSky.Dtos;

/// <summary>EdgeType 属性 Schema 项（读写共用）：镜像 NebulaGraph 边属性定义。</summary>
public class EdgeTypePropDto
{
    public string PropName { get; set; } = string.Empty;

    /// <summary>数据类型（STRING/INT/DOUBLE/BOOL/TIMESTAMP/DATE，见 EdgePropDataTypes）。</summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>是否可为空（false = NOT NULL，此时 DefaultValue 必填，Nebula 约束）。</summary>
    public bool Nullable { get; set; } = true;

    /// <summary>默认值（json 字面量；null = 无默认值）。</summary>
    public JsonElement? DefaultValue { get; set; }

    public string? Comment { get; set; }

    /// <summary>中文显示名称（仅 UI 展示用）。</summary>
    public string? DisplayName { get; set; }

    /// <summary>展示序（仅 UI 排序用，不参与图 Schema）。</summary>
    public int DisplayOrder { get; set; }
}

/// <summary>EdgeType 完整视图（名称 + 属性 Schema + TTL + 注释 + 中文显示名）。</summary>
public class EdgeTypeDto
{
    public string Name { get; set; } = string.Empty;

    /// <summary>中文显示名称（仅 UI 展示用，Nebula 无此概念）。</summary>
    public string? DisplayName { get; set; }
    public string? Comment { get; set; }
    public string? TtlCol { get; set; }
    public long? TtlDurationSeconds { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<EdgeTypePropDto> Props { get; set; } = [];
}

/// <summary>
/// 创建 EdgeType 请求（对齐 CREATE EDGE）：
/// 名称创建后不可改；TTL 与注释仅创建时可设（Nebula 不支持 ALTER 修改）。
/// </summary>
public class CreateEdgeTypeRequest
{
    public string Name { get; set; } = string.Empty;

    /// <summary>中文显示名称（仅 UI 展示用）。</summary>
    public string? DisplayName { get; set; }

    public string? Comment { get; set; }

    /// <summary>TTL 列名（须为本类型的 INT/TIMESTAMP 属性；与 TtlDurationSeconds 成对提供）。</summary>
    public string? TtlCol { get; set; }

    /// <summary>TTL 时长（秒）。</summary>
    public long? TtlDurationSeconds { get; set; }

    public List<EdgeTypePropDto> Props { get; set; } = [];
}

/// <summary>
/// ALTER EDGE 请求：仅支持属性集变更（增/删/改类型），与 Nebula 能力对齐。
/// 名称 / TTL / 注释不可变更。
/// </summary>
public class AlterEdgeTypeRequest
{
    /// <summary>新增属性（ALTER ADD）。</summary>
    public List<EdgeTypePropDto> AddProps { get; set; } = [];

    /// <summary>删除属性名（ALTER DROP；TTL 列不可删）。</summary>
    public List<string> DropProps { get; set; } = [];

    /// <summary>类型变更（ALTER CHANGE；仅 Nebula 白名单转换）。</summary>
    public List<EdgeTypePropTypeChange> ChangeProps { get; set; } = [];
}

/// <summary>属性变更项（类型走 Nebula ALTER CHANGE；可空/默认值为 PG 元数据变更，不同步 DDL）。</summary>
public class EdgeTypePropTypeChange
{
    public string PropName { get; set; } = string.Empty;

    /// <summary>目标类型；空 = 不变更类型（仅 Nebula 白名单转换）。</summary>
    public string NewDataType { get; set; } = string.Empty;

    /// <summary>目标可空性；null = 不变更（改 NOT NULL 须保证生效默认值非空）。</summary>
    public bool? Nullable { get; set; }

    /// <summary>是否变更默认值（true 时按 <see cref="DefaultValue"/> 应用，null 值 = 清除默认值）。</summary>
    public bool DefaultValueSet { get; set; }

    /// <summary>新默认值（配合 <see cref="DefaultValueSet"/>）。</summary>
    public System.Text.Json.JsonElement? DefaultValue { get; set; }
}

/// <summary>更新边类型中文显示名请求（非 Nebula 概念，仅 UI 展示）。</summary>
public class UpdateEdgeTypeDisplayNameRequest
{
    /// <summary>中文显示名称；空/空白 = 清除。</summary>
    public string? DisplayName { get; set; }
}

/// <summary>更新边属性中文显示名请求（非 Nebula 概念，仅 UI 展示）。</summary>
public class UpdateEdgePropDisplayNameRequest
{
    /// <summary>中文显示名称；空/空白 = 清除。</summary>
    public string? DisplayName { get; set; }
}
