using System.Globalization;
using APromisedLand.Api.Data;
using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using APromisedLand.Shared.DiberyTree.Attributes.Models;
using APromisedLand.Shared.DiberyTree.Attributes.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APromisedLand.Api.Projects.DiberyTree;

[ApiController]
[Route("TreeAttribute")]
public class TreeAttributeControllerBase(DiberyDbContext db) : ControllerBase
{
    // ---------- 属性定义 ----------
    [HttpPost("definitions")]
    public async Task<IActionResult> Create(AttributeDefinition definition)
    {
        db.AttributeDefinitions.Add(definition);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = definition.Id }, definition);
    }

    [HttpGet("definitions/{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var def = await db.AttributeDefinitions.FindAsync(id);
        return def is null ? NotFound() : Ok(def);
    }

    [HttpGet("definitions")]
    public async Task<IActionResult> GetAll()
        => Ok(await db.AttributeDefinitions.ToListAsync());

    // ---------- 属性值 ----------
    [HttpPost("values/{nodeId}")]
    public async Task<IActionResult> AddValue(string nodeId, AddValueDto dto)
    {
        // 可选：校验节点是否存在
        // if (!await _db.Nodes.AnyAsync(n => n.Id == nodeId)) return NotFound("节点不存在");

        var def = await db.AttributeDefinitions
            .Include(d => d.AttributeType)
            .FirstOrDefaultAsync(d => d.Id == dto.AttributeDefinitionId);
        if (def is null) return NotFound("属性定义不存在");

        var validation = ValueValidator.ValidateAndBuild(def, dto.Value, nodeId);
        if (!validation.IsValid)
            return BadRequest(validation.ErrorMessage);

        var entity = validation.ValueEntity!;
        switch (entity)
        {
            case TextAttributeValue tv: db.TextAttributeValues.Add(tv); break;
            case DecimalAttributeValue dv: db.DecimalAttributeValues.Add(dv); break;
            case IntegerAttributeValue iv: db.IntegerAttributeValues.Add(iv); break;
            case DateAttributeValue dav: db.DateAttributeValues.Add(dav); break;
            case TimeAttributeValue tav: db.TimeAttributeValues.Add(tav); break;
            case DateTimeAttributeValue dtav: db.DateTimeAttributeValues.Add(dtav); break;
            case FileAttributeValue fav: db.FileAttributeValues.Add(fav); break;
            case LocationAttributeValue lav: db.LocationAttributeValues.Add(lav); break;
        }

        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetSingleValue), new { nodeId, id = entity.Id }, null);
    }

    [HttpGet("values/{nodeId}/{id:int}")]
    public async Task<IActionResult> GetSingleValue(string nodeId, int id)
    {
        var value = await FindValueAsync(nodeId, id);
        return value is null ? NotFound() : Ok(MapToDto(value));
    }

    [HttpGet("values/{nodeId}")]
    public async Task<IActionResult> GetAllValues(string nodeId)
    {
        var list = new List<AttributeDto>();
        list.AddRange(await QueryValues<TextAttributeValue>(nodeId));
        list.AddRange(await QueryValues<DecimalAttributeValue>(nodeId));
        list.AddRange(await QueryValues<IntegerAttributeValue>(nodeId));
        list.AddRange(await QueryValues<DateAttributeValue>(nodeId));
        list.AddRange(await QueryValues<TimeAttributeValue>(nodeId));
        list.AddRange(await QueryValues<DateTimeAttributeValue>(nodeId));
        list.AddRange(await QueryValues<FileAttributeValue>(nodeId));
        list.AddRange(await QueryValues<LocationAttributeValue>(nodeId));

        return Ok(new NodeDto { Id = nodeId, Attributes = list });
    }

    [HttpDelete("values/{nodeId}/{id:int}")]
    public async Task<IActionResult> DeleteValue(string nodeId, int id)
    {
        var value = await FindValueAsync(nodeId, id);
        if (value is null) return NotFound();

        db.Remove(value);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- 私有辅助方法 ----------
    private async Task<AttributeValueBase?> FindValueAsync(string nodeId, int id)
    {
        if (await db.TextAttributeValues.AnyAsync(v => v.NodeId == nodeId && v.Id == id))
            return await db.TextAttributeValues.FirstAsync(v => v.NodeId == nodeId && v.Id == id);

        if (await db.DecimalAttributeValues.AnyAsync(v => v.NodeId == nodeId && v.Id == id))
            return await db.DecimalAttributeValues.FirstAsync(v => v.NodeId == nodeId && v.Id == id);

        if (await db.IntegerAttributeValues.AnyAsync(v => v.NodeId == nodeId && v.Id == id))
            return await db.IntegerAttributeValues.FirstAsync(v => v.NodeId == nodeId && v.Id == id);

        if (await db.DateAttributeValues.AnyAsync(v => v.NodeId == nodeId && v.Id == id))
            return await db.DateAttributeValues.FirstAsync(v => v.NodeId == nodeId && v.Id == id);

        if (await db.TimeAttributeValues.AnyAsync(v => v.NodeId == nodeId && v.Id == id))
            return await db.TimeAttributeValues.FirstAsync(v => v.NodeId == nodeId && v.Id == id);

        if (await db.DateTimeAttributeValues.AnyAsync(v => v.NodeId == nodeId && v.Id == id))
            return await db.DateTimeAttributeValues.FirstAsync(v => v.NodeId == nodeId && v.Id == id);

        if (await db.FileAttributeValues.AnyAsync(v => v.NodeId == nodeId && v.Id == id))
            return await db.FileAttributeValues.FirstAsync(v => v.NodeId == nodeId && v.Id == id);

        if (await db.LocationAttributeValues.AnyAsync(v => v.NodeId == nodeId && v.Id == id))
            return await db.LocationAttributeValues.FirstAsync(v => v.NodeId == nodeId && v.Id == id);

        return null;
    }

    private async Task<List<AttributeDto>> QueryValues<T>(string nodeId) where T : AttributeValueBase
    {
        var set = db.Set<T>();
        var items = await set
            .Include(v => v.Definition)
                .ThenInclude(d => d.AttributeType)
            .Include(v => v.Definition)
                .ThenInclude(d => d.Unit)
            .Where(v => v.NodeId == nodeId)
            .ToListAsync();
        return items.Select(MapToDto).ToList();
    }

    private static AttributeDto MapToDto(AttributeValueBase v)
    {
        var def = v.Definition;
        var attrType = def?.AttributeType;
        var unit = def?.Unit;

        return new AttributeDto
        {
            DefinitionId = v.AttributeDefinitionId,
            DefinitionName = def?.Name ?? "未知定义",
            Type = attrType?.Name ?? "未知类型",
            TypeDescription = attrType?.Description,
            Unit = unit?.Abbreviation ?? unit?.Name,
            Lines = def?.Lines,
            Value = v switch
            {
                TextAttributeValue tv => tv.Value,
                DecimalAttributeValue dv => dv.Value.ToString(CultureInfo.InvariantCulture),
                IntegerAttributeValue iv => iv.Value.ToString(CultureInfo.InvariantCulture),
                DateAttributeValue dateV => dateV.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                TimeAttributeValue timeV => timeV.Value.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture),
                DateTimeAttributeValue dtV => dtV.Value.ToString("O", CultureInfo.InvariantCulture),
                FileAttributeValue fv => fv.Value,
                LocationAttributeValue lv => $"{lv.Latitude.ToString(CultureInfo.InvariantCulture)},{lv.Longitude.ToString(CultureInfo.InvariantCulture)}",
                _ => null
            }
        };
    }
}