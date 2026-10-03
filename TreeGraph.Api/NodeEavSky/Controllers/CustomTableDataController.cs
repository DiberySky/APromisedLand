using Microsoft.AspNetCore.Mvc;
using TreeGraph.Api.NodeEavSky.Services;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Api.NodeEavSky.Controllers;

[ApiController]
[Route("api/eav/{entityType}/entities/{entityId}/tables")]
public class CustomTableDataController : ControllerBase
{
    private readonly CustomTableReadService _read;
    private readonly CustomTableWriteService _write;
    private readonly IAttributeCache _attrCache;

    public CustomTableDataController(
        CustomTableReadService read, CustomTableWriteService write,
        IAttributeCache attrCache)
    {
        _read = read;
        _write = write;
        _attrCache = attrCache;
    }

    [HttpGet("{tableName}")]
    public async Task<IActionResult> Load(
        string entityType, string entityId, string tableName,
        CancellationToken ct)
    {
        var def = _attrCache.GetDefinition(entityType, tableName);
        if (def?.RefTableDefinitionId is null) return NotFound();

        var result = await _read.LoadAsync(
            entityId, entityType,
            def.AttributeId, def.RefTableDefinitionId, ct);
        return Ok(result);
    }

    [HttpPut("{tableName}")]
    public async Task<IActionResult> Replace(
        string entityType, string entityId, string tableName,
        [FromBody] CustomTableValue value, CancellationToken ct)
    {
        var def = _attrCache.GetDefinition(entityType, tableName);
        if (def?.RefTableDefinitionId is null) return NotFound();

        try
        {
            await _write.ReplaceAsync(
                entityId, entityType,
                def.AttributeId, def.RefTableDefinitionId, value, ct);
            return NoContent();
        }
        catch (EavValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
    }

    [HttpPut("{tableName}/rows")]
    public async Task<IActionResult> UpsertRow(
        string entityType, string entityId, string tableName,
        [FromBody] CustomTableRowValue rowValue, CancellationToken ct)
    {
        var def = _attrCache.GetDefinition(entityType, tableName);
        if (def?.RefTableDefinitionId is null) return NotFound();

        try
        {
            await _write.UpsertRowAsync(
                entityId, entityType,
                def.AttributeId, def.RefTableDefinitionId,
                rowValue, ct);
            return NoContent();
        }
        catch (EavValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>删除单行（★ 修复 P0-3：带归属校验；rowId 是 GUID 字符串）</summary>
    [HttpDelete("{tableName}/rows/{rowId}")]
    public async Task<IActionResult> DeleteRow(
        string entityType, string entityId, string tableName,
        string rowId, CancellationToken ct)
    {
        var def = _attrCache.GetDefinition(entityType, tableName);
        if (def?.RefTableDefinitionId is null) return NotFound();

        await _write.DeleteRowAsync(
            rowId, entityId, entityType, def.AttributeId, ct);
        return NoContent();
    }
}
