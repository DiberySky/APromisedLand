using APromisedLand.Shared.DiberyTree.Interfaces;
using APromisedLand.Shared.DiberyTree.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace APromisedLand.Api.Contracts.DiberyTree;

/// <summary>
/// 泛型树控制器基类，支持任意节点值类型 T
/// </summary>
/// <typeparam name="T">节点值的类型</typeparam>
[ApiController]
[Route("[controller]")]
public abstract class TreeControllerBase<T>(ITreeService<T> treeService, 
    ILogger logger) : ControllerBase
{
    protected readonly ITreeService<T> TreeService = treeService;
    private readonly ILogger _logger = logger;

    [HttpGet("roots")]
    [HttpGet("roots/{rootId}")]
    [ProducesResponseType(StatusCodes.Status200OK)] // 移除 typeof，只保留状态码
    public virtual async Task<IActionResult> GetRoots(string? rootId = null, CancellationToken cancellationToken = default)
    {
        var roots = await TreeService.GetRootNodesAsync(rootId, cancellationToken);
        
        return Ok(roots);
    }

    [HttpGet("children/{parentId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public virtual async Task<IActionResult> GetChildren(string parentId, CancellationToken cancellationToken = default)
    {
        var children = await TreeService.GetChildrenAsync(parentId, cancellationToken);
        return Ok(children);
    }

    [HttpPost("query")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public virtual async Task<IActionResult> Query([FromBody] TreeQueryParams queryParams, CancellationToken cancellationToken = default)
    {
        var result = await TreeService.QueryNodesAsync(queryParams, cancellationToken);
        return Ok(result);
    }

    [HttpGet("full")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<IActionResult> GetFullTree([FromQuery] string? rootId = null, CancellationToken cancellationToken = default)
    {
        var tree = await TreeService.GetFullTreeAsync(rootId, cancellationToken);
        if (tree == null)
            return NotFound($"根节点 '{rootId ?? "默认"}' 不存在");
        return Ok(tree);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public virtual async Task<IActionResult> Create([FromBody] TreeNodeDto<T> node, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(node.Text))
            return BadRequest("节点文本不能为空");

        try
        {
            var created = await TreeService.CreateNodeAsync(node, cancellationToken);
            return CreatedAtAction(nameof(GetFullTree), new { rootId = created.Id }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建节点失败");
            return StatusCode(500, "创建节点时发生错误");
        }
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public virtual async Task<IActionResult> Update(string id, [FromBody] TreeNodeDto<T> node, CancellationToken cancellationToken = default)
    {
        if (id != node.Id)
            return BadRequest("URL 中的 ID 与请求体中的 ID 不一致");

        if (string.IsNullOrWhiteSpace(node.Text))
            return BadRequest("节点文本不能为空");

        try
        {
            var updated = await TreeService.UpdateNodeAsync(node, cancellationToken);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"节点 '{id}' 不存在");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新节点失败");
            return StatusCode(500, "更新节点时发生错误");
        }
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<IActionResult> Delete(string id, CancellationToken cancellationToken = default)
    {
        var result = await TreeService.DeleteNodeAsync(id, cancellationToken);
        if (!result)
            return NotFound($"节点 '{id}' 不存在或删除失败");
        return Ok(true);
    }

    [HttpPost("move")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<IActionResult> Move([FromQuery] string nodeId, [FromQuery] string? newParentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return BadRequest("节点 ID 不能为空");

        if (!string.IsNullOrEmpty(newParentId))
        {
            var parentExists = await TreeService.GetFullTreeAsync(newParentId, cancellationToken) != null;
            if (!parentExists)
                return NotFound($"目标父节点 '{newParentId}' 不存在");
        }

        var result = await TreeService.MoveNodeAsync(nodeId, newParentId, cancellationToken);
        if (!result)
            return BadRequest("移动失败，可能节点不存在或试图移动到自身子节点下");
        return Ok(true);
    }
    
    [HttpGet("{nodeId}/ancestors")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<IActionResult> GetAncestorPath(string nodeId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return BadRequest("节点 ID 不能为空");

        var path = await TreeService.GetAncestorPathAsync(nodeId, cancellationToken);
    
        if (path.Count == 0)
            return NotFound($"节点 '{nodeId}' 不存在");

        return Ok(path);
    }
}