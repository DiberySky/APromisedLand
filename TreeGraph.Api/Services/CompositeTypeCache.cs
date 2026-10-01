using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TreeGraph.Api.Data;
using TreeGraph.Api.Entities;

namespace TreeGraph.Api.Services;

public interface ICompositeTypeCache
{
    CompositeTypeDefinition GetType(long compositeTypeId);
    void Invalidate(long compositeTypeId);
}

/// <summary>组合类型定义缓存（单例）</summary>
public class CompositeTypeCache : ICompositeTypeCache
{
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;

    public CompositeTypeCache(IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    public CompositeTypeDefinition GetType(long compositeTypeId)
    {
        return _cache.GetOrCreate($"eav:composite:{compositeTypeId}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            entry.SlidingExpiration = TimeSpan.FromMinutes(10);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EavDbContext>();
            return db.CompositeTypes
                .Include(t => t.Fields)
                .AsNoTracking()
                .First(t => t.CompositeTypeId == compositeTypeId);
        })!;
    }

    public void Invalidate(long compositeTypeId)
        => _cache.Remove($"eav:composite:{compositeTypeId}");
}
