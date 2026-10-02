using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TreeGraph.Api.Data;
using TreeGraph.Api.Entities;

namespace TreeGraph.Api.Services;

public interface IOptionSetCache
{
    OptionSet GetSet(long optionSetId);
    List<OptionSet> GetAll(string? entityType = null, bool includeDeleted = false);
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

    /// <summary>
    /// 按 ID 取选项集。**不过滤集合级 IsDeleted**：
    /// 历史数据里已存有该集合的 Value，读取时需要 Label 做降级显示。
    /// 集合级 IsDeleted 仅用于管理页列表过滤。
    /// </summary>
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

    /// <summary>
    /// 列出选项集。
    /// ★ includeDeleted = true 时返回软删除的集合（管理页恢复入口）。
    /// </summary>
    public List<OptionSet> GetAll(string? entityType = null, bool includeDeleted = false)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EavDbContext>();

        var query = db.OptionSets.AsQueryable();
        if (!includeDeleted) query = query.Where(s => !s.IsDeleted);
        if (entityType is not null)
            query = query.Where(s => s.EntityType == entityType || s.EntityType == "Shared");

        var ids = query.OrderBy(s => s.OptionSetId)
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
