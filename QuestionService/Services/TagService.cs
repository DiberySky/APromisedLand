using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using QuestionService.Data;
using QuestionService.Models;
using StackExchange.Redis;

namespace QuestionService.Services;

public class TagService(IMemoryCache cache, QuestionDbContext db, IConnectionMultiplexer cacheMux)
{
    private const string CacheKey = "TagService";
    private readonly IDatabase _cache = cacheMux.GetDatabase();

    private async Task<List<Tag>> GetTagsFromRedis()
    {
        // 1. 尝试从 Redis 读取
        var cachedJson = await _cache.StringGetAsync(CacheKey);
        if (!cachedJson.IsNullOrEmpty)
        {
            // 显式转换为 string，解决重载歧义
            string json = cachedJson.ToString();
            return JsonSerializer.Deserialize<List<Tag>>(json) ?? [];
        }

        // 2. 缓存未命中，从数据库加载
        var tags = await db.Tags.AsNoTracking().ToListAsync();

        // 3. 序列化并存入 Redis，设置绝对过期时间（3小时）
        var jsonTags = JsonSerializer.Serialize(tags);
        await _cache.StringSetAsync(CacheKey, jsonTags, TimeSpan.FromHours(3));

        return tags;
    }
    
    private async Task<List<Tag>> GetTagsFromMemory()
    {
        return await cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(3);

            var tags = await db.Tags.AsNoTracking().ToListAsync();

            return tags;
        }) ?? [];
    }

    public async Task<bool> AreTagsValidAsync(List<string> slugs)
    {
        // var tags = await GetTagsFromMemory();
        var tags = await GetTagsFromRedis();
        
        var tagSet = tags.Select(x => x.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return slugs.All(tagSet.Contains);
    }
}