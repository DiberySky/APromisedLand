using APromisedLand.Api.DiberyTree.Services;
using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using APromisedLand.Shared.DiberyTree.Attributes.Enums;
using APromisedLand.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace APromisedLand.Api.DiberyTree.Controllers;

/// <summary>
/// 属性定义控制器基类。从 <see cref="TreeControllerBase{T}"/> 分离——
/// 属性定义（schema）不耦合具体树，作为全局资源独立路由（definitions/types）。
/// <para>属性值端点（依赖 nodeId）仍保留在 <see cref="TreeControllerBase{T}"/>。</para>
/// <para>派生类只需继承并配置路由前缀，例如 <c>AttributesController : AttributeControllerBase</c>。</para>
/// </summary>
[ApiController]
[Route("[controller]")]
public abstract class AttributesControllerBase(
    AttributeDefinitionService attributeService,
    ILogger<AttributesControllerBase> logger) : ControllerBase
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

    // [HttpGet("types")]
    // [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    // public virtual async Task<IActionResult> GetAttributeTypesAsync()
    // {
    //     try
    //     {
    //         var list = await attributeService.GetAttributeTypesAsync();
    //         return Ok(ApiResponse<IReadOnlyList<AttributeType>>.Ok(list));
    //     }
    //     catch (Exception ex)
    //     {
    //         logger.LogError(ex, "获取属性类型列表失败");
    //         return StatusCode(500, ApiResponse<IReadOnlyList<AttributeType>>.Fail($"获取属性类型列表失败: {ex.Message}"));
    //     }
    // }

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

    // ==================== 动态表：表定义列表 ====================

    /// <summary>获取所有「表格」类型的表定义（ParentId=null 且 AttributeType=表格）。</summary>
    [HttpGet("tables")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> ListTables()
    {
        try
        {
            var list = await attributeService.ListTablesAsync();
            return Ok(ApiResponse<List<AttributeDefinitionDto>>.Ok(list));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取表定义列表失败");
            return StatusCode(500, ApiResponse<List<AttributeDefinitionDto>>.Fail($"获取表定义列表失败: {ex.Message}"));
        }
    }

    /// <summary>获取指定表下的所有列定义，按 Order 排序（服务端过滤 ParentId）。</summary>
    [HttpGet("tables/{tableId}/columns")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> ListTableColumns(string tableId)
    {
        try
        {
            // 先确认该表存在且是表定义
            var table = await attributeService.GetByIdAsync(tableId);
            if (table is null || table.ParentId != null || table.AttributeType != AttributeTypeEnum.表格)
                return NotFound(ApiResponse<object>.Fail($"表定义 '{tableId}' 不存在或不是表格类型"));

            var list = await attributeService.ListColumnsAsync(tableId);
            return Ok(ApiResponse<List<AttributeDefinitionDto>>.Ok(list));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取表列列表失败, TableId: {TableId}", tableId);
            return StatusCode(500, ApiResponse<List<AttributeDefinitionDto>>.Fail($"获取表列列表失败: {ex.Message}"));
        }
    }

    /// <summary>在指定表下新建列定义（ParentId 由路由 tableId 确定，body 里传 ParentId 无效）。</summary>
    [HttpPost("tables/{tableId}/columns")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> CreateTableColumn(
        string tableId, [FromBody] AttributeDefinitionCreateDto dto)
    {
        try
        {
            var created = await attributeService.CreateTableColumnAsync(tableId, dto);
            return CreatedAtAction(nameof(GetDefinitionById), new { id = created.Id },
                ApiResponse<AttributeDefinitionDto>.Ok(created));
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
            logger.LogError(ex, "创建表列失败, TableId: {TableId}", tableId);
            return StatusCode(500, ApiResponse<object>.Fail($"创建表列失败: {ex.Message}"));
        }
    }
}
