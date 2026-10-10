using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TreeGraph.Api.TreeEavSky.Services;
using TreeGraph.Shared.TreeEavSky.Models;

namespace TreeGraph.Api.TreeEavSky.Controllers;

/// <summary>
/// 泛型树控制器基类，支持任意节点值类型 T 的树节点 CRUD。
/// 响应体统一为裸数据/匿名错误体，由全局 ApiEnvelopeFilter 包装为
/// { success, message, data } 信封（与 NodeEavSky 同一过滤器）。
/// </summary>
/// <typeparam name="T">节点值的类型</typeparam>
[ApiController]
[Route("[controller]")]
public abstract class TreeControllerBase<T>(
    ITreeService<T> treeService,
    ILogger<TreeControllerBase<T>> logger)
    : ControllerBase
{
    protected readonly ITreeService<T> TreeService = treeService;
    protected readonly ILogger Logger = logger;

    // ==================== 树节点操作 ====================

    [HttpGet("roots")]
    [HttpGet("roots/{rootId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public virtual async Task<IActionResult> GetRoots(string? rootId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var roots = await TreeService.GetRootNodesAsync(rootId, cancellationToken);
            return Ok(roots);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "获取根节点失败");
            return BadRequest(new { error = $"获取根节点失败: {ex.Message}" });
        }
    }

    [HttpGet("children/{parentId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public virtual async Task<IActionResult> GetChildren(string parentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var children = await TreeService.GetChildrenAsync(parentId, cancellationToken);
            return Ok(children);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "获取子节点失败");
            return BadRequest(new { error = $"获取子节点失败: {ex.Message}" });
        }
    }

    [HttpPost("query")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public virtual async Task<IActionResult> Query([FromBody] TreeQueryParams queryParams, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await TreeService.QueryNodesAsync(queryParams, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "查询节点失败");
            return BadRequest(new { error = $"查询节点失败: {ex.Message}" });
        }
    }

    [HttpGet("full")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<IActionResult> GetFullTree([FromQuery] string? rootId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var tree = await TreeService.GetFullTreeAsync(rootId, cancellationToken);
            if (tree == null)
                return NotFound(new { error = $"根节点 '{rootId ?? "默认"}' 不存在" });
            return Ok(tree);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "获取完整树失败");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = $"获取完整树失败: {ex.Message}" });
        }
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public virtual async Task<IActionResult> Create([FromBody] TreeNodeDto<T> node, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(node.Text))
            return BadRequest(new { error = "节点文本不能为空" });

        try
        {
            var created = await TreeService.CreateNodeAsync(node, cancellationToken);
            return CreatedAtAction(nameof(GetFullTree), new { rootId = created.Id }, created);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "创建节点失败");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = $"创建节点时发生错误: {ex.Message}" });
        }
    }

    [HttpPost("children")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<IActionResult> UpdateChildren([FromBody] TreeNodeDto<T> nodeDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var updated = await TreeService.UpdateChildrenAsync(nodeDto, cancellationToken);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "更新节点子项失败");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = $"更新节点子项时发生错误: {ex.Message}" });
        }
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public virtual async Task<IActionResult> Update(string id, [FromBody] TreeNodeDto<T> node, CancellationToken cancellationToken = default)
    {
        if (id != node.Id)
            return BadRequest(new { error = "URL 中的 ID 与请求体中的 ID 不一致" });

        if (string.IsNullOrWhiteSpace(node.Text))
            return BadRequest(new { error = "节点文本不能为空" });

        try
        {
            var updated = await TreeService.UpdateNodeAsync(node, cancellationToken);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"节点 '{id}' 不存在" });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "更新节点失败");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = $"更新节点时发生错误: {ex.Message}" });
        }
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<IActionResult> Delete(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await TreeService.DeleteNodeAsync(id, cancellationToken);
            if (!result)
                return NotFound(new { error = $"节点 '{id}' 不存在或删除失败" });
            return Ok(true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "删除节点失败");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = $"删除节点时发生错误: {ex.Message}" });
        }
    }

    [HttpPost("move")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<IActionResult> Move([FromQuery] string nodeId, [FromQuery] string? newParentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return BadRequest(new { error = "节点 ID 不能为空" });

        if (!string.IsNullOrEmpty(newParentId))
        {
            try
            {
                var parentExists = await TreeService.GetFullTreeAsync(newParentId, cancellationToken) != null;
                if (!parentExists)
                    return NotFound(new { error = $"目标父节点 '{newParentId}' 不存在" });
            }
            catch (Exception ex)
            {
                return NotFound(new { error = $"检查父节点存在性失败: {ex.Message}" });
            }
        }

        try
        {
            var result = await TreeService.MoveNodeAsync(nodeId, newParentId, cancellationToken);
            if (!result)
                return BadRequest(new { error = "移动失败：试图移动到自身子节点下" });
            return Ok(true);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"节点 '{nodeId}' 不存在" });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "移动节点失败");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = $"移动节点时发生错误: {ex.Message}" });
        }
    }

    [HttpGet("{nodeId}/ancestors")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<IActionResult> GetAncestorPath(string nodeId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return BadRequest(new { error = "节点 ID 不能为空" });

        try
        {
            var path = await TreeService.GetAncestorPathAsync(nodeId, cancellationToken);
            if (path.Count == 0)
                return NotFound(new { error = $"节点 '{nodeId}' 不存在" });
            return Ok(path);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "获取祖先路径失败");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = $"获取祖先路径失败: {ex.Message}" });
        }
    }
}
