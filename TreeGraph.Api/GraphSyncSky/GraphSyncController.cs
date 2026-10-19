using Microsoft.AspNetCore.Mvc;

namespace TreeGraph.Api.GraphSyncSky;

/// <summary>
/// 图同步 API。
/// 路由：POST /api/graph-sync/rebuild
/// 响应信封由 NodeEavSky 全局 ApiEnvelopeFilter 统一包装。
/// </summary>
[ApiController]
[Route("api/graph-sync")]
public sealed class GraphSyncController : ControllerBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GraphSyncController> _logger;

    public GraphSyncController(
        IServiceScopeFactory scopeFactory,
        ILogger<GraphSyncController> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// 全量重建：DROP SPACE → 重建 schema → 从 PG 全量装载顶点/边写入 NebulaGraph。
    /// 用于图库初始化或数据漂移修复。
    /// </summary>
    [HttpPost("rebuild")]
    public async Task<IActionResult> Rebuild(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var synchronizer = scope.ServiceProvider.GetRequiredService<GraphSynchronizer>();

        var result = await synchronizer.RebuildAllAsync(ct);

        _logger.LogInformation(
            "手动图全量重建：顶点 {V} 父子边 {PE} 语义边 {SE} 错误 {Err}",
            result.Vertices, result.ParentEdges, result.SemanticEdges, result.Errors.Count);

        return Ok(result);
    }
}
