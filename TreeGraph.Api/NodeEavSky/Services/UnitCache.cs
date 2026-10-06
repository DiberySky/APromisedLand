using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TreeGraph.Api.NodeEavSky.Data;
using TreeGraph.Api.NodeEavSky.Entities;

namespace TreeGraph.Api.NodeEavSky.Services;

public interface IUnitCache
{
    Unit Get(Guid id);
    IReadOnlyList<Unit> GetAll();
    IReadOnlyList<Unit> GetByCategory(string category);
    Unit? GetBaseUnit(string category);
    void Invalidate();
}

/// <summary>单位缓存（单例，单位表几乎不变）</summary>
public class UnitCache : IUnitCache
{
    private const string CacheKey = "eav:units:all";
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;

    public UnitCache(IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    public IReadOnlyList<Unit> GetAll()
    {
        return _cache.GetOrCreate(CacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            entry.SlidingExpiration = TimeSpan.FromMinutes(20);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TreeGraphDbContext>();
            return db.Units
                .Where(u => !u.IsDeleted)
                .OrderBy(u => u.Category)
                .ThenBy(u => u.DisplayOrder)
                .AsNoTracking()
                .ToList();
        })!;
    }

    public Unit Get(Guid id)
    {
        var unit = GetAll().FirstOrDefault(u => u.Id == id);
        if (unit is null)
            throw new InvalidOperationException($"单位不存在: {id}");
        return unit;
    }

    public IReadOnlyList<Unit> GetByCategory(string category)
        => GetAll().Where(u => u.Category == category).ToList();

    public Unit? GetBaseUnit(string category)
        => GetAll().FirstOrDefault(u => u.Category == category && u.IsBaseUnit);

    public void Invalidate() => _cache.Remove(CacheKey);
}
