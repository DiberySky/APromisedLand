using System.Globalization;
using APromisedLand.Api.Data;
using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using APromisedLand.Shared.DiberyTree.Attributes.Models;
using APromisedLand.Shared.DiberyTree.Attributes.Validation;
using APromisedLand.Shared.DiberyTree.Models;
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

    [HttpGet("definitions/{id}")]
    public async Task<IActionResult> GetById(string id)
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
        // 1. 仅查询定义（无需 Include）
        var def = await db.AttributeDefinitions
            .FirstOrDefaultAsync(d => d.Id == dto.AttributeDefinitionId);
        if (def is null) return NotFound("属性定义不存在");

        // 2. 验证并构建值实体
        var validation = ValueValidator.ValidateAndBuild(def, dto.Value, nodeId);
        if (!validation.IsValid)
            return BadRequest(validation.ErrorMessage);

        var entity = validation.ValueEntity!;

        // 3. 添加到对应 DbSet
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
            default: return BadRequest("不支持的值类型");
        }

        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetSingleValue), new { nodeId, id = entity.Id }, null);
    }

    [HttpGet("values/{nodeId}/{id}")]
    public async Task<IActionResult> GetSingleValue(string nodeId, string id)
    {
        var value = await FindValueAsync(nodeId, id);
        if (value is null) return NotFound();

        // 单独获取定义及关联信息
        var (def, attrType, unit) = await GetDefinitionWithTypeAndUnitAsync(value.AttributeDefinitionId);
        var dto = MapToDto(value, def, attrType, unit);
        return Ok(dto);
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

    [HttpDelete("values/{nodeId}/{id}")]
    public async Task<IActionResult> DeleteValue(string nodeId, string id)
    {
        var value = await FindValueAsync(nodeId, id);
        if (value is null) return NotFound();

        db.Remove(value);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- 私有辅助方法 ----------

    /// <summary>在多个表中查找值实体（按节点 + ID）</summary>
    private async Task<AttributeValueBase?> FindValueAsync(string nodeId, string id)
    {
        // 使用 FirstOrDefault 替代 Any+First，减少往返
        var tasks = new Func<Task<AttributeValueBase?>>[]
        {
            async () => await db.TextAttributeValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
            async () => await db.DecimalAttributeValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
            async () => await db.IntegerAttributeValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
            async () => await db.DateAttributeValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
            async () => await db.TimeAttributeValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
            async () => await db.DateTimeAttributeValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
            async () => await db.FileAttributeValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
            async () => await db.LocationAttributeValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id)
        };

        foreach (var task in tasks)
        {
            var result = await task();
            if (result != null) return result;
        }
        return null;
    }

    /// <summary>查询定义 + 类型 + 单位（无导航）</summary>
    private async Task<(AttributeDefinition Definition, AttributeType? Type, UnitTree? Unit)> GetDefinitionWithTypeAndUnitAsync(string definitionId)
    {
        var query = db.AttributeDefinitions
            .Where(d => d.Id == definitionId)
            .Join(db.AttributeTypes, d => d.AttributeTypeId, t => t.Id, (d, t) => new { d, t })
            .GroupJoin(db.UnitTrees, x => x.d.UnitId, u => u.Id, (x, units) => new { x.d, x.t, units })
            .SelectMany(x => x.units.DefaultIfEmpty(), (x, u) => new { x.d, x.t, u })
            .FirstOrDefaultAsync();

        var result = await query;
        if (result == null) return (null!, null, null);
        return (result.d, result.t, result.u);
    }

    /// <summary>查询指定节点下某类型的所有值，并组装为 DTO</summary>
    private async Task<List<AttributeDto>> QueryValues<T>(string nodeId) where T : AttributeValueBase
    {
        var values = await db.Set<T>()
            .Where(v => v.NodeId == nodeId)
            .ToListAsync();

        if (values.Count == 0) return new List<AttributeDto>();

        // 收集所有定义ID
        var defIds = values.Select(v => v.AttributeDefinitionId).Distinct().ToList();

        // 一次性查询所有定义、类型、单位
        var defInfos = await db.AttributeDefinitions
            .Where(d => defIds.Contains(d.Id))
            .Join(db.AttributeTypes, d => d.AttributeTypeId, t => t.Id, (d, t) => new { d, t })
            .GroupJoin(db.UnitTrees, x => x.d.UnitId, u => u.Id, (x, units) => new { x.d, x.t, units })
            .SelectMany(x => x.units.DefaultIfEmpty(), (x, u) => new { x.d, x.t, u })
            .ToDictionaryAsync(
                x => x.d.Id,
                x => (Definition: x.d, AttributeType: x.t, Unit: x.u)
            );

        return values
            .Select(v =>
            {
                defInfos.TryGetValue(v.AttributeDefinitionId, out var info);
                return MapToDto(v, info.Definition, info.AttributeType, info.Unit);
            })
            .ToList();
    }

    /// <summary>将值实体映射为 DTO（传入定义及相关信息）</summary>
    private static AttributeDto MapToDto(
        AttributeValueBase v,
        AttributeDefinition? def,
        AttributeType? attrType,
        UnitTree? unit)
    {
        return new AttributeDto
        {
            Id = v.Id,                               // 新增，便于前端操作
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