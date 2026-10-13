using Microsoft.AspNetCore.Mvc;
using TreeGraph.Api.NodeEavSky.Services;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Api.NodeEavSky.Controllers;

/// <summary>
/// 链式结构（Chain）专用端点。
///
/// 链式结构是 Edge（关系/边）的特例：EdgeDefinition.IsChain=true。
/// 端点覆盖：批量创建链、完整路径查询、拓扑校验、连通分量。
/// </summary>
[ApiController]
[Route("api/eav/chains")]
public class ChainController : ControllerBase
{
    private readonly ChainService _chain;

    public ChainController(ChainService chain) => _chain = chain;

    // ============================================================
    // 批量创建链
    // ============================================================

    /// <summary>
    /// 按顺序创建 A→B→C→D 的链式边。全事务，任一段失败整体回滚。
    /// </summary>
    [HttpPost("{edgeTypeId}/create")]
    public async Task<IActionResult> CreateChain(
        [FromRoute] string edgeTypeId,
        [FromBody] CreateChainRequest req,
        CancellationToken ct)
    {
        try
        {
            var edges = await _chain.CreateChainAsync(edgeTypeId, req, ct);
            return Ok(new
            {
                success = true,
                message = $"创建链成功：{edges.Count} 段边",
                data = edges
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { success = false, error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    // ============================================================
    // 路径查询
    // ============================================================

    /// <summary>
    /// 从 nodeId 向上游走到底（找整条上游链）。
    /// </summary>
    [HttpGet("{edgeTypeId}/upstream/{nodeId}")]
    public async Task<IActionResult> Upstream(
        [FromRoute] string edgeTypeId,
        [FromRoute] string nodeId,
        CancellationToken ct)
    {
        var path = await _chain.GetPathAsync(edgeTypeId, nodeId, "up", ct);
        return Ok(new { success = true, data = path });
    }

    /// <summary>
    /// 从 nodeId 向下游走到底（找整条下游链）。
    /// </summary>
    [HttpGet("{edgeTypeId}/downstream/{nodeId}")]
    public async Task<IActionResult> Downstream(
        [FromRoute] string edgeTypeId,
        [FromRoute] string nodeId,
        CancellationToken ct)
    {
        var path = await _chain.GetPathAsync(edgeTypeId, nodeId, "down", ct);
        return Ok(new { success = true, data = path });
    }

    /// <summary>
    /// 获取 nodeId 所在的完整链（先 up 到源头，再 down 到底，拼接成一条有序链）。
    /// </summary>
    [HttpGet("{edgeTypeId}/path/{nodeId}")]
    public async Task<IActionResult> FullPath(
        [FromRoute] string edgeTypeId,
        [FromRoute] string nodeId,
        CancellationToken ct)
    {
        // 1. 向上游走到源头（不含当前节点）
        var upstream = await _chain.GetPathAsync(edgeTypeId, nodeId, "up", ct);
        // 2. 向下游走到终点
        var downstream = await _chain.GetPathAsync(edgeTypeId, nodeId, "down", ct);

        // 拼接：upstream 的 nodeIds 反向 + 当前 + downstream 的 nodeIds[1..]
        var orderedNodes = upstream.NodeIds.Reverse().Concat(downstream.NodeIds.Skip(1)).ToList();

        // 边：upstream.Edges 反向 + downstream.Edges
        var orderedEdges = upstream.Edges.Reverse().Concat(downstream.Edges).ToList();

        var result = new ChainPathResult(orderedNodes, orderedEdges, orderedEdges.Count);
        return Ok(new { success = true, data = result });
    }

    // ============================================================
    // 拓扑校验
    // ============================================================

    /// <summary>
    /// 校验链式结构拓扑：入度/出度/成环。返回诊断详情。
    /// </summary>
    [HttpGet("{edgeTypeId}/validate")]
    public async Task<IActionResult> Validate(
        [FromRoute] string edgeTypeId,
        CancellationToken ct)
    {
        var result = await _chain.ValidateTopologyAsync(edgeTypeId, ct);
        return Ok(new { success = true, data = result });
    }

    // ============================================================
    // 连通分量
    // ============================================================

    /// <summary>
    /// 找出该 EdgeType 下所有互不相连的链式分量（多条独立链）。
    /// </summary>
    [HttpGet("{edgeTypeId}/components")]
    public async Task<IActionResult> Components(
        [FromRoute] string edgeTypeId,
        CancellationToken ct)
    {
        var comps = await _chain.GetConnectedComponentsAsync(edgeTypeId, ct);
        return Ok(new { success = true, data = comps });
    }
}
