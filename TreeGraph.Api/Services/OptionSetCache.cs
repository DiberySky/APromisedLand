using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TreeGraph.Api.Data;
using TreeGraph.Api.Entities;

namespace TreeGraph.Api.Services;

public interface IOptionSetCache
{
    OptionSet GetSet(long optionSetId);
    List<OptionSet> GetAll(string? entityType = null);
    void Invalidate(long optionSetId);
}

/// <summary>选项集缓存（单例，含未删除选项项）</summary>
public class OptionSetCache : IOptionSetCache
{
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;

    public OptionSetCache(IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    /// <summary>★ 修复 P1-1：集合不存在时抛 KeyNotFoundException，
    /// 让调用端可以捕获并降级（而不是 NRE）</summary>
    public OptionSet GetSet(long optionSetId)
    {
        return _cache.GetOrCreate($"eav:oset:{optionSetId}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            entry.SlidingExpiration = TimeSpan.FromMinutes(10);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EavDbContext>();
            var set = db.OptionSets
                .Include(s => s.Items.Where(i => !i.IsDeleted))
                .AsNoTracking()
                .FirstOrDefault(s => s.OptionSetId == optionSetId)
                ?? throw new KeyNotFoundException($"选项集不存在: {optionSetId}");
            set.Items = set.Items.OrderBy(i => i.DisplayOrder).ToList();
            return set;
        })!;
    }

    public List<OptionSet> GetAll(string? entityType = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EavDbContext>();
        var ids = db.OptionSets
            .Where(s => entityType == null
                     || s.EntityType == entityType || s.EntityType == "Shared")
            .OrderBy(s => s.OptionSetId)
            .AsNoTracking()
            .Select(s => s.OptionSetId)
            .ToList();

        var result = new List<OptionSet>(ids.Count);
        foreach (var id in ids)
        {
            try { result.Add(GetSet(id)); }
            catch (KeyNotFoundException) { /* 已删除，跳过 */ }
        }
        return result;
    }

    public void Invalidate(long optionSetId)
        => _cache.Remove($"eav:oset:{optionSetId}");
}

/// <summary>单选值运行时表示：Value 是存储值，Label 是展示名</summary>
public sealed record SingleChoiceValue(string Value, string Label);
