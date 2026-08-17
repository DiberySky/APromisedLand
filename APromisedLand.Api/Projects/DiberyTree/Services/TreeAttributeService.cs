using System.Globalization;
using System.Text.Json;
using APromisedLand.Api.Data;
using APromisedLand.Api.Projects.DiberyTree.Interface;
using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using APromisedLand.Shared.DiberyTree.Attributes.Enums;
using APromisedLand.Shared.DiberyTree.Attributes.Models;
using APromisedLand.Shared.DiberyTree.Attributes.Validation;
using APromisedLand.Shared.DiberyTree.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace APromisedLand.Api.Projects.DiberyTree.Services;

public class TreeAttributeService : ITreeAttributeService
{
    private readonly DiberyDbContext _dbContext;
    private readonly ILogger<TreeAttributeService> _logger;

    public TreeAttributeService(DiberyDbContext dbContext, ILogger<TreeAttributeService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    // ---------- 属性定义 ----------
    public async Task<IReadOnlyList<AttributeDefinitionDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var query = _dbContext.AttributeDefinitions
            .Join(_dbContext.AttributeTypes, d => d.AttributeTypeId, t => t.Id, (d, t) => new { d, t })
            .GroupJoin(_dbContext.UnitTrees, x => x.d.UnitId, u => u.Id, (x, units) => new { x.d, x.t, units })
            .SelectMany(x => x.units.DefaultIfEmpty(), (x, u) => new AttributeDefinitionDto
            {
                Id = x.d.Id,
                Name = x.d.Name,
                AttributeType = x.t,
                // AttributeTypeName = x.t.Name,
                MaxLength = x.d.MaxLength,
                Lines = x.d.Lines,
                Precision = x.d.Precision,
                Scale = x.d.Scale,
                Unit = u
            });
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AttributeType>> GetAttributeTypesAsync(
        CancellationToken cancellationToken = default)
        => await _dbContext.AttributeTypes.ToListAsync(cancellationToken);

    public async Task<AttributeDefinitionDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.AttributeDefinitions
            .Where(d => d.Id == id)
            .Join(_dbContext.AttributeTypes, d => d.AttributeTypeId, t => t.Id, (d, t) => new { d, t })
            .GroupJoin(_dbContext.UnitTrees, x => x.d.UnitId, u => u.Id, (x, units) => new { x.d, x.t, units })
            .SelectMany(x => x.units.DefaultIfEmpty(), (x, u) => new AttributeDefinitionDto
            {
                Id = x.d.Id,
                Name = x.d.Name,
                AttributeType = x.t,
                // AttributeTypeName = x.t.Name,
                MaxLength = x.d.MaxLength,
                Lines = x.d.Lines,
                Precision = x.d.Precision,
                Scale = x.d.Scale,
                Unit = u
            });
        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<AttributeDefinitionDto> CreateAsync(AttributeDefinitionCreateDto dto,
        CancellationToken cancellationToken = default)
    {
        // 验证类型存在
        if (dto.AttributeType == null || string.IsNullOrEmpty(dto.AttributeType.Id))
            throw new ArgumentException("AttributeType is required.");
        var attrTypeExists =
            await _dbContext.AttributeTypes.AnyAsync(t => t.Id == dto.AttributeType.Id, cancellationToken);
        if (!attrTypeExists)
            throw new ArgumentException($"AttributeType with id {dto.AttributeType.Id} does not exist.");

        if (!string.IsNullOrEmpty(dto.UnitId))
        {
            var unitExists = await _dbContext.UnitTrees.AnyAsync(u => u.Id == dto.UnitId, cancellationToken);
            if (!unitExists)
                throw new ArgumentException($"Unit with id {dto.UnitId} does not exist.");
        }

        var entity = new AttributeDefinition
        {
            Id = Guid.NewGuid().ToString(),
            Name = dto.Name,
            AttributeTypeId = dto.AttributeType.Id,
            MaxLength = dto.MaxLength ?? 30,
            Lines = dto.Lines ?? 1,
            Precision = dto.Precision,
            Scale = dto.Scale,
            UnitId = dto.UnitId
        };

        await _dbContext.AttributeDefinitions.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(entity.Id, cancellationToken)
               ?? throw new Exception("Failed to retrieve created entity.");
    }

    public async Task<AttributeDefinitionDto> UpdateAsync(string id, AttributeDefinitionUpdateDto dto,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.AttributeDefinitions.FindAsync(new object[] { id }, cancellationToken);
        if (entity == null) throw new KeyNotFoundException($"AttributeDefinition with id {id} not found.");

        entity.Name = dto.Name;
        entity.Lines = dto.Lines;
        entity.Precision = dto.Precision;
        entity.Scale = dto.Scale;
        entity.UnitId = dto.UnitId;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken) ?? throw new Exception("Failed to retrieve updated entity.");
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.AttributeDefinitions.FindAsync(new object[] { id }, cancellationToken);
        if (entity == null) return false;

        bool hasReferences =
            await _dbContext.TextAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, cancellationToken)
            || await _dbContext.IntegerAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, cancellationToken)
            || await _dbContext.DecimalAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, cancellationToken)
            || await _dbContext.DateAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, cancellationToken)
            || await _dbContext.TimeAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, cancellationToken)
            || await _dbContext.DateTimeAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, cancellationToken)
            || await _dbContext.FileAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, cancellationToken)
            || await _dbContext.LocationAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, cancellationToken);

        if (hasReferences)
            throw new InvalidOperationException("无法删除定义，因为它正被一个或多个节点属性值使用。");

        _dbContext.AttributeDefinitions.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---------- 属性值操作 ----------
    public async Task<AttributeValueBase> AddValueAsync(string nodeId, AddValueDto dto)
    {
        // 查询定义并加载类型
        var defWithType = await _dbContext.AttributeDefinitions
            .Join(_dbContext.AttributeTypes, d => d.AttributeTypeId, t => t.Id, (d, t) => new { d, t })
            .Where(x => x.d.Id == dto.AttributeDefinitionId)
            .Select(x => new { Definition = x.d, AttributeType = x.t })
            .FirstOrDefaultAsync();
        if (defWithType == null)
            throw new KeyNotFoundException($"属性定义 '{dto.AttributeDefinitionId}' 不存在。");

        var definition = defWithType.Definition;
        definition.AttributeType = defWithType.AttributeType;

        // 直接传递 JsonElement 给验证器
        var validation = ValueValidator.ValidateAndBuild(definition, dto.Value, nodeId);
        if (!validation.IsValid)
            throw new ArgumentException(validation.ErrorMessage);

        var entity = validation.ValueEntity!;
        entity.Id = Guid.NewGuid().ToString();

        // 🔧 处理 DateTimeAttributeValue 的 UTC 转换
        if (entity is DateTimeAttributeValue dt)
        {
            var original = dt.Value;
            // 强制转换为 UTC（偏移量为 0）
            dt.Value = new DateTimeOffset(original.UtcDateTime, TimeSpan.Zero);
            _logger.LogInformation(
                "DateTimeAttributeValue 时区转换: 原始 {Original} (Offset={Offset}) -> 转换后 {Converted} (Offset=00:00)",
                original, original.Offset, dt.Value);
        }

        // 🔧 处理 DateAttributeValue 的 UTC 转换（修复：PostgreSQL timestamptz 只接受 UTC）
        if (entity is DateAttributeValue d)
        {
            var original = d.Value;
            d.Value = new DateTimeOffset(original.UtcDateTime, TimeSpan.Zero);
            _logger.LogInformation(
                "DateAttributeValue 时区转换: 原始 {Original} (Offset={Offset}) -> 转换后 {Converted} (Offset=00:00)",
                original, original.Offset, d.Value);
        }

        // 如果项目中有其他日期时间实体类型，可在此添加
        // else if (entity is DateTimeOffsetAttributeValue dtoffset)
        // {
        //     dtoffset.Value = dtoffset.Value.ToUniversalTime();
        // }

        // 添加到对应的 DbSet
        switch (entity)
        {
            case TextAttributeValue tv: await _dbContext.TextAttributeValues.AddAsync(tv); break;
            case DecimalAttributeValue dv: await _dbContext.DecimalAttributeValues.AddAsync(dv); break;
            case IntegerAttributeValue iv: await _dbContext.IntegerAttributeValues.AddAsync(iv); break;
            case DateAttributeValue dav: await _dbContext.DateAttributeValues.AddAsync(dav); break;
            case TimeAttributeValue tav: await _dbContext.TimeAttributeValues.AddAsync(tav); break;
            case DateTimeAttributeValue dtav: await _dbContext.DateTimeAttributeValues.AddAsync(dtav); break;
            case FileAttributeValue fav: await _dbContext.FileAttributeValues.AddAsync(fav); break;
            case LocationAttributeValue lav: await _dbContext.LocationAttributeValues.AddAsync(lav); break;
            default: throw new NotSupportedException($"不支持的值类型 '{entity.GetType().Name}'。");
        }

        await _dbContext.SaveChangesAsync();
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
        return new NodeDto { Id = nodeId, Attributes = list };
    }

    public async Task<bool> DeleteValueAsync(string nodeId, string id)
    {
        var value = await FindValueAsync(nodeId, id);
        if (value == null) return false;
        _dbContext.Remove(value);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task UpdateValueAsyncDS(string nodeId, string id, JsonElement jsonValue, CancellationToken cancellationToken)
    {
        // 1. 查找现有的属性值实体
        var existing = await FindValueAsync(nodeId, id);
        if (existing == null)
            throw new KeyNotFoundException($"属性值 '{id}' 不存在或不属于节点 '{nodeId}'。");

        // 2. 获取属性定义及类型信息
        var info = await GetDefinitionWithTypeAndUnitAsync(existing.AttributeDefinitionId);
        if (info == null)
            throw new InvalidOperationException($"属性定义 '{existing.AttributeDefinitionId}' 不存在。");

        var definition = info.Definition;
        definition.AttributeType = info.AttributeType; // 为验证器提供类型信息

        // 3. 验证新值（使用现有的验证器）
        var validation = ValueValidator.ValidateAndBuild(definition, jsonValue, nodeId);
        if (!validation.IsValid)
            throw new ArgumentException(validation.ErrorMessage);

        var newEntity = validation.ValueEntity!;

        // 4. 将新值复制到现有实体（类型必须一致）
        CopyValue(existing, newEntity);

        // 5. 对日期时间类型进行 UTC 转换（与 AddValueAsync 保持一致）
        if (existing is DateTimeAttributeValue dtExisting)
        {
            var original = dtExisting.Value;
            dtExisting.Value = new DateTimeOffset(original.UtcDateTime, TimeSpan.Zero);
            _logger.LogInformation(
                "DateTimeAttributeValue 时区转换: 原始 {Original} (Offset={Offset}) -> 转换后 {Converted} (Offset=00:00)",
                original, original.Offset, dtExisting.Value);
        }
        else if (existing is DateAttributeValue dExisting)
        {
            var original = dExisting.Value;
            dExisting.Value = new DateTimeOffset(original.UtcDateTime, TimeSpan.Zero);
            _logger.LogInformation(
                "DateAttributeValue 时区转换: 原始 {Original} (Offset={Offset}) -> 转换后 {Converted} (Offset=00:00)",
                original, original.Offset, dExisting.Value);
        }

        // 6. 保存更改
        await _dbContext.SaveChangesAsync(cancellationToken);
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
            default:
                throw new NotSupportedException($"不支持的值类型 '{target.GetType().Name}'。");
        }
    }
    
    public async Task UpdateValueAsync(string nodeId, string id, JsonElement value, CancellationToken cancellationToken)
    {
        // 1. 查找现有值
        var existing = await FindValueAsync(nodeId, id);
        if (existing == null)
            throw new KeyNotFoundException($"属性值 '{id}' 在节点 '{nodeId}' 中不存在。");

        // 2. 获取定义并加载类型
        var defWithType = await _dbContext.AttributeDefinitions
            .Join(_dbContext.AttributeTypes, d => d.AttributeTypeId, t => t.Id, (d, t) => new { d, t })
            .Where(x => x.d.Id == existing.AttributeDefinitionId)
            .Select(x => new { Definition = x.d, AttributeType = x.t })
            .FirstOrDefaultAsync(cancellationToken);
        if (defWithType == null)
            throw new KeyNotFoundException($"属性定义 '{existing.AttributeDefinitionId}' 不存在。");

        var definition = defWithType.Definition;
        definition.AttributeType = defWithType.AttributeType;

        // 3. 验证新值
        var validation = ValueValidator.ValidateAndBuild(definition, value, nodeId);
        if (!validation.IsValid)
            throw new ArgumentException(validation.ErrorMessage);

        var newEntity = validation.ValueEntity!;

        // 4. 将新值复制到现有实体（保留原 Id）
        switch (existing)
        {
            case TextAttributeValue existingText when newEntity is TextAttributeValue newText:
                existingText.Value = newText.Value;
                break;
            case DecimalAttributeValue existingDec when newEntity is DecimalAttributeValue newDec:
                existingDec.Value = newDec.Value;
                break;
            case IntegerAttributeValue existingInt when newEntity is IntegerAttributeValue newInt:
                existingInt.Value = newInt.Value;
                break;
            case DateAttributeValue existingDate when newEntity is DateAttributeValue newDate:
                existingDate.Value = newDate.Value;
                break;
            case TimeAttributeValue existingTime when newEntity is TimeAttributeValue newTime:
                existingTime.Value = newTime.Value;
                break;
            case DateTimeAttributeValue existingDt when newEntity is DateTimeAttributeValue newDt:
                existingDt.Value = newDt.Value;
                break;
            case FileAttributeValue existingFile when newEntity is FileAttributeValue newFile:
                existingFile.Value = newFile.Value;
                break;
            case LocationAttributeValue existingLoc when newEntity is LocationAttributeValue newLoc:
                existingLoc.Latitude = newLoc.Latitude;
                existingLoc.Longitude = newLoc.Longitude;
                break;
            default:
                throw new InvalidOperationException(
                    $"属性值类型不匹配或不受支持：现有类型 '{existing.GetType().Name}'，新值类型 '{newEntity.GetType().Name}'。");
        }

        // 5. 处理 DateTimeAttributeValue 的 UTC 转换
        if (existing is DateTimeAttributeValue dt)
        {
            var original = dt.Value;
            dt.Value = new DateTimeOffset(original.UtcDateTime, TimeSpan.Zero);
            _logger.LogInformation(
                "DateTimeAttributeValue 更新时区转换: 原始 {Original} (Offset={Offset}) -> 转换后 {Converted} (Offset=00:00)",
                original, original.Offset, dt.Value);
        }

        // 6. 处理 DateAttributeValue 的 UTC 转换
        if (existing is DateAttributeValue d)
        {
            var original = d.Value;
            d.Value = new DateTimeOffset(original.UtcDateTime, TimeSpan.Zero);
            _logger.LogInformation(
                "DateAttributeValue 更新时区转换: 原始 {Original} (Offset={Offset}) -> 转换后 {Converted} (Offset=00:00)",
                original, original.Offset, d.Value);
        }

        // 7. 保存
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // ---------- 私有辅助方法 ----------
    private async Task<AttributeValueBase?> FindValueAsync(string nodeId, string id)
    {
        var tasks = new Func<Task<AttributeValueBase?>>[]
        {
            async () => await _dbContext.TextAttributeValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
            async () => await _dbContext.DecimalAttributeValues.FirstOrDefaultAsync(v =>
                v.NodeId == nodeId && v.Id == id),
            async () => await _dbContext.IntegerAttributeValues.FirstOrDefaultAsync(v =>
                v.NodeId == nodeId && v.Id == id),
            async () => await _dbContext.DateAttributeValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
            async () => await _dbContext.TimeAttributeValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
            async () => await _dbContext.DateTimeAttributeValues.FirstOrDefaultAsync(v =>
                v.NodeId == nodeId && v.Id == id),
            async () => await _dbContext.FileAttributeValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
            async () => await _dbContext.LocationAttributeValues.FirstOrDefaultAsync(v =>
                v.NodeId == nodeId && v.Id == id)
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
        var query = _dbContext.AttributeDefinitions
            .Where(d => d.Id == definitionId)
            .Join(_dbContext.AttributeTypes, d => d.AttributeTypeId, t => t.Id, (d, t) => new { d, t })
            .GroupJoin(_dbContext.UnitTrees, x => x.d.UnitId, u => u.Id, (x, units) => new { x.d, x.t, units })
            .SelectMany(x => x.units.DefaultIfEmpty(), (x, u) => new DefinitionWithTypeAndUnit
            {
                Definition = x.d,
                AttributeType = x.t,
                Unit = u
            });
        return await query.FirstOrDefaultAsync();
    }

    private async Task<List<AttributeDto>> QueryValues<TValue>(string nodeId) where TValue : AttributeValueBase
    {
        var items = await _dbContext.Set<TValue>().Where(v => v.NodeId == nodeId).ToListAsync();
        if (items.Count == 0) return new List<AttributeDto>();

        var definitionIds = items.Select(v => v.AttributeDefinitionId).Distinct().ToList();
        var defInfos = new Dictionary<string, DefinitionWithTypeAndUnit>();

        foreach (var defId in definitionIds)
        {
            var info = await GetDefinitionWithTypeAndUnitAsync(defId);
            if (info != null) defInfos[defId] = info;
        }

        var result = new List<AttributeDto>();
        foreach (var v in items)
        {
            if (defInfos.TryGetValue(v.AttributeDefinitionId, out var info))
                result.Add(MapToDto(v, info.Definition, info.AttributeType, info.Unit));
        }

        return result;
    }

    private static AttributeDto MapToDto(AttributeValueBase v, AttributeDefinition? def, AttributeType? attrType,
        UnitTree? unit)
    {
        // 确保 def 包含导航属性（用于前端）
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
            LocationAttributeValue lv => new { lv.Latitude, lv.Longitude }, // 或 string
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

    private sealed class DefinitionWithTypeAndUnit
    {
        public AttributeDefinition Definition { get; set; } = null!;
        public AttributeType AttributeType { get; set; } = null!;
        public UnitTree? Unit { get; set; }
    }
}