using APromisedLand.Api.Data;
using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using APromisedLand.Shared.DiberyTree.Attributes.Enums;
using APromisedLand.Shared.DiberyTree.Attributes.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace APromisedLand.Api.DiberyTree.Services;

/// <summary>
/// 定位属性值的业务服务。
/// <para>仿 <see cref="AttributeTableValueService"/> 分层模式，从 TreeAttributeService 中分离。</para>
/// 负责 LocationAttributeValue 的 CRUD，独立路由、独立幂等去重。
/// </summary>
public partial class AttributeService
{
    private const int LocationDedupWindowSeconds = 30;
    private const string LocationDedupCachePrefix = "Location_Add_Dedup_";

    // ---------- 新增 ----------

    /// <summary>为节点添加一个定位值（带幂等去重）。</summary>
    /// <returns>(值 Id, 错误信息, 是否为重复请求)。</returns>
    public async Task<(string? ValueId, string? Error, bool Duplicated)> AddAsync(
        string nodeId, AddAttributeLocationValueDto dto)
    {
        // 幂等去重：基于 nodeId + 定义Id + lat + lon 计算指纹
        var fingerprint = ComputeFingerprint(nodeId, dto.AttributeDefinitionId, dto.Latitude, dto.Longitude);
        var cacheKey = LocationDedupCachePrefix + fingerprint;
    
        if (cache.TryGetValue<(string? ValueId, string? Error)>(cacheKey, out var cached))
            return (cached.ValueId, cached.Error, Duplicated: true);
    
        cache.Set(cacheKey, ((string?)null, (string?)null), TimeSpan.FromSeconds(LocationDedupWindowSeconds));
    
        var (valueId, error) = await AddInternalAsync(nodeId, dto);
    
        cache.Set(cacheKey, (valueId, error), TimeSpan.FromSeconds(LocationDedupWindowSeconds));
        return (valueId, error, Duplicated: false);
    }

    private async Task<(string? ValueId, string? Error)> AddInternalAsync(string nodeId, AddAttributeLocationValueDto dto)
    {
        // 校验定义存在且为定位类型
        var def = await db.AttributeDefinitions.FirstOrDefaultAsync(d => d.Id == dto.AttributeDefinitionId);
        if (def == null) return (null, $"属性定义 '{dto.AttributeDefinitionId}' 不存在");

        if (!AttributeTypeMapping.IdToEnum.TryGetValue(def.AttributeTypeId, out var attrType) || attrType != AttributeTypeEnum.定位)
            return (null, $"'{dto.AttributeDefinitionId}' 不是定位类型");

        // 校验经纬度范围
        if (dto.Latitude < -90 || dto.Latitude > 90)
            return (null, "纬度范围 [-90, 90]");
        if (dto.Longitude < -180 || dto.Longitude > 180)
            return (null, "经度范围 [-180, 180]");

        var valueId = Guid.NewGuid().ToString();
        db.LocationAttributeValues.Add(new LocationAttributeValue
        {
            Id = valueId,
            NodeId = nodeId,
            AttributeDefinitionId = dto.AttributeDefinitionId,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude
        });

        await db.SaveChangesAsync();
        return (valueId, null);
    }

    // ---------- 查询 ----------

    /// <summary>获取节点的所有定位值。</summary>
    public async Task<List<AttributeLocationValueDto>> ListByNodeAsync(string nodeId)
    {
        var items = await db.LocationAttributeValues
            .Where(v => v.NodeId == nodeId)
            .ToListAsync();

        var result = new List<AttributeLocationValueDto>();
        foreach (var v in items)
        {
            var def = await db.AttributeDefinitions.FirstOrDefaultAsync(d => d.Id == v.AttributeDefinitionId);
            result.Add(new AttributeLocationValueDto
            {
                ValueId = v.Id,
                NodeId = v.NodeId,
                DefinitionId = v.AttributeDefinitionId,
                DefinitionName = def?.Name,
                Latitude = v.Latitude,
                Longitude = v.Longitude
            });
        }
        return result;
    }

    /// <summary>获取单个定位值。</summary>
    public async Task<AttributeLocationValueDto?> GetAsync(string valueId)
    {
        var v = await db.LocationAttributeValues.FirstOrDefaultAsync(x => x.Id == valueId);
        if (v == null) return null;

        var def = await db.AttributeDefinitions.FirstOrDefaultAsync(d => d.Id == v.AttributeDefinitionId);
        return new AttributeLocationValueDto
        {
            ValueId = v.Id,
            NodeId = v.NodeId,
            DefinitionId = v.AttributeDefinitionId,
            DefinitionName = def?.Name,
            Latitude = v.Latitude,
            Longitude = v.Longitude
        };
    }

    // ---------- 更新 ----------

    /// <summary>更新定位值。返回错误信息（null 表示成功）。</summary>
    public async Task<string?> UpdateAsync(string valueId, UpdateAttributeLocationValueDto dto)
    {
        var v = await db.LocationAttributeValues.FirstOrDefaultAsync(x => x.Id == valueId);
        if (v == null) return $"定位值 '{valueId}' 不存在";

        if (dto.Latitude < -90 || dto.Latitude > 90) return "纬度范围 [-90, 90]";
        if (dto.Longitude < -180 || dto.Longitude > 180) return "经度范围 [-180, 180]";

        v.Latitude = dto.Latitude;
        v.Longitude = dto.Longitude;

        await db.SaveChangesAsync();
        return null;
    }

    // ---------- 删除 ----------

    /// <summary>删除定位值。返回 true=已删除, false=不存在。</summary>
    // public async Task<bool> DeleteAsync(string valueId)
    // {
    //     var v = await db.LocationAttributeValues.FirstOrDefaultAsync(x => x.Id == valueId);
    //     if (v == null) return false;
    //
    //     db.LocationAttributeValues.Remove(v);
    //     await db.SaveChangesAsync();
    //     return true;
    // }

    // ---------- 辅助 ----------

    private static string ComputeFingerprint(string nodeId, string defId, double lat, double lon)
    {
        var raw = $"{nodeId}|{defId}|{lat:F6}|{lon:F6}";
        var bytes = System.Text.Encoding.UTF8.GetBytes(raw);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
