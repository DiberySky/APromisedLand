using APromisedLand.Api.DiberyTree.Services;
using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using APromisedLand.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace APromisedLand.Api.DiberyTree.Controllers;

/// <summary>
/// 动态表数据控制器基类。负责行实例与列值的 CRUD。
/// <para>与 <see cref="AttributesControllerBase"/> 分工：后者管「定义」（schema），本类管「数据」（行实例+列值）。</para>
/// <para>派生类只需继承并配置路由前缀，例如 <c>TableValuesController : AttributeTableValueControllerBase</c>。</para>
/// </summary>
public abstract partial class AttributesControllerBase
{
    // ==================== 表行数据 ====================

    [HttpPost("tables/{tableId}/rows")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<TableRowDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> AddRow(
        string tableId,
        [FromBody] AddTableRowDto dto)
    {
        var traceId = HttpContext.TraceIdentifier;
        try
        {
            logger.LogInformation(
                "[TraceId: {TraceId}] 新增表行, NodeId: {NodeId}, TableId: {TableId}, TableDefId: {TableDefId}",
                traceId, dto.NodeId, tableId, dto.DefinitionId);

            var (rowId, error, duplicated) = await attributeService.AddRowAsync(tableId, dto);

            if (duplicated)
            {
                logger.LogWarning(
                    "[TraceId: {TraceId}] 检测到重复 AddRow 请求，直接复用上次结果。RowId: {RowId}, Error: {Error}",
                    traceId, rowId, error);
            }
            else
            {
                logger.LogInformation(
                    "[TraceId: {TraceId}] 新增表行成功, RowId: {RowId}",
                    traceId, rowId);
            }

            if (!string.IsNullOrEmpty(error))
                return BadRequest(ApiResponse<object>.Fail(error));

            // 返回新创建的行数据
            var row = await attributeService.GetRowAsync(rowId!);
            return Ok(ApiResponse<TableRowDto>.Ok(row!));
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogWarning("[TraceId: {TraceId}] 新增表行未找到资源: {Msg}", traceId, ex.Message);
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning("[TraceId: {TraceId}] 新增表行参数错误: {Msg}", traceId, ex.Message);
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[TraceId: {TraceId}] 新增表行失败, TableId: {Id}", traceId, tableId);
            return StatusCode(500, ApiResponse<object>.Fail($"新增表行失败: {ex.Message}"));
        }
    }

    [HttpGet("tables/{tableId}/rows")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> ListRows(string tableId)
    {
        var traceId = HttpContext.TraceIdentifier;
        try
        {
            var rows = await attributeService.ListRowsAsync(tableId);
            return Ok(ApiResponse<List<TableRowDto>>.Ok(rows));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[TraceId: {TraceId}] 查询表行失败, TableId: {Id}", traceId, tableId);
            return StatusCode(500, ApiResponse<List<TableRowDto>>.Fail($"查询表行失败: {ex.Message}"));
        }
    }

    [HttpGet("tables/rows/{rowId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> GetRow(string rowId)
    {
        var traceId = HttpContext.TraceIdentifier;
        try
        {
            var row = await attributeService.GetRowAsync(rowId);
            if (row is null)
                return NotFound(ApiResponse<object>.Fail($"行 '{rowId}' 不存在"));
            return Ok(ApiResponse<TableRowDto>.Ok(row));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[TraceId: {TraceId}] 获取表行失败, RowId: {Id}", traceId, rowId);
            return StatusCode(500, ApiResponse<object>.Fail($"获取表行失败: {ex.Message}"));
        }
    }

    [HttpPut("tables/rows/{rowId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<TableRowDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> UpdateRow(string rowId, [FromBody] UpdateTableRowDto dto)
    {
        var traceId = HttpContext.TraceIdentifier;
        try
        {
            var error = await attributeService.UpdateRowAsync(rowId, dto);
            if (!string.IsNullOrEmpty(error))
                return BadRequest(ApiResponse<object>.Fail(error));

            // 返回更新后的完整行数据（与客户端期望 ApiResponse<TableRowDto> 一致）
            var row = await attributeService.GetRowAsync(rowId);
            return Ok(ApiResponse<TableRowDto>.Ok(row!));
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogWarning("[TraceId: {TraceId}] 更新表行未找到资源: {Msg}", traceId, ex.Message);
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning("[TraceId: {TraceId}] 更新表行参数错误: {Msg}", traceId, ex.Message);
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[TraceId: {TraceId}] 更新表行失败, RowId: {Id}", traceId, rowId);
            return StatusCode(500, ApiResponse<object>.Fail($"更新表行失败: {ex.InnerException?.ToString() ?? ex.Message}"));
        }
    }

    [HttpDelete("tables/rows/{rowId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<string>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<string>))]
    public virtual async Task<IActionResult> DeleteRow(string rowId)
    {
        var traceId = HttpContext.TraceIdentifier;
        try
        {
            var result = await attributeService.DeleteRowAsync(rowId);
            // if (result is false)
            //     return NotFound(ApiResponse<bool>.Fail($"行 '{rowId}' 不存在"));
            
            return Ok(ApiResponse<string>.Ok("删除成功"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[TraceId: {TraceId}] 删除表行失败, RowId: {Id}", traceId, rowId);
            return StatusCode(500, ApiResponse<string>.Fail($"删除表行失败: {ex.Message}"));
        }
    }
}
