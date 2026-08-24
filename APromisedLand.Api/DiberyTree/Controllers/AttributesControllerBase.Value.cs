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
/// 属性定义控制器基类。从 <see cref="TreeControllerBase{T}"/> 分离——
/// 属性定义（schema）不耦合具体树，作为全局资源独立路由（definitions/types）。
/// <para>属性值端点（依赖 nodeId）仍保留在 <see cref="TreeControllerBase{T}"/>。</para>
/// <para>派生类只需继承并配置路由前缀，例如 <c>AttributesController : AttributeControllerBase</c>。</para>
/// </summary>
public abstract partial class AttributesControllerBase : ControllerBase
{
    
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
