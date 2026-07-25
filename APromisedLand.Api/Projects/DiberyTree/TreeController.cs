using APromisedLand.Api.Projects.DiberyTree.Interface;
using APromisedLand.Api.Projects.DiberyTree.Services;
using APromisedLand.Shared.DiberyTree.Interfaces;
using APromisedLand.Shared.DiberyTree.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace APromisedLand.Api.Projects.DiberyTree;

/// <summary>
/// 泛型树 API 控制器
/// </summary>
/// <typeparam name="T">节点值的类型</typeparam>
[ApiController]
[Route("[controller]")]
public class TreeController<T>(ITreeService<T> treeService, ILogger<TreeController<T>> logger)
    : ControllerBase
{
    /// <summary>
    /// 获取根节点列表
    /// </summary>
    [HttpGet("roots/{rootId}")]
    public async Task<IActionResult> GetRoots(string? rootId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var roots = await treeService.GetRootNodesAsync(rootId, cancellationToken);
            return Ok(new { success = true, data = roots });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取根节点失败");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// 获取子节点列表（懒加载）
    /// </summary>
    [HttpGet("{parentId}/children")]
    public async Task<IActionResult> GetChildren(
        string parentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var children = await treeService.GetChildrenAsync(parentId, cancellationToken);
            return Ok(new { success = true, data = children });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取子节点失败, ParentId: {ParentId}", parentId);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// 查询节点
    /// </summary>
    [HttpGet("query")]
    public async Task<IActionResult> Query(
        [FromQuery] TreeQueryParams queryParams,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var nodes = await treeService.QueryNodesAsync(queryParams, cancellationToken);
            return Ok(new { success = true, data = nodes, total = nodes.Count });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "查询节点失败");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// 获取完整树
    /// </summary>
    [HttpGet("full")]
    public async Task<IActionResult> GetFullTree(
        [FromQuery] string? rootId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tree = await treeService.GetFullTreeAsync(rootId, cancellationToken);
            return Ok(new { success = true, data = tree });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取完整树失败");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// 创建节点
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] TreeNodeDto<T> node,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var created = await treeService.CreateNodeAsync(node, cancellationToken);
            return Ok(new { success = true, data = created });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "创建节点失败");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// 更新节点
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string id,
        [FromBody] TreeNodeDto<T> node,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (id != node.Id)
            {
                return BadRequest(new { success = false, message = "ID不匹配" });
            }

            var updated = await treeService.UpdateNodeAsync(node, cancellationToken);
            return Ok(new { success = true, data = updated });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, message = $"节点 {id} 不存在" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "更新节点失败, Id: {Id}", id);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// 删除节点
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await treeService.DeleteNodeAsync(id, cancellationToken);
            if (!result)
            {
                return NotFound(new { success = false, message = $"节点 {id} 不存在" });
            }
            return Ok(new { success = true, message = "删除成功" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "删除节点失败, Id: {Id}", id);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// 移动节点
    /// </summary>
    [HttpPatch("{id}/move")]
    public async Task<IActionResult> Move(
        string id,
        [FromBody] MoveNodeRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await treeService.MoveNodeAsync(id, request.NewParentId, cancellationToken);
            if (!result)
            {
                return BadRequest(new { success = false, message = "移动失败，请检查是否移动到自身子节点下" });
            }
            return Ok(new { success = true, message = "移动成功" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "移动节点失败, Id: {Id}", id);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }
}