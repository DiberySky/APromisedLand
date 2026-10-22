namespace TreeGraph.MafWfApi.Infrastructure;

/// <summary>
/// NebulaGraph 查询配置（NebulaFastApi 节）。
/// 端点由 AppHost 通过环境变量 NebulaFastApi__Endpoint 注入；
/// 本地无编排运行时回退 http://localhost:9393/。
/// </summary>
public sealed class NebulaGraphOptions
{
    public const string SectionName = "NebulaFastApi";

    public const string DefaultEndpoint = "http://localhost:9393/";

    /// <summary>
    /// TreeGraph.NebulaFastApi REST 端点。
    /// 为 null/空时回退 <see cref="DefaultEndpoint"/>。
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>NebulaGraph 图空间名。</summary>
    public string Space { get; set; } = "treegraph";
}
