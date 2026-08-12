using System.Globalization;
using APromisedLand.Api.Data;
using APromisedLand.Api.Projects.DiberyTree.Interface;
using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using APromisedLand.Shared.DiberyTree.Attributes.Models;
using APromisedLand.Shared.DiberyTree.Attributes.Validation;
using APromisedLand.Shared.DiberyTree.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace APromisedLand.Api.Projects.DiberyTree.Services;

public class TreeAttributeService(DiberyDbContext dbContext, ILogger<TreeAttributeService> logger) : ITreeAttributeService
{
    public async Task<IReadOnlyList<AttributeDefinitionDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // 使用 Lambda 表达式进行 Join 和 GroupJoin
        var query = dbContext.AttributeDefinitions
            .Join(
                dbContext.AttributeTypes,
                d => d.AttributeTypeId,
                t => t.Id,
                (d, t) => new { d, t }
            )
            .GroupJoin(
                dbContext.UnitTrees,
                x => x.d.UnitId,
                u => u.Id,
                (x, units) => new { x.d, x.t, units }
            )
            .SelectMany(
                x => x.units.DefaultIfEmpty(),
                (x, u) => new AttributeDefinitionDto
                {
                    Id = x.d.Id,
                    Name = x.d.Name,
                    AttributeType = x.t,
                    AttributeTypeName = x.t.Name,
                    MaxLength = x.d.MaxLength,
                    Lines = x.d.Lines,
                    Precision = x.d.Precision,
                    Scale = x.d.Scale,
                    Unit = u
                }
            );

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AttributeType>> GetAttributeTypesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.AttributeTypes
            .ToListAsync(cancellationToken);
    }

    public async Task<AttributeDefinitionDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var query = dbContext.AttributeDefinitions
            .Where(d => d.Id == id)
            .Join(
                dbContext.AttributeTypes,
                d => d.AttributeTypeId,
                t => t.Id,
                (d, t) => new { d, t }
            )
            .GroupJoin(
                dbContext.UnitTrees,
                x => x.d.UnitId,
                u => u.Id,
                (x, units) => new { x.d, x.t, units }
            )
            .SelectMany(
                x => x.units.DefaultIfEmpty(),
                (x, u) => new AttributeDefinitionDto
                {
                    Id = x.d.Id,
                    Name = x.d.Name,
                    AttributeType = x.t,
                    AttributeTypeName = x.t.Name,
                    MaxLength = x.d.MaxLength,
                    Lines = x.d.Lines,
                    Precision = x.d.Precision,
                    Scale = x.d.Scale,
                    Unit = u
                }
            );

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<AttributeDefinitionDto> CreateAsync(AttributeDefinitionCreateDto dto, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(">>> CreateAsync 被调用，时间：{Time}, 参数 Name={Name}, AttributeTypeId={TypeId}",
            DateTime.Now, dto.Name, dto.AttributeType?.Id);

        if (dto.AttributeType == null || string.IsNullOrEmpty(dto.AttributeType.Id))
            throw new ArgumentException("AttributeType is required.");

        // 验证 AttributeType 是否存在
        var attrTypeExists = await dbContext.AttributeTypes
            .AnyAsync(t => t.Id == dto.AttributeType.Id, cancellationToken);
        if (!attrTypeExists)
            throw new ArgumentException($"AttributeType with id {dto.AttributeType.Id} does not exist.");

        // 验证 Unit 是否存在（如果提供）
        if (!string.IsNullOrEmpty(dto.UnitId))
        {
            var unitExists = await dbContext.UnitTrees
                .AnyAsync(u => u.Id == dto.UnitId, cancellationToken);
            if (!unitExists)
                throw new ArgumentException($"Unit with id {dto.UnitId} does not exist.");
        }

        var entity = new AttributeDefinition
        {
            Id = Guid.NewGuid().ToString(),
            Name = dto.Name,
            AttributeTypeId = dto.AttributeType.Id,
            MaxLength = dto.MaxLength,
            Lines = dto.Lines,
            Precision = dto.Precision,
            Scale = dto.Scale,
            UnitId = dto.UnitId
        };

        await dbContext.AttributeDefinitions.AddAsync(entity, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(entity.Id, cancellationToken)
               ?? throw new Exception("Failed to retrieve created entity.");
    }

    public async Task<AttributeDefinitionDto> UpdateAsync(string id, AttributeDefinitionUpdateDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.AttributeDefinitions
            .FindAsync(new object[] { id }, cancellationToken);
        if (entity == null)
            throw new KeyNotFoundException($"AttributeDefinition with id {id} not found.");

        entity.Name = dto.Name;
        entity.Lines = dto.Lines;
        entity.Precision = dto.Precision;
        entity.Scale = dto.Scale;
        entity.UnitId = dto.UnitId;

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken)
               ?? throw new Exception("Failed to retrieve updated entity.");
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.AttributeDefinitions
            .FindAsync([id], cancellationToken);
        if (entity == null) return false;

        var hasReferences = await dbContext.TextAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, cancellationToken)
                            || await dbContext.IntegerAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, cancellationToken)
                            || await dbContext.DecimalAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, cancellationToken)
                            || await dbContext.DateAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, cancellationToken)
                            || await dbContext.TimeAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, cancellationToken)
                            || await dbContext.DateTimeAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, cancellationToken)
                            || await dbContext.FileAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, cancellationToken)
                            || await dbContext.LocationAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, cancellationToken);

        if (hasReferences)
            throw new InvalidOperationException("无法删除定义，因为它正被一个或多个节点属性值使用。");

        dbContext.AttributeDefinitions.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<AttributeValueBase> AddValueAsync(string nodeId, AddValueDto dto)
    {
        // 1. 用 Lambda 表达式获取定义、类型、单位
        var defQuery = dbContext.AttributeDefinitions
            .Where(d => d.Id == dto.AttributeDefinitionId)
            .Join(
                dbContext.AttributeTypes,
                d => d.AttributeTypeId,
                t => t.Id,
                (d, t) => new { d, t }
            )
            .GroupJoin(
                dbContext.UnitTrees,
                x => x.d.UnitId,
                u => u.Id,
                (x, units) => new { x.d, x.t, units }
            )
            .SelectMany(
                x => x.units.DefaultIfEmpty(),
                (x, u) => new { x.d, x.t, u }
            );

        var result = await defQuery.FirstOrDefaultAsync();
        if (result == null)
            throw new KeyNotFoundException($"属性定义 '{dto.AttributeDefinitionId}' 不存在。");

        var def = result.d;
        var attrType = result.t;
        var unit = result.u;

        var validation = ValueValidator.ValidateAndBuild(def, dto.Value, nodeId);
        if (!validation.IsValid)
            throw new ArgumentException(validation.ErrorMessage);

        var entity = validation.ValueEntity!;
        entity.Id = Guid.NewGuid().ToString();

        switch (entity)
        {
            case TextAttributeValue tv:
                await dbContext.TextAttributeValues.AddAsync(tv);
                break;
            case DecimalAttributeValue dv:
                await dbContext.DecimalAttributeValues.AddAsync(dv);
                break;
            case IntegerAttributeValue iv:
                await dbContext.IntegerAttributeValues.AddAsync(iv);
                break;
            case DateAttributeValue dav:
                await dbContext.DateAttributeValues.AddAsync(dav);
                break;
            case TimeAttributeValue tav:
                await dbContext.TimeAttributeValues.AddAsync(tav);
                break;
            case DateTimeAttributeValue dtav:
                await dbContext.DateTimeAttributeValues.AddAsync(dtav);
                break;
            case FileAttributeValue fav:
                await dbContext.FileAttributeValues.AddAsync(fav);
                break;
            case LocationAttributeValue lav:
                await dbContext.LocationAttributeValues.AddAsync(lav);
                break;
            default:
                throw new NotSupportedException($"不支持的值类型 '{entity.GetType().Name}'。");
        }

        await dbContext.SaveChangesAsync();
        return entity;
    }

    public async Task<AttributeDto?> GetValueAsync(string nodeId, string id)
    {
        var value = await FindValueAsync(nodeId, id);
        if (value == null) return null;

        var defInfo = await GetDefinitionWithTypeAndUnitAsync(value.AttributeDefinitionId);
        if (defInfo == null) return null;

        return MapToDto(value, defInfo.Definition, defInfo.AttributeType, defInfo.Unit);
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

        return new NodeDto { Id = nodeId, Attributes = list };
    }

    public async Task<bool> DeleteValueAsync(string nodeId, string id)
    {
        var value = await FindValueAsync(nodeId, id);
        if (value == null)
            return false;

        dbContext.Remove(value);
        await dbContext.SaveChangesAsync();
        return true;
    }

    // ==================== 私有辅助方法 ====================

    private async Task<AttributeValueBase?> FindValueAsync(string nodeId, string id)
    {
        var tasks = new Func<Task<AttributeValueBase?>>[]
        {
            async () => await dbContext.TextAttributeValues
                .FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
            async () => await dbContext.DecimalAttributeValues
                .FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
            async () => await dbContext.IntegerAttributeValues
                .FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
            async () => await dbContext.DateAttributeValues
                .FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
            async () => await dbContext.TimeAttributeValues
                .FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
            async () => await dbContext.DateTimeAttributeValues
                .FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
            async () => await dbContext.FileAttributeValues
                .FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
            async () => await dbContext.LocationAttributeValues
                .FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id)
        };

        foreach (var task in tasks)
        {
            var result = await task();
            if (result != null)
                return result;
        }

        return null;
    }

    private async Task<DefinitionWithTypeAndUnit?> GetDefinitionWithTypeAndUnitAsync(string definitionId)
    {
        var query = dbContext.AttributeDefinitions
            .Where(d => d.Id == definitionId)
            .Join(
                dbContext.AttributeTypes,
                d => d.AttributeTypeId,
                t => t.Id,
                (d, t) => new { d, t }
            )
            .GroupJoin(
                dbContext.UnitTrees,
                x => x.d.UnitId,
                u => u.Id,
                (x, units) => new { x.d, x.t, units }
            )
            .SelectMany(
                x => x.units.DefaultIfEmpty(),
                (x, u) => new DefinitionWithTypeAndUnit
                {
                    Definition = x.d,
                    AttributeType = x.t,
                    Unit = u
                }
            );

        return await query.FirstOrDefaultAsync();
    }

    private async Task<List<AttributeDto>> QueryValues<TValue>(string nodeId)
        where TValue : AttributeValueBase
    {
        var items = await dbContext.Set<TValue>()
            .Where(v => v.NodeId == nodeId)
            .ToListAsync();

        if (items.Count == 0) return new List<AttributeDto>();

        var result = new List<AttributeDto>();

        var definitionIds = items
            .Select(v => v.AttributeDefinitionId)
            .Distinct()
            .ToList();

        var defInfos = new Dictionary<string, DefinitionWithTypeAndUnit>();

        foreach (var defId in definitionIds)
        {
            var info = await GetDefinitionWithTypeAndUnitAsync(defId);
            if (info != null)
                defInfos[defId] = info;
        }

        foreach (var v in items)
        {
            if (defInfos.TryGetValue(v.AttributeDefinitionId, out var info))
            {
                result.Add(MapToDto(v, info.Definition, info.AttributeType, info.Unit));
            }
        }

        return result;
    }

    private static AttributeDto MapToDto(AttributeValueBase v, AttributeDefinition? def, AttributeType? attrType, UnitTree? unit)
    {
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

    private sealed class DefinitionWithTypeAndUnit
    {
        public AttributeDefinition Definition { get; set; } = null!;
        public AttributeType AttributeType { get; set; } = null!;
        public UnitTree? Unit { get; set; }
    }
}