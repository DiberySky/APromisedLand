using APromisedLand.Api.Projects.DiberyTree.Interface;
using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using APromisedLand.Shared.DiberyTree.Models;
using APromisedLand.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace APromisedLand.Api.Projects.DiberyTree.Controllers;

/// <summary>
/// 泛型树控制器基类，支持任意节点值类型 T，并包含属性值的操作（属性定义已分离至 <see cref="T"/>）。
/// </summary>
/// <typeparam name="T">节点值的类型</typeparam>
[ApiController]
[Route("[controller]")]
public abstract class TreeControllerBase<T>(
    ITreeService<T> treeService,
    ITreeAttributeService attributeService,
    ILogger<TreeControllerBase<T>> logger)
    : ControllerBase
{
    protected readonly ITreeService<T> TreeService = treeService;
    protected readonly ILogger Logger = logger;

    // ==================== 树节点操作 ====================

    [HttpGet("roots")]
    [HttpGet("roots/{rootId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> GetRoots(string? rootId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var roots = await TreeService.GetRootNodesAsync(rootId, cancellationToken);
            return Ok(ApiResponse<IReadOnlyList<TreeNodeDto<T>>>.Ok(roots));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "获取根节点失败");
            return BadRequest(ApiResponse<List<TreeNodeDto<T>>>.Fail($"获取根节点失败: {ex.Message}"));
        }
    }

    [HttpGet("children/{parentId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> GetChildren(string parentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var children = await TreeService.GetChildrenAsync(parentId, cancellationToken);
            return Ok(ApiResponse<IReadOnlyList<TreeNodeDto<T>>>.Ok(children));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "获取子节点失败");
            return BadRequest(ApiResponse<List<TreeNodeDto<T>>>.Fail($"获取子节点失败: {ex.Message}"));
        }
    }

    [HttpPost("query")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> Query([FromBody] TreeQueryParams queryParams, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await TreeService.QueryNodesAsync(queryParams, cancellationToken);
            return Ok(ApiResponse<object>.Ok(result));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "查询节点失败");
            return BadRequest(ApiResponse<object>.Fail($"查询节点失败: {ex.Message}"));
        }
    }

    [HttpGet("full")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> GetFullTree([FromQuery] string? rootId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var tree = await TreeService.GetFullTreeAsync(rootId, cancellationToken);
            if (tree == null)
                return NotFound(ApiResponse<object>.Fail($"根节点 '{rootId ?? "默认"}' 不存在"));
            return Ok(ApiResponse<TreeNodeDto<T>>.Ok(tree));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "获取完整树失败");
            return StatusCode(500, ApiResponse<object>.Fail($"获取完整树失败: {ex.Message}"));
        }
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> Create([FromBody] TreeNodeDto<T> node, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(node.Text))
            return BadRequest(ApiResponse<object>.Fail("节点文本不能为空"));

        try
        {
            var created = await TreeService.CreateNodeAsync(node, cancellationToken);
            return CreatedAtAction(nameof(GetFullTree), new { rootId = created.Id }, ApiResponse<TreeNodeDto<T>>.Ok(created));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "创建节点失败");
            return StatusCode(500, ApiResponse<object>.Fail($"创建节点时发生错误: {ex.Message}"));
        }
    }

    [HttpPost("children")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> UpdateChildren([FromBody] TreeNodeDto<T> nodeDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var updated = await TreeService.UpdateChildrenAsync(nodeDto, cancellationToken);
            return Ok(ApiResponse<TreeNodeDto<T>>.Ok(updated));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "更新节点子项失败");
            return StatusCode(500, ApiResponse<object>.Fail($"更新节点子项时发生错误: {ex.Message}"));
        }
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> Update(string id, [FromBody] TreeNodeDto<T> node, CancellationToken cancellationToken = default)
    {
        if (id != node.Id)
            return BadRequest(ApiResponse<object>.Fail("URL 中的 ID 与请求体中的 ID 不一致"));

        if (string.IsNullOrWhiteSpace(node.Text))
            return BadRequest(ApiResponse<object>.Fail("节点文本不能为空"));

        try
        {
            var updated = await TreeService.UpdateNodeAsync(node, cancellationToken);
            return Ok(ApiResponse<TreeNodeDto<T>>.Ok(updated));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Fail($"节点 '{id}' 不存在"));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "更新节点失败");
            return StatusCode(500, ApiResponse<object>.Fail($"更新节点时发生错误: {ex.Message}"));
        }
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> Delete(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await TreeService.DeleteNodeAsync(id, cancellationToken);
            if (!result)
                return NotFound(ApiResponse<object>.Fail($"节点 '{id}' 不存在或删除失败"));
            return Ok(ApiResponse<bool>.Ok(true));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "删除节点失败");
            return StatusCode(500, ApiResponse<object>.Fail($"删除节点时发生错误: {ex.Message}"));
        }
    }

    [HttpPost("move")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> Move([FromQuery] string nodeId, [FromQuery] string? newParentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return BadRequest(ApiResponse<object>.Fail("节点 ID 不能为空"));

        if (!string.IsNullOrEmpty(newParentId))
        {
            try
            {
                var parentExists = await TreeService.GetFullTreeAsync(newParentId, cancellationToken) != null;
                if (!parentExists)
                    return NotFound(ApiResponse<object>.Fail($"目标父节点 '{newParentId}' 不存在"));
            }
            catch (Exception ex)
            {
                return NotFound(ApiResponse<object>.Fail($"检查父节点存在性失败: {ex.Message}"));
            }
        }

        try
        {
            var result = await TreeService.MoveNodeAsync(nodeId, newParentId, cancellationToken);
            if (!result)
                return BadRequest(ApiResponse<object>.Fail("移动失败，可能节点不存在或试图移动到自身子节点下"));
            return Ok(ApiResponse<bool>.Ok(true));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "移动节点失败");
            return StatusCode(500, ApiResponse<object>.Fail($"移动节点时发生错误: {ex.Message}"));
        }
    }

    [HttpGet("{nodeId}/ancestors")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> GetAncestorPath(string nodeId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return BadRequest(ApiResponse<object>.Fail("节点 ID 不能为空"));

        try
        {
            var path = await TreeService.GetAncestorPathAsync(nodeId, cancellationToken);
            if (path.Count == 0)
                return NotFound(ApiResponse<object>.Fail($"节点 '{nodeId}' 不存在"));
            return Ok(ApiResponse<IReadOnlyList<string>>.Ok(path));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "获取祖先路径失败");
            return StatusCode(500, ApiResponse<object>.Fail($"获取祖先路径失败: {ex.Message}"));
        }
    }

    // 属性定义端点已分离至 AttributesControllerBase（属性定义不耦合具体树，可独立路由）

    // ==================== 属性值 (路由前缀: {nodeId}/attributes/values) ====================

    [HttpPost("{nodeId}/attributes/values")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> AddValue(string nodeId, [FromBody] AddValueDto dto)
    {
        try
        {
            var result = await attributeService.AddValueAsync(nodeId, dto);
            return CreatedAtAction(nameof(GetSingleValue), new { nodeId, id = result.Id }, ApiResponse<object>.Ok(result, "属性值添加成功"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "添加节点属性值失败, NodeId: {NodeId}", nodeId);
            return StatusCode(500, ApiResponse<object>.Fail($"添加属性值时发生错误: {ex.Message}"));
        }
    }

    // 修改：移除 :int 约束，因为 Id 已改为 string
    [HttpGet("{nodeId}/attributes/values/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> GetSingleValue(string nodeId, string id)
    {
        try
        {
            var dto = await attributeService.GetValueAsync(nodeId, id);
            if (dto is null)
                return NotFound(ApiResponse<object>.Fail($"属性值 {id} 不存在或不属于节点 {nodeId}"));
            return Ok(ApiResponse<AttributeDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取属性值失败, NodeId: {NodeId}, ValueId: {ValueId}", nodeId, id);
            return StatusCode(500, ApiResponse<object>.Fail($"获取属性值失败: {ex.Message}"));
        }
    }

    [HttpGet("{nodeId}/attributes/values")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> GetAllValues(string nodeId)
    {
        try
        {
            var nodeDto = await attributeService.GetAllValuesAsync(nodeId);
            return Ok(ApiResponse<NodeDto>.Ok(nodeDto));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取节点所有属性值失败, NodeId: {NodeId}", nodeId);
            return StatusCode(500, ApiResponse<object>.Fail($"获取节点所有属性值失败: {ex.Message}"));
        }
    }

    [HttpPut("{nodeId}/attributes/values/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> UpdateValue(string nodeId, string id, [FromBody] UpdateValueDto valueDto, CancellationToken cancellationToken = default)
    {
        try
        {
            await attributeService.UpdateValueAsync(nodeId, id, valueDto.Value, cancellationToken);
            return Ok(ApiResponse<object>.Ok(valueDto, "属性值更新成功"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "更新属性值失败, NodeId: {NodeId}, ValueId: {ValueId}", nodeId, id);
            return StatusCode(500, ApiResponse<object>.Fail($"更新属性值失败: {ex.Message}"));
        }
    }
    
    [HttpDelete("{nodeId}/attributes/values/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> DeleteValue(string nodeId, string id)
    {
        try
        {
            var deleted = await attributeService.DeleteValueAsync(nodeId, id);
            if (!deleted)
                return NotFound(ApiResponse<object>.Fail($"属性值 {id} 不存在或不属于节点 {nodeId}"));
            return Ok(ApiResponse<bool>.Ok(true));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "删除属性值失败, NodeId: {NodeId}, ValueId: {ValueId}", nodeId, id);
            return StatusCode(500, ApiResponse<object>.Fail($"删除属性值失败: {ex.Message}"));
        }
    }
}