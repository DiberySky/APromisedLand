using Microsoft.AspNetCore.Mvc;
using TreeGraph.Api.StringTreeSky.Services;
using TreeGraph.Shared.StringTreeSky.Contracts;

namespace TreeGraph.Api.StringTreeSky.Controllers;

[ApiController]
[Route("api/string-tree")]
public class StringTreeNodeController : ControllerBase
{
    private readonly IStringTreeService _service;
    public StringTreeNodeController(IStringTreeService service) => _service = service;

    [HttpGet("nodes/roots")]
    [ProducesResponseType(typeof(StringTreeResponse<List<StringNodeDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<StringTreeResponse<List<StringNodeDto>>>> GetRoots(CancellationToken ct)
        => Ok(StringTreeResponse<List<StringNodeDto>>.Ok(await _service.GetRootNodesAsync(ct)));

    [HttpGet("nodes/children/{parentId}")]
    [ProducesResponseType(typeof(StringTreeResponse<List<StringNodeDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<StringTreeResponse<List<StringNodeDto>>>> GetChildren(string parentId, CancellationToken ct)
        => Ok(StringTreeResponse<List<StringNodeDto>>.Ok(await _service.GetChildrenAsync(parentId, ct)));

    [HttpGet("nodes/{id}")]
    [ProducesResponseType(typeof(StringTreeResponse<StringNodeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(StringTreeResponse<StringNodeDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StringTreeResponse<StringNodeDto>>> GetNode(string id, CancellationToken ct)
    {
        var node = await _service.GetNodeAsync(id, ct);
        return node is null
            ? NotFound(StringTreeResponse<StringNodeDto>.Fail("节点不存在"))
            : Ok(StringTreeResponse<StringNodeDto>.Ok(node));
    }

    [HttpGet("nodes/{id}/ancestors")]
    [ProducesResponseType(typeof(StringTreeResponse<List<StringNodeDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<StringTreeResponse<List<StringNodeDto>>>> GetAncestors(string id, CancellationToken ct)
        => Ok(StringTreeResponse<List<StringNodeDto>>.Ok(await _service.GetAncestorPathAsync(id, ct)));

    [HttpPost("nodes")]
    [ProducesResponseType(typeof(StringTreeResponse<StringNodeDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<StringTreeResponse<StringNodeDto>>> Create(
        [FromBody] StringNodeDto dto, CancellationToken ct)
    {
        var created = await _service.CreateNodeAsync(dto, ct);
        return CreatedAtAction(nameof(GetNode), new { id = created.Id },
            StringTreeResponse<StringNodeDto>.Ok(created));
    }

    [HttpPut("nodes/{id}")]
    [ProducesResponseType(typeof(StringTreeResponse<StringNodeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(StringTreeResponse<StringNodeDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StringTreeResponse<StringNodeDto>>> Update(
        string id, [FromBody] StringNodeDto dto, CancellationToken ct)
    {
        var updated = await _service.UpdateNodeAsync(id, dto, ct);
        return updated is null
            ? NotFound(StringTreeResponse<StringNodeDto>.Fail("节点不存在"))
            : Ok(StringTreeResponse<StringNodeDto>.Ok(updated));
    }

    [HttpDelete("nodes/{id}")]
    [ProducesResponseType(typeof(StringTreeResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(StringTreeResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StringTreeResponse<bool>>> Delete(string id, CancellationToken ct)
    {
        var ok = await _service.DeleteNodeAsync(id, ct);
        return ok
            ? Ok(StringTreeResponse<bool>.Ok(true))
            : NotFound(StringTreeResponse<bool>.Fail("节点不存在"));
    }

    [HttpPost("nodes/{id}/move")]
    [ProducesResponseType(typeof(StringTreeResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(StringTreeResponse<bool>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StringTreeResponse<bool>>> Move(
        string id, [FromBody] MoveRequest req, CancellationToken ct)
    {
        var ok = await _service.MoveNodeAsync(id, req.ParentId, req.SortOrder, ct);
        return ok
            ? Ok(StringTreeResponse<bool>.Ok(true))
            : BadRequest(StringTreeResponse<bool>.Fail("移动失败：节点不存在或会形成环"));
    }

    [HttpPost("nodes/{parentId}/children/sort")]
    [ProducesResponseType(typeof(StringTreeResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(StringTreeResponse<bool>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StringTreeResponse<bool>>> Sort(
        string parentId, [FromBody] List<string> orderedIds, CancellationToken ct)
    {
        var ok = await _service.SortChildrenAsync(parentId, orderedIds, ct);
        return ok
            ? Ok(StringTreeResponse<bool>.Ok(true))
            : BadRequest(StringTreeResponse<bool>.Fail("排序失败"));
    }

    public class MoveRequest
    {
        public string? ParentId { get; set; }
        public int SortOrder { get; set; }
    }
}
