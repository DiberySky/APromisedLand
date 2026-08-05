using System.Globalization;
using APromisedLand.Api.Data;
using APromisedLand.Api.Projects.DiberyTree.Interface;
using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using APromisedLand.Shared.DiberyTree.Attributes.Models;
using APromisedLand.Shared.DiberyTree.Attributes.Validation;
using Microsoft.EntityFrameworkCore;

namespace APromisedLand.Api.Projects.DiberyTree.Services;

public class TreeAttributeService(DiberyDbContext dbContext) : ITreeAttributeService
{
    public async Task<IReadOnlyList<AttributeDefinitionDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.AttributeDefinitions
            .Include(d => d.AttributeType)
            .Include(d => d.UnitOfMeasure)
            .Select(d => new AttributeDefinitionDto
            {
                Id = d.Id,
                Name = d.Name,
                AttributeType = d.AttributeType,
                AttributeTypeName = d.AttributeType.Name,
                Lines = d.Lines,
                Precision = d.Precision,
                Scale = d.Scale,
                UnitOfMeasureId = d.UnitOfMeasureId,
                UnitOfMeasureName = d.UnitOfMeasure != null ? d.UnitOfMeasure.Name : null
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AttributeType>> GetAttributeTypesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.AttributeTypes
            .ToListAsync(cancellationToken);
    }
    
    public async Task<AttributeDefinitionDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.AttributeDefinitions
            .Include(d => d.AttributeType)
            .Include(d => d.UnitOfMeasure)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (entity == null) return null;

        return new AttributeDefinitionDto
        {
            Id = entity.Id,
            Name = entity.Name,
            AttributeType = entity.AttributeType,
            AttributeTypeName = entity.AttributeType.Name,
            Lines = entity.Lines,
            Precision = entity.Precision,
            Scale = entity.Scale,
            UnitOfMeasureId = entity.UnitOfMeasureId,
            UnitOfMeasureName = entity.UnitOfMeasure?.Name
        };
    }

    public async Task<AttributeDefinitionDto> CreateAsync(AttributeDefinitionCreateDto dto, CancellationToken cancellationToken = default)
    {
        // 检查属性类型是否存在
        var attrType = await dbContext.AttributeTypes.FindAsync(new object[] { dto.AttributeType }, cancellationToken);
        if (attrType == null)
            throw new ArgumentException($"AttributeType with id {dto.AttributeType} does not exist.");

        // 检查单位（如果提供）
        if (!string.IsNullOrEmpty(dto.UnitOfMeasureId))
        {
            var unit = await dbContext.UnitsOfMeasure.FindAsync(new object[] { dto.UnitOfMeasureId }, cancellationToken);
            if (unit == null)
                throw new ArgumentException($"UnitOfMeasure with id {dto.UnitOfMeasureId} does not exist.");
        }

        var entity = new AttributeDefinition
        {
            Id = Guid.NewGuid().ToString(),
            Name = dto.Name,
            AttributeType = dto.AttributeType,
            Lines = dto.Lines,
            Precision = dto.Precision,
            Scale = dto.Scale,
            UnitOfMeasureId = dto.UnitOfMeasureId
        };

        // 简单校验：只有文本类型可以有 Lines，只有数字类型可以有 Precision/Scale
        // 可以根据业务需求增强，此处不做强制，留给业务层或数据库约束

        dbContext.AttributeDefinitions.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        // 返回完整 DTO（重新加载关联）
        return await GetByIdAsync(entity.Id, cancellationToken) 
               ?? throw new Exception("Failed to retrieve created entity.");
    }

    public async Task<AttributeDefinitionDto> UpdateAsync(string id, AttributeDefinitionUpdateDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.AttributeDefinitions.FindAsync(new object[] { id }, cancellationToken);
        if (entity == null)
            throw new KeyNotFoundException($"AttributeDefinition with id {id} not found.");

        // 只更新允许修改的字段
        entity.Name = dto.Name;
        entity.Lines = dto.Lines;
        entity.Precision = dto.Precision;
        entity.Scale = dto.Scale;
        entity.UnitOfMeasureId = dto.UnitOfMeasureId;

        // 注意：不允许更改 AttributeTypeId，因为类型改变会导致值表不兼容，如有需要可设计迁移方案，此处忽略

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken) 
               ?? throw new Exception("Failed to retrieve updated entity.");
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.AttributeDefinitions.FindAsync(new object[] { id }, cancellationToken);
        if (entity == null) return false;

        // 检查是否被节点值引用（任一种值表）
        var hasReferences = await dbContext.TextAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, cancellationToken)
                            || await dbContext.IntegerAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, cancellationToken)
                            || await dbContext.DecimalAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, cancellationToken)
                            || await dbContext.DateAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, cancellationToken)
                            || await dbContext.TimeAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, cancellationToken)
                            || await dbContext.DateTimeAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, cancellationToken)
                            || await dbContext.FileAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, cancellationToken)
                            || await dbContext.LocationAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, cancellationToken);

        if (hasReferences)
            throw new InvalidOperationException("Cannot delete definition because it is being used by one or more node attribute values.");

        dbContext.AttributeDefinitions.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<AttributeValueBase> AddValueAsync(string nodeId, AddValueDto dto)
    {
        // 1. 获取属性定义（含类型和单位）
        var def = await dbContext.AttributeDefinitions
            .Include(d => d.AttributeType)
            .Include(d => d.UnitOfMeasure)
            .FirstOrDefaultAsync(d => d.Id == dto.AttributeDefinitionId);

        if (def == null)
            throw new KeyNotFoundException($"属性定义 '{dto.AttributeDefinitionId}' 不存在。");

        // 2. 验证并构建具体值实体
        var validation = ValueValidator.ValidateAndBuild(def, dto.Value, nodeId);
        if (!validation.IsValid)
            throw new ArgumentException(validation.ErrorMessage);

        var entity = validation.ValueEntity!;

        // 3. 添加到对应的 DbSet
        switch (entity)
        {
            case TextAttributeValue tv:
                dbContext.TextAttributeValues.Add(tv);
                break;
            case DecimalAttributeValue dv:
                dbContext.DecimalAttributeValues.Add(dv);
                break;
            case IntegerAttributeValue iv:
                dbContext.IntegerAttributeValues.Add(iv);
                break;
            case DateAttributeValue dav:
                dbContext.DateAttributeValues.Add(dav);
                break;
            case TimeAttributeValue tav:
                dbContext.TimeAttributeValues.Add(tav);
                break;
            case DateTimeAttributeValue dtav:
                dbContext.DateTimeAttributeValues.Add(dtav);
                break;
            case FileAttributeValue fav:
                dbContext.FileAttributeValues.Add(fav);
                break;
            case LocationAttributeValue lav:
                dbContext.LocationAttributeValues.Add(lav);
                break;
            default:
                throw new NotSupportedException($"不支持的值类型 '{entity.GetType().Name}'。");
        }

        await dbContext.SaveChangesAsync();

        // 返回完整实体（含自动生成的 Id）
        return entity;
    }

    public async Task<AttributeDto?> GetValueAsync(string nodeId, int id)
    {
        var value = await FindValueAsync(nodeId, id);
        return value == null ? null : MapToDto(value);
    }

    public async Task<NodeDto> GetAllValuesAsync(string nodeId)
    {
        var list = new List<AttributeDto>();

        // 分别查询每种值表
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

    public async Task<bool> DeleteValueAsync(string nodeId, int id)
    {
        var value = await FindValueAsync(nodeId, id);
        if (value == null)
            return false;

        dbContext.Remove(value);
        await dbContext.SaveChangesAsync();
        return true;
    }

    // ==================== 私有辅助方法 ====================

    /// <summary>
    /// 根据 nodeId 和 id 在8张值表中查找实体
    /// </summary>
    private async Task<AttributeValueBase?> FindValueAsync(string nodeId, int id)
    {
        // 依次检查各表，使用 Any + First 组合（可优化为直接 FirstOrDefault）
        var tables = new Func<Task<AttributeValueBase?>>[]
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

        foreach (var query in tables)
        {
            var result = await query();
            if (result != null)
                return result;
        }

        return null;
    }

    /// <summary>
    /// 查询指定类型的所有值，并映射为 AttributeDto
    /// </summary>
    private async Task<List<AttributeDto>> QueryValues<TValue>(string nodeId)
        where TValue : AttributeValueBase
    {
        var set = dbContext.Set<TValue>();
        var items = await set
            .Include(v => v.Definition)
                .ThenInclude(d => d.AttributeType)
            .Include(v => v.Definition)
                .ThenInclude(d => d.UnitOfMeasure)
            .Where(v => v.NodeId == nodeId)
            .ToListAsync();

        return items.Select(MapToDto).ToList();
    }

    /// <summary>
    /// 将值实体映射为 DTO（与原控制器逻辑一致）
    /// </summary>
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