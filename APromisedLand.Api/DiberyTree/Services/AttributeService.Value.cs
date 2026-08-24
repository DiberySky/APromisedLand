using System.Text.Json;
using APromisedLand.Api.Data;
using APromisedLand.Api.DiberyTree.Interface;
using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using APromisedLand.Shared.DiberyTree.Attributes.Enums;
using APromisedLand.Shared.DiberyTree.Attributes.Models;
using APromisedLand.Shared.DiberyTree.Attributes.Validation;
using APromisedLand.Shared.DiberyTree.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace APromisedLand.Api.DiberyTree.Services;

public partial class AttributeService
{
    // ---------- 属性值操作 ----------
    public async Task<AttributeValueBase> AddValueAsync(string nodeId, AddValueDto dto)
    {
        // 1. 获取定义
        var definition = await db.AttributeDefinitions
            .FirstOrDefaultAsync(d => d.Id == dto.AttributeDefinitionId);
        if (definition == null)
            throw new KeyNotFoundException($"属性定义 '{dto.AttributeDefinitionId}' 不存在。");

        // 2. 从映射获取枚举类型
        if (!AttributeTypeMapping.IdToEnum.TryGetValue(definition.AttributeTypeId, out var attrType))
            throw new InvalidOperationException($"无法识别属性类型ID: {definition.AttributeTypeId}");

        // 3. 验证并构建值实体（传入枚举类型）
        var validation = ValueValidator.ValidateAndBuild(definition, dto.Value, nodeId);
        if (!validation.IsValid)
            throw new ArgumentException(validation.ErrorMessage);

        var entity = validation.ValueEntity!;
        entity.Id = Guid.NewGuid().ToString();

        // 4. 处理日期时间类型（UTC转换）
        if (entity is DateTimeAttributeValue dt)
        {
            var original = dt.Value;
            dt.Value = new DateTimeOffset(original.UtcDateTime, TimeSpan.Zero);
            logger.LogInformation(
                "DateTimeAttributeValue 时区转换: 原始 {Original} (Offset={Offset}) -> 转换后 {Converted} (Offset=00:00)",
                original, original.Offset, dt.Value);
        }
        else if (entity is DateAttributeValue d)
        {
            var original = d.Value;
            d.Value = new DateTimeOffset(original.UtcDateTime, TimeSpan.Zero);
            logger.LogInformation(
                "DateAttributeValue 时区转换: 原始 {Original} (Offset={Offset}) -> 转换后 {Converted} (Offset=00:00)",
                original, original.Offset, d.Value);
        }

        // 5. 保存
        switch (entity)
        {
            case TextAttributeValue tv: await db.TextAttributeValues.AddAsync(tv); break;
            case DecimalAttributeValue dv: await db.DecimalAttributeValues.AddAsync(dv); break;
            case IntegerAttributeValue iv: await db.IntegerAttributeValues.AddAsync(iv); break;
            case DateAttributeValue dav: await db.DateAttributeValues.AddAsync(dav); break;
            case TimeAttributeValue tav: await db.TimeAttributeValues.AddAsync(tav); break;
            case DateTimeAttributeValue dtav: await db.DateTimeAttributeValues.AddAsync(dtav); break;
            case FileAttributeValue fav: await db.FileAttributeValues.AddAsync(fav); break;
            case LocationAttributeValue lav: await db.LocationAttributeValues.AddAsync(lav); break;
            case TableAttributeDefValue tv: await db.TableAttributeValues.AddAsync(tv); break;
            default: throw new NotSupportedException($"不支持的值类型 '{entity.GetType().Name}'。");
        }

        await db.SaveChangesAsync();
        return entity;
    }

    public async Task<AttributeDto?> GetValueAsync(string nodeId, string id)
    {
        var value = await FindValueAsync(nodeId, id);
        if (value == null) return null;

        var info = await GetDefinitionWithTypeAndUnitAsync(value.AttributeDefinitionId);
        if (info == null) return null;

        return MapToDto(value, info.Definition, info.AttributeType, info.Unit);
    }

    public async Task<NodeDto> GetAllValuesAsync(string nodeId)
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
        list.AddRange(await QueryValues<TableAttributeDefValue>(nodeId));
        return new NodeDto { Id = nodeId, AttributeDtos = list };
    }

    public async Task<bool> DeleteValueAsync(string nodeId, string id)
    {
        var value = await FindValueAsync(nodeId, id);
        if (value == null) return false;
        db.Remove(value);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task UpdateValueAsync(string nodeId, string id, JsonElement value, CancellationToken cancellationToken)
    {
        // 1. 查找现有值
        var existing = await FindValueAsync(nodeId, id);
        if (existing == null)
            throw new KeyNotFoundException($"属性值 '{id}' 在节点 '{nodeId}' 中不存在。");

        // 2. 获取定义和类型枚举
        var def = await db.AttributeDefinitions
            .FirstOrDefaultAsync(d => d.Id == existing.AttributeDefinitionId, cancellationToken);
        if (def == null)
            throw new KeyNotFoundException($"属性定义 '{existing.AttributeDefinitionId}' 不存在。");

        if (!AttributeTypeMapping.IdToEnum.TryGetValue(def.AttributeTypeId, out var attrType))
            throw new InvalidOperationException($"无法识别属性类型ID: {def.AttributeTypeId}");

        // 3. 验证新值
        var validation = ValueValidator.ValidateAndBuild(def, value, nodeId);
        if (!validation.IsValid)
            throw new ArgumentException(validation.ErrorMessage);

        var newEntity = validation.ValueEntity!;

        // 4. 复制值到现有实体
        CopyValue(existing, newEntity);

        // 5. 处理日期时间类型（UTC转换）
        if (existing is DateTimeAttributeValue dt)
        {
            var original = dt.Value;
            dt.Value = new DateTimeOffset(original.UtcDateTime, TimeSpan.Zero);
            logger.LogInformation(
                "DateTimeAttributeValue 更新时区转换: 原始 {Original} (Offset={Offset}) -> 转换后 {Converted} (Offset=00:00)",
                original, original.Offset, dt.Value);
        }
        else if (existing is DateAttributeValue d)
        {
            var original = d.Value;
            d.Value = new DateTimeOffset(original.UtcDateTime, TimeSpan.Zero);
            logger.LogInformation(
                "DateAttributeValue 更新时区转换: 原始 {Original} (Offset={Offset}) -> 转换后 {Converted} (Offset=00:00)",
                original, original.Offset, d.Value);
        }

        // 6. 保存
        await db.SaveChangesAsync(cancellationToken);
    }

    // ---------- 私有辅助方法 ----------
    private async Task<AttributeValueBase?> FindValueAsync(string nodeId, string id)
    {
        var tasks = new Func<Task<AttributeValueBase?>>[]
        {
            async () => await db.TextAttributeValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
            async () =>
                await db.DecimalAttributeValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
            async () =>
                await db.IntegerAttributeValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
            async () => await db.DateAttributeValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
            async () => await db.TimeAttributeValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
            async () => await db.DateTimeAttributeValues.FirstOrDefaultAsync(v =>
                v.NodeId == nodeId && v.Id == id),
            async () => await db.FileAttributeValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
            async () => await db.LocationAttributeValues.FirstOrDefaultAsync(v =>
                v.NodeId == nodeId && v.Id == id),
            async () => await db.TableAttributeValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
        };
        foreach (var task in tasks)
        {
            var result = await task();
            if (result != null) return result;
        }

        return null;
    }

    private async Task<DefinitionWithTypeAndUnit?> GetDefinitionWithTypeAndUnitAsync(string definitionId)
    {
        var def = await db.AttributeDefinitions
            .FirstOrDefaultAsync(d => d.Id == definitionId);
        if (def == null) return null;

        if (!AttributeTypeMapping.IdToEnum.TryGetValue(def.AttributeTypeId, out var attrType))
            return null;

        UnitTree? unit = null;
        if (!string.IsNullOrEmpty(def.UnitId))
            unit = await db.UnitTrees.FindAsync(def.UnitId);

        return new DefinitionWithTypeAndUnit
        {
            Definition = def,
            AttributeType = attrType,
            Unit = unit
        };
    }

    private async Task<List<AttributeDto>> QueryValues<TValue>(string nodeId) where TValue : AttributeValueBase
    {
        var items = await db.Set<TValue>().Where(v => v.NodeId == nodeId).ToListAsync();
        if (items.Count == 0) return new List<AttributeDto>();

        var result = new List<AttributeDto>();
        foreach (var v in items)
        {
            var info = await GetDefinitionWithTypeAndUnitAsync(v.AttributeDefinitionId);
            if (info != null)
                result.Add(MapToDto(v, info.Definition, info.AttributeType, info.Unit));
        }

        return result;
    }

    private static AttributeDto MapToDto(AttributeValueBase v, AttributeDefinition? def, AttributeTypeEnum attrType,
        UnitTree? unit)
    {
        // 如果 def 不为空，可设置一个未映射的属性（若实体中有）
        // 但此处不需要，因为 DTO 已包含枚举

        object? rawValue = v switch
        {
            TextAttributeValue tv => tv.Value,
            DecimalAttributeValue dv => dv.Value,
            IntegerAttributeValue iv => iv.Value,
            DateAttributeValue dateV => dateV.Value,
            TimeAttributeValue timeV => timeV.Value,
            DateTimeAttributeValue dtV => dtV.Value,
            FileAttributeValue fv => fv.Value,
            LocationAttributeValue lv => new { lv.Latitude, lv.Longitude },
            TableAttributeDefValue tv => tv.Value,
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

    private static void CopyValue(AttributeValueBase target, AttributeValueBase source)
    {
        if (target.GetType() != source.GetType())
            throw new InvalidOperationException("目标类型与源类型不匹配。");

        switch (target)
        {
            case TextAttributeValue tTarget:
                var tSource = (TextAttributeValue)source;
                tTarget.Value = tSource.Value;
                break;
            case DecimalAttributeValue dTarget:
                var dSource = (DecimalAttributeValue)source;
                dTarget.Value = dSource.Value;
                break;
            case IntegerAttributeValue iTarget:
                var iSource = (IntegerAttributeValue)source;
                iTarget.Value = iSource.Value;
                break;
            case DateAttributeValue dateTarget:
                var dateSource = (DateAttributeValue)source;
                dateTarget.Value = dateSource.Value;
                break;
            case TimeAttributeValue timeTarget:
                var timeSource = (TimeAttributeValue)source;
                timeTarget.Value = timeSource.Value;
                break;
            case DateTimeAttributeValue dtTarget:
                var dtSource = (DateTimeAttributeValue)source;
                dtTarget.Value = dtSource.Value;
                break;
            case FileAttributeValue fTarget:
                var fSource = (FileAttributeValue)source;
                fTarget.Value = fSource.Value;
                break;
            case LocationAttributeValue lTarget:
                var lSource = (LocationAttributeValue)source;
                lTarget.Latitude = lSource.Latitude;
                lTarget.Longitude = lSource.Longitude;
                break;
            case TableAttributeDefValue tvTarget:
                var tvSource = (TableAttributeDefValue)source;
                tvTarget.Value = tvSource.Value;
                break;
            default:
                throw new NotSupportedException($"不支持的值类型 '{target.GetType().Name}'。");
        }
    }

    private sealed class DefinitionWithTypeAndUnit
    {
        public AttributeDefinition Definition { get; set; } = null!;
        public AttributeTypeEnum AttributeType { get; set; }
        public UnitTree? Unit { get; set; }
    }
}