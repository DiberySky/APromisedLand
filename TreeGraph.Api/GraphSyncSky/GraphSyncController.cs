using Microsoft.AspNetCore.Mvc;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Api.GraphSyncSky;

/// <summary>
/// 图同步 API。
/// 路由：POST /api/graph-sync/rebuild（全量重建）
///       POST /api/graph-sync/category/{category}（按类别幂等推送）
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

    /// <summary>
    /// 按类别幂等推送（不 DROP SPACE）：把 PG 中该类别现状批量覆盖写入 NebulaGraph。
    /// 用于同步刷新对话框分步展示；图内残留清理仍靠事件同步 / 全量重建。
    /// category：space=空间树；tree=单位/分类/知识树；line=线路；edge=语义边。
    /// </summary>
    [HttpPost("category/{category}")]
    public async Task<IActionResult> SyncCategory(string category, CancellationToken ct)
    {
        if (!SyncCategoryParser.TryParse(category, out var cat))
            return BadRequest(new { error = "category 仅支持 space|tree|line|edge" });

        await using var scope = _scopeFactory.CreateAsyncScope();
        var synchronizer = scope.ServiceProvider.GetRequiredService<GraphSynchronizer>();

        var result = await synchronizer.SyncCategoryAsync(cat, ct);

        _logger.LogInformation(
            "手动图按类别同步：{Category} 顶点 {V}/{VT} 边 {E}/{ET} 错误 {Err}",
            cat, result.VerticesWritten, result.Vertices,
            result.EdgesWritten, result.ParentEdges + result.SemanticEdges, result.Errors.Count);

        return Ok(new GraphCategorySyncResultDto(
            result.Vertices, result.ParentEdges + result.SemanticEdges,
            result.VerticesWritten, result.EdgesWritten, result.Errors));
    }
}
