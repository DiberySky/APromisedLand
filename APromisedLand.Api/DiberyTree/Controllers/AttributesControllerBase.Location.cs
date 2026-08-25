using APromisedLand.Api.DiberyTree.Services;
using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using APromisedLand.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace APromisedLand.Api.DiberyTree.Controllers;

/// <summary>
/// 定位属性值控制器基类。仿 <see cref="AttributesControllerBase"/> 分层模式。
/// <para>负责 LocationAttributeValue 的 CRUD，独立路由。</para>
/// <para>派生类只需继承并配置路由前缀，例如 <c>AttributeLocationValueController : AttributeLocationValueControllerBase</c>。</para>
/// </summary>
public abstract partial class AttributesControllerBase
{
    // ==================== 定位值 CRUD ====================
//            $"{BasePath}/locations/node/{Uri.EscapeDataString(nodeId)}/attribute/{Uri.EscapeDataString(attributeId)}/location/{Uri.EscapeDataString(locationId)}");

    [HttpGet("locations/{locationId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<AttributeLocationDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> GetLocation(string locationId)
    {
        // var traceId = HttpContext.TraceIdentifier;
        try
        {
            var dto = await attributeService.GetLocationAsync(locationId);
            if (dto is null)
                return NotFound(ApiResponse<object>.Fail($"定位值 '{locationId}' 不存在"));
            
            return Ok(ApiResponse<AttributeLocationDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取定位值失败, LocationId: {LocationId}", locationId);
            return StatusCode(500, ApiResponse<object>.Fail($"获取定位值失败: {ex.Message}"));
        }
    }
    
    [HttpPut("locations/{locationId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<AttributeLocationDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> UpdateLocation(string locationId, [FromBody] AttributeLocationDto dto)
    {
        var traceId = HttpContext.TraceIdentifier;
        try
        {
            var error = await attributeService.UpdateLocationAsync(locationId, dto);
            if (!string.IsNullOrEmpty(error))
                return BadRequest(ApiResponse<object>.Fail(error));

            var result = await attributeService.GetLocationAsync(locationId);
            return Ok(ApiResponse<AttributeLocationDto>.Ok(result!));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[TraceId: {TraceId}] 更新定位值失败, ValueId: {ValueId}", traceId, locationId);
            return StatusCode(500, ApiResponse<object>.Fail($"更新定位值失败: {ex.Message}"));
        }
    }
    
    [HttpPost("node/{nodeId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<AttributeLocationValueDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> Add(string nodeId, [FromBody] AddAttributeLocationValueDto dto)
    {
        var traceId = HttpContext.TraceIdentifier;
        try
        {
            logger.LogInformation("[TraceId: {TraceId}] 新增定位值, NodeId: {NodeId}, DefId: {DefId}", traceId, nodeId, dto.AttributeDefinitionId);

            var (valueId, error, duplicated) = await attributeService.AddAsync(nodeId, dto);

            if (duplicated)
                logger.LogWarning("[TraceId: {TraceId}] 检测到重复 Add 定位值请求，直接复用上次结果。ValueId: {ValueId}", traceId, valueId);
            else
                logger.LogInformation("[TraceId: {TraceId}] 新增定位值成功, ValueId: {ValueId}", traceId, valueId);

            if (!string.IsNullOrEmpty(error))
                return BadRequest(ApiResponse<object>.Fail(error));

            var result = await attributeService.GetAsync(valueId!);
            return Ok(ApiResponse<AttributeLocationValueDto>.Ok(result!));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[TraceId: {TraceId}] 新增定位值失败, NodeId: {NodeId}", traceId, nodeId);
            return StatusCode(500, ApiResponse<object>.Fail($"新增定位值失败: {ex.Message}"));
        }
    }

    [HttpGet("node/{nodeId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<AttributeLocationValueDto>>))]
    public virtual async Task<IActionResult> ListByNode(string nodeId)
    {
        var traceId = HttpContext.TraceIdentifier;
        try
        {
            var list = await attributeService.ListByNodeAsync(nodeId);
            return Ok(ApiResponse<List<AttributeLocationValueDto>>.Ok(list));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[TraceId: {TraceId}] 查询定位值失败, NodeId: {NodeId}", traceId, nodeId);
            return StatusCode(500, ApiResponse<List<AttributeLocationValueDto>>.Fail($"查询定位值失败: {ex.Message}"));
        }
    }

    [HttpGet("{valueId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<AttributeLocationValueDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> Get(string valueId)
    {
        var traceId = HttpContext.TraceIdentifier;
        try
        {
            var dto = await attributeService.GetAsync(valueId);
            if (dto is null)
                return NotFound(ApiResponse<object>.Fail($"定位值 '{valueId}' 不存在"));
            return Ok(ApiResponse<AttributeLocationValueDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[TraceId: {TraceId}] 获取定位值失败, ValueId: {ValueId}", traceId, valueId);
            return StatusCode(500, ApiResponse<object>.Fail($"获取定位值失败: {ex.Message}"));
        }
    }

    [HttpPut("{valueId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<AttributeLocationValueDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> Update(string valueId, [FromBody] UpdateAttributeLocationValueDto dto)
    {
        var traceId = HttpContext.TraceIdentifier;
        try
        {
            var error = await attributeService.UpdateAsync(valueId, dto);
            if (!string.IsNullOrEmpty(error))
                return BadRequest(ApiResponse<object>.Fail(error));

            var result = await attributeService.GetAsync(valueId);
            return Ok(ApiResponse<AttributeLocationValueDto>.Ok(result!));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[TraceId: {TraceId}] 更新定位值失败, ValueId: {ValueId}", traceId, valueId);
            return StatusCode(500, ApiResponse<object>.Fail($"更新定位值失败: {ex.Message}"));
        }
    }

    [HttpDelete("{valueId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<bool>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> Delete(string valueId)
    {
        var traceId = HttpContext.TraceIdentifier;
        try
        {
            var deleted = await attributeService.DeleteAsync(valueId);
            if (!deleted)
                return NotFound(ApiResponse<object>.Fail($"定位值 '{valueId}' 不存在"));
            return Ok(ApiResponse<bool>.Ok(true));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[TraceId: {TraceId}] 删除定位值失败, ValueId: {ValueId}", traceId, valueId);
            return StatusCode(500, ApiResponse<object>.Fail($"删除定位值失败: {ex.Message}"));
        }
    }
}
