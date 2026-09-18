using System.ComponentModel.DataAnnotations;

namespace MAFWorkFlowApi.Infrastructure;

public sealed class LiteGraphOptions
{
    public const string SectionName = "LiteGraph";

    /// <summary>
    /// LiteGraph REST Server 端点。
    /// 为 null/空时由 LiteGraphEndpointResolver 从 Aspire 环境变量解析：
    ///   services__litegraph__http__0  →  http://litegraph:8701
    /// </summary>
    [Url]
    public string? Endpoint { get; set; }

    /// <summary>租户 GUID。默认租户由 LiteGraph 服务器自动创建。</summary>
    [Required]
    public string TenantGuid { get; set; } =
        "00000000-0000-0000-0000-000000000000";

    /// <summary>
    /// Bearer Token（在 LiteGraph 语境下等同于 API Key）。
    /// 通过 Authorization: Bearer {ApiKey} 头传递。
    /// </summary>
    [Required]
    public string ApiKey { get; set; } = "litegraphadmin";
}