using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using APromisedLand.Api.Data;
using Microsoft.Extensions.Caching.Memory;
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
    // 幂等去重：属性值请求指纹缓存的时间窗口（秒）
    private const int ValueDedupWindowSeconds = 30;
    private const string ValueDedupCachePrefix = "Attr_AddValue_Dedup_";

    // ---------- 属性值操作 ----------

    /// <summary>为指定节点添加一个属性值（带幂等去重）。</summary>
    /// <returns>(值实体, 错误信息, 是否为重复请求)。当 Duplicated=true 表示请求已在 30 秒窗口内处理过，已直接返回第一次结果。</returns>
    public async Task<(AttributeValueBase? Value, string? Error, bool Duplicated)> AddValueAsync(string nodeId, AddValueDto dto)
    {
        // ========== 第一步：基于请求内容计算 SHA256 指纹，做幂等去重 ==========
        var fingerprint = ComputeAddValueFingerprint(nodeId, dto);
        var cacheKey = ValueDedupCachePrefix + fingerprint;

        // 命中缓存（含占位）：直接复用第一次结果，不再落库
        if (cache.TryGetValue<(string? ValueId, string? Error)>(cacheKey, out var cached))
        {
            AttributeValueBase? cachedValue = null;
            if (!string.IsNullOrEmpty(cached.ValueId))
                cachedValue = await FindValueAsync(nodeId, cached.ValueId!);
            return (cachedValue, cached.Error, Duplicated: true);
        }

        // 占位：防止首次落库完成前又进来相同请求（重试 / 并发窗口保护）
        cache.Set(cacheKey, (ValueId: (string?)null, Error: (string?)null),
            TimeSpan.FromSeconds(ValueDedupWindowSeconds));

        // ========== 第二步：执行业务插入逻辑 ==========
        try
        {
            var entity = await AddValueInternalAsync(nodeId, dto);

            // 将实际结果写回缓存，覆盖占位值
            cache.Set(cacheKey, (ValueId: entity.Id, Error: (string?)null),
                TimeSpan.FromSeconds(ValueDedupWindowSeconds));

            return (entity, null, Duplicated: false);
        }
        catch (Exception)
        {
            // 失败：移除占位，允许后续重试真正执行（而非被误判为重复而返回空结果）
            cache.Remove(cacheKey);
            throw;
        }
    }

    /// <summary>计算 AddValue 请求的 SHA256 指纹。相同 nodeId + 相同定义 Id + 相同值得到相同指纹。</summary>
    private static string ComputeAddValueFingerprint(string nodeId, AddValueDto dto)
    {
        var sb = new StringBuilder();
        sb.Append("nodeId=").Append(nodeId).Append('|');
        sb.Append("defId=").Append(dto.AttributeDefinitionId).Append('|');
        sb.Append("value=").Append(dto.Value.ValueKind == JsonValueKind.Undefined ? "" : dto.Value.GetRawText());

        var raw = Encoding.UTF8.GetBytes(sb.ToString());
        return Convert.ToBase64String(SHA256.HashData(raw));
    }

    /// <summary>实际的属性值插入逻辑（不含去重）。校验失败时抛出异常由控制器映射 HTTP 状态码。</summary>
    private async Task<AttributeValueBase> AddValueInternalAsync(string nodeId, AddValueDto dto)
    {
        // 1. 获取定义
        var definition = await db.AttributeDefinitions
            .FirstOrDefaultAsync(d => d.Id == dto.AttributeDefinitionId);
        if (definition == null)
            throw new KeyNotFoundException($"属性定义 '{dto.AttributeDefinitionId}' 不存在。");

        // 2. 从映射获取枚举类型
        if (!AttributeTypeMapping.IdToEnum.TryGetValue(definition.AttributeTypeId, out var attrType))
            throw new InvalidOperationException($"无法识别属性类型ID: {definition.AttributeTypeId}");
        
        var attrTypeEmun = definition.AttributeTypeId.ToAttributeTypeName();
        
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
            case LocationAttributeDefValue lav: await db.LocationAttributeDefValues.AddAsync(lav); break;
            case TableAttributeDefValue tv: await db.TableAttributeDefValues.AddAsync(tv); break;
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
        // 注意：AddValueAsync 对「定位」写入的是 LocationAttributeDefValue（定义值/名称字符串），
        // 而非 LocationAttributeValue（经纬度对象）。查询必须与写入保持一致，否则查不到刚写入的数据。
        list.AddRange(await QueryValues<LocationAttributeDefValue>(nodeId));
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
            // 定位有两张表：LocationAttributeValue（经纬度对象）与 LocationAttributeDefValue（定义/名称字符串）
            async () => await db.LocationAttributeValues.FirstOrDefaultAsync(v =>
                v.NodeId == nodeId && v.Id == id),
            async () => await db.LocationAttributeDefValues.FirstOrDefaultAsync(v =>
                v.NodeId == nodeId && v.Id == id),
            async () => await db.TableAttributeDefValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
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
            // 定位定义值：存储的是地点名称/字符串，直接返回 Value（与 AddValueAsync 的写入方式一致）
            LocationAttributeDefValue ldv => ldv.Value,
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
            case LocationAttributeDefValue ldvTarget:
                var ldvSource = (LocationAttributeDefValue)source;
                ldvTarget.Value = ldvSource.Value;
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