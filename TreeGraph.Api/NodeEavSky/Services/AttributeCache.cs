using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TreeGraph.Api.Data;
using TreeGraph.Api.NodeEavSky.Entities;

namespace TreeGraph.Api.NodeEavSky.Services;

public interface IAttributeCache
{
    IReadOnlyList<AttributeDefinition> GetDefinitions(string entityType);
    AttributeDefinition? GetDefinition(string entityType, string attributeName);
    void Invalidate(string entityType);
}

/// <summary>属性元数据缓存（单例，读取频率极高）</summary>
public class AttributeCache : IAttributeCache
{
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public AttributeCache(IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    public IReadOnlyList<AttributeDefinition> GetDefinitions(string entityType)
    {
        return _cache.GetOrCreate($"eav:defs:{entityType}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            entry.SlidingExpiration = TimeSpan.FromMinutes(10);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TreeGraphDbContext>();
            return db.AttributeCatalog
                .Where(a => a.EntityType == entityType && !a.IsDeleted)
                .OrderBy(a => a.DisplayOrder)
                .AsNoTracking()
                .ToList();
        })!;
    }

    public AttributeDefinition? GetDefinition(string entityType, string attributeName)
        => GetDefinitions(entityType).FirstOrDefault(d => d.AttributeName == attributeName);

    public void Invalidate(string entityType)
        => _cache.Remove($"eav:defs:{entityType}");
}
