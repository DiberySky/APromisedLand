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
}

/// <summary>自定义表结构缓存（单例，含列定义，过滤软删除列）</summary>
public class CustomTableCache : ICustomTableCache
{
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;

    public CustomTableCache(IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    public CustomTableDefinition GetTable(long tableDefinitionId)
    {
        return _cache.GetOrCreate($"eav:ctable:{tableDefinitionId}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            entry.SlidingExpiration = TimeSpan.FromMinutes(10);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EavDbContext>();
            var table = db.CustomTables
                .Include(t => t.Columns)
                .AsNoTracking()
                .First(t => t.TableDefinitionId == tableDefinitionId);
            table.Columns.RemoveAll(c => c.IsDeleted);
            return table;
        })!;
    }

    public CustomTableDefinition? GetTableByName(string entityType, string tableName)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EavDbContext>();
        var def = db.CustomTables
            .Where(t => t.EntityType == entityType && t.TableName == tableName && !t.IsDeleted)
            .OrderByDescending(t => t.Version)
            .AsNoTracking()
            .FirstOrDefault();
        return def is null ? null : GetTable(def.TableDefinitionId);
    }

    public void Invalidate(long tableDefinitionId)
        => _cache.Remove($"eav:ctable:{tableDefinitionId}");
}
