using System.Globalization;
using APromisedLand.Api.Data;
using APromisedLand.Api.Projects.DiberyTree.Interface;
using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using APromisedLand.Shared.DiberyTree.Attributes.Models;
using APromisedLand.Shared.DiberyTree.Attributes.Validation;
using APromisedLand.Shared.DiberyTree.Models;
using APromisedLand.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace APromisedLand.Api.Projects.DiberyTree;

/// <summary>
/// 泛型树控制器基类，支持任意节点值类型 T，并包含属性定义与值的操作。
/// </summary>
/// <typeparam name="T">节点值的类型</typeparam>
[ApiController]
[Route("[controller]")]
public abstract class TreeControllerBase<T>(
    ITreeService<T> treeService,
    ITreeAttributeService attributeService,
    // DiberyDbContext db,
    ILogger<TreeControllerBase<T>> logger)
    : ControllerBase
{
    protected readonly ITreeService<T> TreeService = treeService;
    // protected readonly DiberyDbContext Db = db;
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
            return BadRequest($"获取根节点失败: {ex.Message}");
        }
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
            Logger.LogError(ex, "创建节点失败");
            return StatusCode(500, "创建节点时发生错误");
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
        catch (Exception ex)
        {
            Logger.LogError(ex, "更新节点子项失败");
            return StatusCode(500, "更新节点子项时发生错误");
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
            Logger.LogError(ex, "更新节点失败");
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

    // ==================== 属性定义 (路由前缀: attributes/definitions) ====================

    [HttpPost("attributes/definitions")]
    public async Task<IActionResult> CreateDefinition([FromBody] AttributeDefinitionCreateDto dto)
    {
        try
        {
            var created = await attributeService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetDefinitionById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "创建属性定义失败");
            return StatusCode(500, new { success = false, message = "创建属性定义时发生错误。" });
        }
    }

    [HttpGet("attributes/definitions/{id}")]
    public async Task<IActionResult> GetDefinitionById(string id)
    {
        try
        {
            var dto = await attributeService.GetByIdAsync(id);
            if (dto is null)
                return NotFound(new { success = false, message = $"属性定义 {id} 不存在" });
            return Ok(new { success = true, data = dto });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取属性定义失败, Id: {Id}", id);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpGet("attributes/definitions")]
    public async Task<IActionResult> GetAllDefinitions()
    {
        try
        {
            var list = await attributeService.GetAllAsync();
            // return Ok(new { success = true, data = list });
            return Ok(ApiResponse<IReadOnlyList<AttributeDefinitionDto>>.Ok(list));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取属性定义列表失败");
            return StatusCode(500, ApiResponse<IReadOnlyList<AttributeDefinitionDto>>.Fail($"获取属性定义列表失败: {ex.Message}"));
        }
    }

    [HttpGet("attributes/types")]
    public async Task<IActionResult> GetAttributeTypesAsync()
    {
        try
        {
            var list = await attributeService.GetAttributeTypesAsync();
            return Ok(ApiResponse<IReadOnlyList<AttributeType>>.Ok(list));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取属性类型列表失败");
            return StatusCode(500, ApiResponse<IReadOnlyList<AttributeType>>.Fail($"获取属性类型列表失败: {ex.Message}"));
        }
    }

    [HttpPut("attributes/definitions/{id}")]
    public async Task<IActionResult> UpdateDefinition(string id, [FromBody] AttributeDefinitionUpdateDto dto)
    {
        try
        {
            var updated = await attributeService.UpdateAsync(id, dto);
            return Ok(new { success = true, data = updated });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, message = $"属性定义 {id} 不存在" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "更新属性定义失败, Id: {Id}", id);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpDelete("attributes/definitions/{id}")]
    public async Task<IActionResult> DeleteDefinition(string id)
    {
        try
        {
            var result = await attributeService.DeleteAsync(id);
            if (!result)
                return NotFound(new { success = false, message = $"属性定义 {id} 不存在" });
            return Ok(new { success = true, message = "删除成功" });
        }
        catch (InvalidOperationException ex) // 被节点值引用
        {
            return Conflict(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "删除属性定义失败, Id: {Id}", id);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    // ==================== 属性值 (路由前缀: {nodeId}/attributes/values) ====================

    [HttpPost("{nodeId}/attributes/values")]
    public async Task<IActionResult> AddValue(string nodeId, [FromBody] AddValueDto dto)
    {
        try
        {
            var result = await attributeService.AddValueAsync(nodeId, dto);
            return CreatedAtAction(nameof(GetSingleValue), new { nodeId, id = result.Id }, null);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "添加节点属性值失败, NodeId: {NodeId}", nodeId);
            return StatusCode(500, new { success = false, message = "添加属性值时发生错误。" });
        }
    }

    [HttpGet("{nodeId}/attributes/values/{id:int}")]
    public async Task<IActionResult> GetSingleValue(string nodeId, int id)
    {
        try
        {
            var dto = await attributeService.GetValueAsync(nodeId, id);
            if (dto is null)
                return NotFound(new { success = false, message = $"属性值 {id} 不存在或不属于节点 {nodeId}" });
            return Ok(new { success = true, data = dto });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取属性值失败, NodeId: {NodeId}, ValueId: {ValueId}", nodeId, id);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpGet("{nodeId}/attributes/values")]
    public async Task<IActionResult> GetAllValues(string nodeId)
    {
        try
        {
            var nodeDto = await attributeService.GetAllValuesAsync(nodeId);
            return Ok(new { success = true, data = nodeDto });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取节点所有属性值失败, NodeId: {NodeId}", nodeId);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpDelete("{nodeId}/attributes/values/{id:int}")]
    public async Task<IActionResult> DeleteValue(string nodeId, int id)
    {
        try
        {
            var deleted = await attributeService.DeleteValueAsync(nodeId, id);
            if (!deleted)
                return NotFound(new { success = false, message = $"属性值 {id} 不存在或不属于节点 {nodeId}" });
            return Ok(new { success = true, message = "删除成功" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "删除属性值失败, NodeId: {NodeId}, ValueId: {ValueId}", nodeId, id);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }
}