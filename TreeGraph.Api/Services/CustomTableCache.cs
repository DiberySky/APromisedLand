using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TreeGraph.Api.Data;
using TreeGraph.Api.Entities;

namespace TreeGraph.Api.Services;

public interface ICustomTableCache
{
    CustomTableDefinition GetTable(long tableDefinitionId);
    CustomTableDefinition? GetTableByName(string entityType, string tableName);
    void Invalidate(long tableDefinitionId);

    /// <summary>
    /// 清除按名称索引的缓存（name → id 映射）。
    /// 表创建/软删除/改版本后，若希望立即生效可调用；否则等 TTL 到期。
    /// </summary>
    void InvalidateByName(string entityType, string tableName);
}

/// <summary>
/// 自定义表结构缓存（单例，含列定义，过滤软删除列）。
///
/// 两级缓存：
///   - 实体缓存：key = `eav:ctable:{id}`       → 完整 CustomTableDefinition（含 Columns）
///   - 名称索引：key = `eav:ctable:name:{et}:{tn}` → TableDefinitionId（0 表示不存在）
///
/// GetTableByName 命中两级缓存时，**不创建 DI scope**，避免高频调用时
/// 每次都查 DB（即使命中实体缓存也会先创建 scope 查 id）。
/// </summary>
public class CustomTableCache : ICustomTableCache
{
    private const string EntityKeyPrefix = "eav:ctable:";
    private const string NameKeyPrefix = "eav:ctable:name:";

    private static readonly TimeSpan EntityTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan NameTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan Sliding = TimeSpan.FromMinutes(10);

    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;

    public CustomTableCache(IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    private static string EntityKey(long id) => $"{EntityKeyPrefix}{id}";
    private static string NameKey(string entityType, string tableName)
        => $"{NameKeyPrefix}{entityType}:{tableName}";

    public CustomTableDefinition GetTable(long tableDefinitionId)
    {
        return _cache.GetOrCreate(EntityKey(tableDefinitionId), entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = EntityTtl;
            entry.SlidingExpiration = Sliding;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EavDbContext>();
            var table = db.CustomTables
                .Include(t => t.Columns)
                .AsNoTracking()
                // ★ 过滤软删除：防止 name 缓存命中旧 id 时把已删表重新加载回实体缓存
                .First(t => t.TableDefinitionId == tableDefinitionId && !t.IsDeleted);
            table.Columns.RemoveAll(c => c.IsDeleted);
            return table;
        })!;
    }

    /// <summary>
    /// 按 (entityType, tableName) 查询表定义。
    ///
    /// ★ P2-2：name → id 映射也缓存（TTL 10 分钟），命中时直接走实体缓存，
    ///   不再每次创建 DI scope 查 DB。
    ///
    /// 最终一致：表被软删除/新版本上线后，最多 10 分钟内按名查询仍返回旧值。
    /// 需要立即生效，请调用 <see cref="InvalidateByName"/>。
    /// </summary>
    public CustomTableDefinition? GetTableByName(string entityType, string tableName)
    {
        // 0 是哨兵值（TableDefinitionId 是 IdentityAlwaysColumn，从 1 开始），
        // 表示"不存在"。用它避免 IMemoryCache 不缓存 null 导致的缓存击穿。
        var id = _cache.GetOrCreate(NameKey(entityType, tableName), entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = NameTtl;
            entry.SlidingExpiration = Sliding;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EavDbContext>();
            return db.CustomTables
                .Where(t => t.EntityType == entityType
                         && t.TableName == tableName
                         && !t.IsDeleted)
                .OrderByDescending(t => t.Version)
                .Select(t => t.TableDefinitionId)
                .FirstOrDefault();   // 无记录时返回 0
        });

        return id == 0 ? null : GetTable(id);
    }

    public void Invalidate(long tableDefinitionId)
        => _cache.Remove(EntityKey(tableDefinitionId));

    public void InvalidateByName(string entityType, string tableName)
        => _cache.Remove(NameKey(entityType, tableName));
}
