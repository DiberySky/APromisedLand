using Microsoft.AspNetCore.Mvc;
using TreeGraph.Api.LineSky.Services;
using TreeGraph.Api.EdgeSky.Entities;
using TreeGraph.Shared.LineSky.Dtos;

namespace TreeGraph.Api.LineSky.Controllers;

/// <summary>
/// 线路结构（Line）专用端点。
///
/// 线路结构是 Edge（关系/边）的特例：EdgeDefinition.IsLine=true。
/// 端点覆盖：批量创建线路、完整路径查询、拓扑校验、连通分量。
///
/// 响应体统一交由 ApiEnvelopeFilter 包装为 ApiResponse，控制器不再手工套
/// { success, message, data }，避免与全局信封叠成双层（前端只解一层信封）。
/// </summary>
[ApiController]
[Route("api/eav/lines")]
public class LineController : ControllerBase
{
    private readonly LineService _line;

    public LineController(LineService line) => _line = line;

    // ============================================================
    // 批量创建线路
    // ============================================================

    /// <summary>
    /// 按顺序创建 A→B→C→D 的线路边。全事务，任一段失败整体回滚。
    /// 建线前校验：节点不重复、节点存在且类型匹配、候选图不成环。
    /// 异常统一交由 ApiEnvelopeFilter 映射：
    ///   ArgumentException → 400；InvalidOperationException（业务规则冲突）→ 409。
    /// </summary>
    [HttpPost("{edgeTypeId}/create")]
    public async Task<IActionResult> CreateLine(
        [FromRoute] string edgeTypeId,
        [FromBody] CreateLineRequest req,
        CancellationToken ct)
    {
        var edges = await _line.CreateLineAsync(edgeTypeId, req, ct);
        return Ok(edges);
    }

    // ============================================================
    // 路径查询（仅严格线性线路）
    // ============================================================

    /// <summary>从 nodeId 向上游走到底（找整条上游线路）。</summary>
    [HttpGet("{edgeTypeId}/upstream/{nodeId}")]
    public async Task<IActionResult> Upstream(
        [FromRoute] string edgeTypeId,
        [FromRoute] string nodeId,
        CancellationToken ct)
    {
        var (error, def) = await GuardLinearAsync(edgeTypeId, ct);
        if (error is not null) return error;

        var path = await _line.GetPathAsync(edgeTypeId, nodeId, "up", ct, def: def);
        return Ok(path);
    }

    /// <summary>从 nodeId 向下游走到底（找整条下游线路）。</summary>
    [HttpGet("{edgeTypeId}/downstream/{nodeId}")]
    public async Task<IActionResult> Downstream(
        [FromRoute] string edgeTypeId,
        [FromRoute] string nodeId,
        CancellationToken ct)
    {
        var (error, def) = await GuardLinearAsync(edgeTypeId, ct);
        if (error is not null) return error;

        var path = await _line.GetPathAsync(edgeTypeId, nodeId, "down", ct, def: def);
        return Ok(path);
    }

    /// <summary>
    /// 获取 nodeId 所在的完整线路（先 up 到源头，再 down 到底，拼接成一条有序线路）。
    /// 无向线路（IsDirected=false）方向无意义，单次双向展开即为整条线路，无需拼接。
    /// </summary>
    [HttpGet("{edgeTypeId}/path/{nodeId}")]
    public async Task<IActionResult> FullPath(
        [FromRoute] string edgeTypeId,
        [FromRoute] string nodeId,
        CancellationToken ct)
    {
        var (error, def) = await GuardLinearAsync(edgeTypeId, ct);
        if (error is not null) return error;

        // 无向线路：up/down 语义不适用，直接返回一次双向展开的完整线路
        if (def is { IsDirected: false })
        {
            var walk = await _line.GetPathAsync(edgeTypeId, nodeId, "down", ct, def: def);
            return Ok(walk);
        }

        // 1. 向上游走到源头（不含当前节点）
        var upstream = await _line.GetPathAsync(edgeTypeId, nodeId, "up", ct, def: def);
        // 2. 向下游走到终点
        var downstream = await _line.GetPathAsync(edgeTypeId, nodeId, "down", ct, def: def);

        // 拼接：upstream 的 nodeIds 反向 + 当前 + downstream 的 nodeIds[1..]
        var orderedNodes = upstream.NodeIds.Reverse().Concat(downstream.NodeIds.Skip(1)).ToList();

        // 边：upstream.Edges 反向 + downstream.Edges
        var orderedEdges = upstream.Edges.Reverse().Concat(downstream.Edges).ToList();

        var result = new LinePathResult(
            orderedNodes, orderedEdges, orderedEdges.Count,
            upstream.Truncated || downstream.Truncated);
        return Ok(result);
    }

    // ============================================================
    // 拓扑校验
    // ============================================================

    /// <summary>校验线路结构拓扑：入度/出度/成环。返回诊断详情。</summary>
    [HttpGet("{edgeTypeId}/validate")]
    public async Task<IActionResult> Validate(
        [FromRoute] string edgeTypeId,
        CancellationToken ct)
    {
        if (await _line.GetDefinitionAsync(edgeTypeId, ct) is null)
            return NotFound(new { error = $"EdgeType 不存在: {edgeTypeId}" });

        return Ok(await _line.ValidateTopologyAsync(edgeTypeId, ct));
    }

    // ============================================================
    // 连通分量
    // ============================================================

    /// <summary>找出该 EdgeType 下所有互不相连的线路分量（多条独立线路）。</summary>
    [HttpGet("{edgeTypeId}/components")]
    public async Task<IActionResult> Components(
        [FromRoute] string edgeTypeId,
        CancellationToken ct)
    {
        if (await _line.GetDefinitionAsync(edgeTypeId, ct) is null)
            return NotFound(new { error = $"EdgeType 不存在: {edgeTypeId}" });

        return Ok(await _line.GetConnectedComponentsAsync(edgeTypeId, ct));
    }

    /// <summary>
    /// 路径类端点的前置守卫：类型不存在 → 404；非严格线性线路 → 400。
    /// 非线性线路的路径查询会把分支/合流摊平成“伪单线”，故直接拒绝。
    /// 返回 (错误结果, 定义) 供调用方复用定义（避免重复查询）。
    /// </summary>
    private async Task<(IActionResult? Error, EdgeDefinition? Def)> GuardLinearAsync(
        string edgeTypeId, CancellationToken ct)
    {
        var def = await _line.GetDefinitionAsync(edgeTypeId, ct);
        if (def is null)
            return (NotFound(new { error = $"EdgeType 不存在: {edgeTypeId}" }), null);

        if (!def.IsLinear)
            return (BadRequest(new
            {
                error = $"关系类型 {def.DisplayName} 非严格线性线路，不支持路径查询（请改用 /components 或图遍历）"
            }), def);

        return (null, def);
    }
}
