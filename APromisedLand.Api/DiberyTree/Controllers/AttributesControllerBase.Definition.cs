using APromisedLand.Api.DiberyTree.Interface;
using APromisedLand.Api.DiberyTree.Services;
using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using APromisedLand.Shared.DiberyTree.Attributes.Enums;
using APromisedLand.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace APromisedLand.Api.DiberyTree.Controllers;

/// <summary>
/// 属性定义控制器基类。从 <see cref="AttributesControllerBase"/> 分离——
/// 属性定义（schema）不耦合具体树，作为全局资源独立路由（definitions/types）。
/// <para>属性值端点（依赖 nodeId）仍保留在 <see cref="AttributesControllerBase"/>。</para>
/// <para>派生类只需继承并配置路由前缀，例如 <c>AttributesController : AttributeControllerBase</c>。</para>
/// </summary>
public abstract partial class AttributesControllerBase
{
    // ==================== 属性定义 ====================

    [HttpPost("definitions")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> CreateDefinition([FromBody] AttributeDefinitionCreateDto dto)
    {
        try
        {
            var created = await attributeService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetDefinitionById), new { id = created.Id }, ApiResponse<AttributeDefinitionDto>.Ok(created));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "创建属性定义失败");
            return StatusCode(500, ApiResponse<object>.Fail($"创建属性定义时发生错误: {ex.Message}"));
        }
    }

    [HttpGet("definitions/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> GetDefinitionById(string id)
    {
        try
        {
            var dto = await attributeService.GetByIdAsync(id);
            if (dto is null)
                return NotFound(ApiResponse<object>.Fail($"属性定义 {id} 不存在"));
            return Ok(ApiResponse<AttributeDefinitionDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取属性定义失败, Id: {Id}", id);
            return StatusCode(500, ApiResponse<object>.Fail($"获取属性定义失败: {ex.Message}"));
        }
    }

    [HttpGet("definitions")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> GetAllDefinitions()
    {
        try
        {
            var list = await attributeService.GetAllAsync();
            return Ok(ApiResponse<IReadOnlyList<AttributeDefinitionDto>>.Ok(list));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取属性定义列表失败");
            return StatusCode(500, ApiResponse<IReadOnlyList<AttributeDefinitionDto>>.Fail($"获取属性定义列表失败: {ex.Message}"));
        }
    }

    [HttpPut("definitions/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> UpdateDefinition(string id, [FromBody] AttributeDefinitionUpdateDto dto)
    {
        try
        {
            var updated = await attributeService.UpdateAsync(id, dto);
            return Ok(ApiResponse<AttributeDefinitionDto>.Ok(updated));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Fail($"属性定义 {id} 不存在"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "更新属性定义失败, Id: {Id}", id);
            return StatusCode(500, ApiResponse<object>.Fail($"更新属性定义失败: {ex.Message}"));
        }
    }

    [HttpDelete("definitions/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> DeleteDefinition(string id)
    {
        try
        {
            var result = await attributeService.DeleteAsync(id);
            if (!result)
                return NotFound(ApiResponse<object>.Fail($"属性定义 {id} 不存在"));
            return Ok(ApiResponse<bool>.Ok(true));
        }
        catch (InvalidOperationException ex) // 被节点值引用
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "删除属性定义失败, Id: {Id}", id);
            return StatusCode(500, ApiResponse<object>.Fail($"删除属性定义失败: {ex.Message}"));
        }
    }
}
