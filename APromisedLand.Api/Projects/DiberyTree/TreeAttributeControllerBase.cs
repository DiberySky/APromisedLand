using System.Globalization;
using System.Text.Json;
using APromisedLand.Api.Data;
using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using APromisedLand.Shared.DiberyTree.Attributes.Enums;
using APromisedLand.Shared.DiberyTree.Attributes.Models;
using APromisedLand.Shared.DiberyTree.Attributes.Validation;
using APromisedLand.Shared.DiberyTree.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APromisedLand.Api.Projects.DiberyTree;

[ApiController]
[Route("TreeAttribute")]
public class TreeAttributeControllerBase : ControllerBase
{
    private readonly DiberyDbContext _db;

    public TreeAttributeControllerBase(DiberyDbContext db)
    {
        _db = db;
    }

    // ---------- 属性定义 ----------
    [HttpPost("definitions")]
    public async Task<IActionResult> Create(AttributeDefinition definition)
    {
        _db.AttributeDefinitions.Add(definition);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = definition.Id }, definition);
    }

    [HttpGet("definitions/{id}")]
    public async Task<IActionResult> GetById(string id)
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
        // 查询定义并加载类型
        var defWithType = await _db.AttributeDefinitions
            .Join(_db.AttributeTypes, d => d.AttributeTypeId, t => t.Id, (d, t) => new { d, t })
            .Where(x => x.d.Id == dto.AttributeDefinitionId)
            .Select(x => new { Definition = x.d, AttributeType = x.t })
            .FirstOrDefaultAsync();

        if (defWithType == null)
            return NotFound("属性定义不存在");

        var def = defWithType.Definition;
        def.AttributeType = defWithType.AttributeType;

        // 从 JsonElement 中提取原始值
        // object? rawValue = null;
        // var systemType = defWithType.AttributeType.SystemType;
        // if (dto.Value.ValueKind != JsonValueKind.Null && dto.Value.ValueKind != JsonValueKind.Undefined)
        // {
        //     rawValue = systemType switch
        //     {
        //         AttributeTypeEnum.文本 => dto.Value.GetString(),
        //         AttributeTypeEnum.整数 => dto.Value.GetInt64(),
        //         AttributeTypeEnum.小数 => dto.Value.GetDecimal(),
        //         AttributeTypeEnum.日期 => dto.Value.GetDateTime(),
        //         AttributeTypeEnum.时间 => dto.Value.GetDateTime(),
        //         AttributeTypeEnum.日期时间 => dto.Value.GetDateTime(),
        //         AttributeTypeEnum.文件 => dto.Value.GetString(),
        //         AttributeTypeEnum.定位 => dto.Value.GetString(),
        //         _ => dto.Value.GetString()
        //     };
        // }
        // var valueString = rawValue?.ToString() ?? string.Empty;

        var validation = ValueValidator.ValidateAndBuild(def, dto.Value, nodeId);
        if (!validation.IsValid)
            return BadRequest(validation.ErrorMessage);

        var entity = validation.ValueEntity!;
        entity.Id = Guid.NewGuid().ToString();

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
            default: return BadRequest("不支持的值类型");
        }

        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetSingleValue), new { nodeId, id = entity.Id }, null);
    }

    [HttpGet("values/{nodeId}/{id}")]
    public async Task<IActionResult> GetSingleValue(string nodeId, string id)
    {
        var value = await FindValueAsync(nodeId, id);
        if (value is null) return NotFound();

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

    [HttpPut("values/{nodeId}/{id}")]
    public async Task<IActionResult> UpdateValue(string nodeId, string id, [FromBody] UpdateValueDto dto)
    {
        // 查找现有值
        var existing = await FindValueAsync(nodeId, id);
        if (existing == null)
            return NotFound($"属性值 {id} 不存在或不属于节点 {nodeId}");

        // 获取定义和类型
        var (def, attrType, _) = await GetDefinitionWithTypeAndUnitAsync(existing.AttributeDefinitionId);
        if (def == null || attrType == null)
            return NotFound("属性定义不存在");

        // 从 JsonElement 提取新值
        // object? rawValue = null;
        // var systemType = attrType.SystemType;
        // if (dto.Value.ValueKind != JsonValueKind.Null && dto.Value.ValueKind != JsonValueKind.Undefined)
        // {
        //     rawValue = systemType switch
        //     {
        //         AttributeTypeEnum.文本 => dto.Value.GetString(),
        //         AttributeTypeEnum.整数 => dto.Value.GetInt64(),
        //         AttributeTypeEnum.小数 => dto.Value.GetDecimal(),
        //         AttributeTypeEnum.日期 => dto.Value.GetDateTime(),
        //         AttributeTypeEnum.时间 => dto.Value.GetDateTime(),
        //         AttributeTypeEnum.日期时间 => dto.Value.GetDateTime(),
        //         AttributeTypeEnum.文件 => dto.Value.GetString(),
        //         AttributeTypeEnum.定位 => dto.Value.GetString(),
        //         _ => dto.Value.GetString()
        //     };
        // }
        // var valueString = rawValue?.ToString() ?? string.Empty;

        // 验证新值（复用验证器）
        def.AttributeType = attrType;
        var validation = ValueValidator.ValidateAndBuild(def, dto.Value, nodeId);
        if (!validation.IsValid)
            return BadRequest(validation.ErrorMessage);

        var newEntity = validation.ValueEntity!;
        // 复制 ID 和节点信息
        newEntity.Id = existing.Id;
        newEntity.NodeId = existing.NodeId;
        newEntity.AttributeDefinitionId = existing.AttributeDefinitionId;

        // 替换实体
        _db.Entry(existing).State = EntityState.Detached;
        switch (newEntity)
        {
            case TextAttributeValue tv: _db.TextAttributeValues.Update(tv); break;
            case DecimalAttributeValue dv: _db.DecimalAttributeValues.Update(dv); break;
            case IntegerAttributeValue iv: _db.IntegerAttributeValues.Update(iv); break;
            case DateAttributeValue dav: _db.DateAttributeValues.Update(dav); break;
            case TimeAttributeValue tav: _db.TimeAttributeValues.Update(tav); break;
            case DateTimeAttributeValue dtav: _db.DateTimeAttributeValues.Update(dtav); break;
            case FileAttributeValue fav: _db.FileAttributeValues.Update(fav); break;
            case LocationAttributeValue lav: _db.LocationAttributeValues.Update(lav); break;
            default: return BadRequest("不支持的值类型");
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("values/{nodeId}/{id}")]
    public async Task<IActionResult> DeleteValue(string nodeId, string id)
    {
        var value = await FindValueAsync(nodeId, id);
        if (value is null) return NotFound();

        _db.Remove(value);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- 私有辅助方法 ----------
    private async Task<AttributeValueBase?> FindValueAsync(string nodeId, string id)
    {
        var tasks = new Func<Task<AttributeValueBase?>>[]
        {
            async () => await _db.TextAttributeValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
            async () => await _db.DecimalAttributeValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
            async () => await _db.IntegerAttributeValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
            async () => await _db.DateAttributeValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
            async () => await _db.TimeAttributeValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
            async () => await _db.DateTimeAttributeValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
            async () => await _db.FileAttributeValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
            async () => await _db.LocationAttributeValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id)
        };
        foreach (var task in tasks)
        {
            var result = await task();
            if (result != null) return result;
        }
        return null;
    }

    private async Task<(AttributeDefinition Definition, AttributeType? Type, UnitTree? Unit)> GetDefinitionWithTypeAndUnitAsync(string definitionId)
    {
        var query = _db.AttributeDefinitions
            .Where(d => d.Id == definitionId)
            .Join(_db.AttributeTypes, d => d.AttributeTypeId, t => t.Id, (d, t) => new { d, t })
            .GroupJoin(_db.UnitTrees, x => x.d.UnitId, u => u.Id, (x, units) => new { x.d, x.t, units })
            .SelectMany(x => x.units.DefaultIfEmpty(), (x, u) => new { x.d, x.t, u })
            .FirstOrDefaultAsync();
        var result = await query;
        if (result == null) return (null!, null, null);
        return (result.d, result.t, result.u);
    }

    private async Task<List<AttributeDto>> QueryValues<T>(string nodeId) where T : AttributeValueBase
    {
        var values = await _db.Set<T>().Where(v => v.NodeId == nodeId).ToListAsync();
        if (values.Count == 0) return new List<AttributeDto>();

        var defIds = values.Select(v => v.AttributeDefinitionId).Distinct().ToList();
        var defInfos = await _db.AttributeDefinitions
            .Where(d => defIds.Contains(d.Id))
            .Join(_db.AttributeTypes, d => d.AttributeTypeId, t => t.Id, (d, t) => new { d, t })
            .GroupJoin(_db.UnitTrees, x => x.d.UnitId, u => u.Id, (x, units) => new { x.d, x.t, units })
            .SelectMany(x => x.units.DefaultIfEmpty(), (x, u) => new { x.d, x.t, u })
            .ToDictionaryAsync(x => x.d.Id, x => (Definition: x.d, AttributeType: x.t, Unit: x.u));

        return values
            .Select(v =>
            {
                defInfos.TryGetValue(v.AttributeDefinitionId, out var info);
                return MapToDto(v, info.Definition, info.AttributeType, info.Unit);
            })
            .ToList();
    }

    private static AttributeDto MapToDto(AttributeValueBase v, AttributeDefinition? def, AttributeType? attrType, UnitTree? unit)
    {
        // 确保定义包含导航属性（供前端使用）
        if (def != null)
        {
            def.AttributeType = attrType;
            def.Unit = unit;
        }

        // 将原始值转为 JsonElement
        object? rawValue = v switch
        {
            TextAttributeValue tv => tv.Value,
            DecimalAttributeValue dv => dv.Value,
            IntegerAttributeValue iv => iv.Value,
            DateAttributeValue dateV => dateV.Value,
            TimeAttributeValue timeV => timeV.Value,
            DateTimeAttributeValue dtV => dtV.Value,
            FileAttributeValue fv => fv.Value,
            LocationAttributeValue lv => new { lv.Latitude, lv.Longitude }, // 或字符串，按需
            _ => null
        };

        var jsonElement = JsonSerializer.SerializeToElement(rawValue, new JsonSerializerOptions
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        });

        return new AttributeDto
        {
            Id = v.Id,
            DefinitionId = v.AttributeDefinitionId,
            Definition = def ?? new AttributeDefinition(),
            Value = jsonElement
        };
    }
}