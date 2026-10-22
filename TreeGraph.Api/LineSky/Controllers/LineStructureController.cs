using Microsoft.AspNetCore.Mvc;
using TreeGraph.Api.LineSky.Services;
using TreeGraph.Api.LineSky.Templates;
using TreeGraph.Shared.LineSky.Dtos;

namespace TreeGraph.Api.LineSky.Controllers;

/// <summary>
/// 线路结构端点（Line 由 LineNode + 段边构成）。
///
/// 路由：api/eav/lines/{lineId}/structure
///   GET    读取结构（线路不存在 404；无结构 200 exists=false + 候选节点）
///   POST   创建结构（已存在 409）
///   PUT    全量替换（空请求等价于清空）
///   POST   /segments 新增一段；DELETE /segments/{edgeId} 删除一段（删最后一段同事务删头表）
///   POST   /terminals 新增/更新端子；DELETE /terminals/{nodeId} 移除端子
///
/// 响应信封与异常映射由 ApiEnvelopeFilter 统一处理。
/// </summary>
[ApiController]
[Route("api/eav/lines/{lineId}/structure")]
public class LineStructureController : ControllerBase
{
    private readonly LineStructureService _structure;

    public LineStructureController(LineStructureService structure)
        => _structure = structure;

    [HttpGet]
    public async Task<IActionResult> Get([FromRoute] string lineId, CancellationToken ct)
    {
        var dto = await _structure.GetAsync(lineId, ct);
        return dto is null
            ? NotFound(new { error = $"线路不存在: {lineId}" })
            : Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromRoute] string lineId,
        [FromBody] LineStructureRequest req,
        CancellationToken ct)
    {
        var dto = await _structure.CreateAsync(lineId, req, ct);
        return dto is null
            ? NotFound(new { error = $"线路不存在: {lineId}" })
            : Ok(dto);
    }

    [HttpPut]
    public async Task<IActionResult> Replace(
        [FromRoute] string lineId,
        [FromBody] LineStructureRequest req,
        CancellationToken ct)
    {
        var dto = await _structure.ReplaceAsync(lineId, req, ct);
        return dto is null
            ? NotFound(new { error = $"线路不存在: {lineId}" })
            : Ok(dto);
    }

    [HttpPost("segments")]
    public async Task<IActionResult> AddSegment(
        [FromRoute] string lineId,
        [FromBody] AddLineSegmentRequest req,
        CancellationToken ct)
        => Ok(await _structure.AddSegmentAsync(lineId, req, ct));

    [HttpDelete("segments/{edgeId}")]
    public async Task<IActionResult> DeleteSegment(
        [FromRoute] string lineId,
        [FromRoute] string edgeId,
        CancellationToken ct)
        => Ok(await _structure.DeleteSegmentAsync(lineId, edgeId, ct));

    [HttpPost("terminals")]
    public async Task<IActionResult> UpsertTerminal(
        [FromRoute] string lineId,
        [FromBody] AddLineTerminalRequest req,
        CancellationToken ct)
        => Ok(await _structure.UpsertTerminalAsync(lineId, req, ct));

    [HttpDelete("terminals/{nodeId}")]
    public async Task<IActionResult> DeleteTerminal(
        [FromRoute] string lineId,
        [FromRoute] string nodeId,
        CancellationToken ct)
        => Ok(await _structure.DeleteTerminalAsync(lineId, nodeId, ct));
}

/// <summary>
/// 线路模板端点：返回全部已注册的 ILineTemplate，供前端 LineStructureDialog 下拉预填。
/// 路由：api/eav/line-templates GET
/// </summary>
[ApiController]
[Route("api/eav/line-templates")]
public class LineTemplateController : ControllerBase
{
    private readonly LineTemplateRegistry _registry;

    public LineTemplateController(LineTemplateRegistry registry) => _registry = registry;

    [HttpGet]
    public IActionResult Get() => Ok(_registry.All.Select(t => t.ToDto()));
}
