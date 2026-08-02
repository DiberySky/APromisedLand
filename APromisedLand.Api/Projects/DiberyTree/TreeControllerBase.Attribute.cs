using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace APromisedLand.Api.Projects.DiberyTree;

public partial class TreeControllerBase<T>
{
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
            return Ok(new { success = true, data = list });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取属性定义列表失败");
            return StatusCode(500, new { success = false, message = ex.Message });
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