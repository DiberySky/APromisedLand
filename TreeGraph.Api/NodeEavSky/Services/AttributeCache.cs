using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TreeGraph.Api.Data;
using TreeGraph.Api.NodeEavSky.Entities;

namespace TreeGraph.Api.NodeEavSky.Services;

public interface IAttributeCache
{
    IReadOnlyList<AttributeDefinition> GetDefinitions();
    AttributeDefinition? GetDefinition(string attributeName);
    void Invalidate();
}

/// <summary>属性元数据缓存（单例，读取频率极高）。属性全局共享，整库一份目录。</summary>
public class AttributeCache : IAttributeCache
{
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);
    private const string CacheKey = "eav:defs:global";

    public AttributeCache(IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    public IReadOnlyList<AttributeDefinition> GetDefinitions()
    {
        return _cache.GetOrCreate(CacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            entry.SlidingExpiration = TimeSpan.FromMinutes(10);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TreeGraphDbContext>();
            return db.AttributeCatalog
                .OrderBy(a => a.DisplayOrder)
                .AsNoTracking()
                .ToList();
        })!;
    }

    public AttributeDefinition? GetDefinition(string attributeName)
        => GetDefinitions().FirstOrDefault(d => d.AttributeName == attributeName);

    public void Invalidate()
        => _cache.Remove(CacheKey);
}
