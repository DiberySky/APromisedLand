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
[ApiController]
[Route("[controller]")]
public abstract class AttributeTableValueControllerBase(
    AttributeTableValueService valueService,
    ILogger<AttributeTableValueControllerBase> logger) : ControllerBase
{
    // ==================== 表行数据 ====================

    [HttpPost("node/{nodeId}/table/{tableId}/definition/{tableDefId}/rows")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> AddRow(string nodeId, string tableId, string tableDefId,
        [FromBody] AddTableRowDto dto)
    {
        try
        {
            logger.LogInformation("新增表行, NodeId: {NodeId}, TableId: {TableId}, TableDefId: {TableDefId}", nodeId, tableId, tableDefId);
            var (rowId, error) = await valueService.AddRowAsync(nodeId, tableId, tableDefId, dto.Values);
            logger.LogInformation("新增表行成功, RowId: {RowId}", rowId);
            
            if (!string.IsNullOrEmpty(error))
                return BadRequest(ApiResponse<object>.Fail(error));
            
            // 返回新创建的行数据
            var row = await valueService.GetRowAsync(rowId!);
            return Ok(ApiResponse<TableRowDto>.Ok(row!));
            return CreatedAtAction(nameof(GetRow), new { rowId }, ApiResponse<TableRowDto>.Ok(row!));

            // var row = await valueService.AddRowAsync(nodeId, tableId, tableDefId, dto.Values);
            // return CreatedAtAction(nameof(GetRow), new { rowId = row.RowId }, ApiResponse<bool>.Ok(true));
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
            logger.LogError(ex, "新增表行失败, TableId: {Id}", tableId);
            return StatusCode(500, ApiResponse<object>.Fail($"新增表行失败: {ex.Message}"));
        }
    }

    [HttpGet("{tableId}/rows")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> ListRows(string tableId)
    {
        try
        {
            var rows = await valueService.ListRowsAsync(tableId);
            return Ok(ApiResponse<List<TableRowDto>>.Ok(rows));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "查询表行失败, TableId: {Id}", tableId);
            return StatusCode(500, ApiResponse<List<TableRowDto>>.Fail($"查询表行失败: {ex.Message}"));
        }
    }

    [HttpGet("rows/{rowId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> GetRow(string rowId)
    {
        try
        {
            var row = await valueService.GetRowAsync(rowId);
            if (row is null)
                return NotFound(ApiResponse<object>.Fail($"行 '{rowId}' 不存在"));
            return Ok(ApiResponse<TableRowDto>.Ok(row));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取表行失败, RowId: {Id}", rowId);
            return StatusCode(500, ApiResponse<object>.Fail($"获取表行失败: {ex.Message}"));
        }
    }

    [HttpPut("rows/{rowId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> UpdateRow(string rowId, [FromBody] UpdateTableRowDto dto)
    {
        try
        {
            var row = await valueService.UpdateRowAsync(rowId, dto.Values);
            return Ok(ApiResponse<bool>.Ok(true));
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
            logger.LogError(ex, "更新表行失败, RowId: {Id}", rowId);
            return StatusCode(500, ApiResponse<object>.Fail($"更新表行失败: {ex.Message}"));
        }
    }

    [HttpDelete("rows/{rowId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public virtual async Task<IActionResult> DeleteRow(string rowId)
    {
        try
        {
            var result = await valueService.DeleteRowAsync(rowId);
            if (result is null)
                return NotFound(ApiResponse<object>.Fail($"行 '{rowId}' 不存在"));
            return Ok(ApiResponse<bool>.Ok(true));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "删除表行失败, RowId: {Id}", rowId);
            return StatusCode(500, ApiResponse<object>.Fail($"删除表行失败: {ex.Message}"));
        }
    }
}
