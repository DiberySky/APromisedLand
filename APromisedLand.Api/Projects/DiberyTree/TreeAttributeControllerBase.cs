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
public partial class TreeAttributeControllerBase : ControllerBase
{
    private readonly DiberyDbContext _db;

    public TreeAttributeControllerBase(DiberyDbContext db) => _db = db;

    // ---------- 属性定义 ----------
    [HttpPost("definitions")]
    public async Task<IActionResult> Create(AttributeDefinition definition)
    {
        _db.AttributeDefinitions.Add(definition);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = definition.Id }, definition);
    }

    [HttpGet("definitions/{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var def = await _db.AttributeDefinitions.FindAsync(id);
        return def is null ? NotFound() : Ok(def);
    }

    [HttpGet("definitions")]
    public async Task<IActionResult> GetAll()
        => Ok(await _db.AttributeDefinitions.ToListAsync());

    // ---------- 属性值 ----------
    [HttpPost("values/{nodeId}")]
    public async Task<IActionResult> AddValue(string nodeId, AddValueDto dto)
    {
        // 可选：校验节点是否存在
        // if (!await _db.Nodes.AnyAsync(n => n.Id == nodeId)) return NotFound("节点不存在");

        var def = await _db.AttributeDefinitions
            .Include(d => d.AttributeType)
            .FirstOrDefaultAsync(d => d.Id == dto.AttributeDefinitionId);
        if (def is null) return NotFound("属性定义不存在");

        var validation = ValueValidator.ValidateAndBuild(def, dto.Value, nodeId);
        if (!validation.IsValid)
            return BadRequest(validation.ErrorMessage);

        var entity = validation.ValueEntity!;
        switch (entity)
        {
            case TextAttributeValue tv: _db.TextAttributeValues.Add(tv); break;
            case DecimalAttributeValue dv: _db.DecimalAttributeValues.Add(dv); break;
            case IntegerAttributeValue iv: _db.IntegerAttributeValues.Add(iv); break;
            case DateAttributeValue dav: _db.DateAttributeValues.Add(dav); break;
            case TimeAttributeValue tav: _db.TimeAttributeValues.Add(tav); break;
            case DateTimeAttributeValue dtav: _db.DateTimeAttributeValues.Add(dtav); break;
            case FileAttributeValue fav: _db.FileAttributeValues.Add(fav); break;
            case LocationAttributeValue lav: _db.LocationAttributeValues.Add(lav); break;
        }

        await _db.SaveChangesAsync();
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

        _db.Remove(value);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- 私有辅助方法 ----------
    private async Task<AttributeValueBase?> FindValueAsync(string nodeId, int id)
    {
        if (await _db.TextAttributeValues.AnyAsync(v => v.NodeId == nodeId && v.Id == id))
            return await _db.TextAttributeValues.FirstAsync(v => v.NodeId == nodeId && v.Id == id);

        if (await _db.DecimalAttributeValues.AnyAsync(v => v.NodeId == nodeId && v.Id == id))
            return await _db.DecimalAttributeValues.FirstAsync(v => v.NodeId == nodeId && v.Id == id);

        if (await _db.IntegerAttributeValues.AnyAsync(v => v.NodeId == nodeId && v.Id == id))
            return await _db.IntegerAttributeValues.FirstAsync(v => v.NodeId == nodeId && v.Id == id);

        if (await _db.DateAttributeValues.AnyAsync(v => v.NodeId == nodeId && v.Id == id))
            return await _db.DateAttributeValues.FirstAsync(v => v.NodeId == nodeId && v.Id == id);

        if (await _db.TimeAttributeValues.AnyAsync(v => v.NodeId == nodeId && v.Id == id))
            return await _db.TimeAttributeValues.FirstAsync(v => v.NodeId == nodeId && v.Id == id);

        if (await _db.DateTimeAttributeValues.AnyAsync(v => v.NodeId == nodeId && v.Id == id))
            return await _db.DateTimeAttributeValues.FirstAsync(v => v.NodeId == nodeId && v.Id == id);

        if (await _db.FileAttributeValues.AnyAsync(v => v.NodeId == nodeId && v.Id == id))
            return await _db.FileAttributeValues.FirstAsync(v => v.NodeId == nodeId && v.Id == id);

        if (await _db.LocationAttributeValues.AnyAsync(v => v.NodeId == nodeId && v.Id == id))
            return await _db.LocationAttributeValues.FirstAsync(v => v.NodeId == nodeId && v.Id == id);

        return null;
    }

    private async Task<List<AttributeDto>> QueryValues<T>(string nodeId) where T : AttributeValueBase
    {
        var set = _db.Set<T>();
        var items = await set
            .Include(v => v.Definition)
                .ThenInclude(d => d.AttributeType)
            .Include(v => v.Definition)
                .ThenInclude(d => d.UnitOfMeasure)
            .Where(v => v.NodeId == nodeId)
            .ToListAsync();
        return items.Select(MapToDto).ToList();
    }

    private static AttributeDto MapToDto(AttributeValueBase v)
    {
        var def = v.Definition;
        var attrType = def?.AttributeType;
        var unit = def?.UnitOfMeasure;

        return new AttributeDto
        {
            DefinitionId = v.AttributeDefinitionId,
            DefinitionName = def?.Name ?? "未知定义",
            Type = attrType?.Name ?? "未知类型",
            TypeDescription = attrType?.Description,
            Unit = unit?.Symbol ?? unit?.Name,
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