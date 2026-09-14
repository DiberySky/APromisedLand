using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace MafStatefulApi.State;

/// <summary>
/// Redis-based implementation of agent session store using IDistributedCache + IConnectionMultiplexer.
/// Suitable for production and multi-instance deployments.
///
/// 会话列表通过 Redis Set 索引维护，避免使用 KEYS/SCAN 阻塞 Redis 服务端。
/// </summary>
public class RedisAgentSessionStore : IAgentSessionStore
{
    /// <summary>会话索引键。所有活跃会话 ID 的集合。</summary>
    private const string IndexKey = "maf:sessions:index";

    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisAgentSessionStore> _logger;
    private readonly TimeSpan _sessionTtl;

    public RedisAgentSessionStore(
        IDistributedCache cache,
        IConnectionMultiplexer redis,
        ILogger<RedisAgentSessionStore> logger,
        IConfiguration configuration)
    {
        _cache = cache;
        _redis = redis;
        _logger = logger;
        var ttlMinutes = configuration.GetValue("SessionTtlMinutes", 30);
        _sessionTtl = TimeSpan.FromMinutes(ttlMinutes);
    }

    public async Task<string?> GetAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        var key = GetKey(conversationId);
        var value = await _cache.GetStringAsync(key, cancellationToken);

        _logger.LogInformation(
            "Redis cache {CacheHitOrMiss} for conversation {ConversationId}",
            value != null ? "hit" : "miss",
            conversationId);

        return value;
    }

    public async Task SetAsync(string conversationId, string serializedThread, CancellationToken cancellationToken = default)
    {
        var key = GetKey(conversationId);
        var options = new DistributedCacheEntryOptions
        {
            SlidingExpiration = _sessionTtl
        };

        await _cache.SetStringAsync(key, serializedThread, options, cancellationToken);

        // 维护索引：把 conversationId 加入 Set（幂等）
        var db = _redis.GetDatabase();
        await db.SetAddAsync(IndexKey, conversationId);

        _logger.LogInformation(
            "Redis cache stored session for conversation {ConversationId}, size: {SizeBytes} bytes",
            conversationId,
            serializedThread.Length);
    }

    public async Task DeleteAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        var key = GetKey(conversationId);
        await _cache.RemoveAsync(key, cancellationToken);

        // 同步从索引中移除
        var db = _redis.GetDatabase();
        await db.SetRemoveAsync(IndexKey, conversationId);

        _logger.LogInformation(
            "Redis cache deleted session for conversation {ConversationId}",
            conversationId);
    }

    public async Task<IEnumerable<string>> ListSessionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var members = await db.SetMembersAsync(IndexKey);

            if (members.Length == 0)
            {
                _logger.LogInformation("Found 0 sessions in Redis");
                return [];
            }

            // 过滤掉已过期但未从索引中清理的会话；
            // 顺带把陈旧条目 SREM，避免索引无限增长。
            var existing = new List<string>(members.Length);
            var stale = new List<RedisValue>();

            foreach (var member in members)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var conversationId = member.ToString();
                if (await db.KeyExistsAsync(GetKey(conversationId)))
                {
                    existing.Add(conversationId);
                }
                else
                {
                    stale.Add(member);
                }
            }

            if (stale.Count > 0)
            {
                await db.SetRemoveAsync(IndexKey, stale.ToArray());
                _logger.LogDebug("Cleaned {Count} stale session entries from index", stale.Count);
            }

            _logger.LogInformation("Found {Count} sessions in Redis", existing.Count);
            return existing;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing sessions from Redis");
            return [];
        }
    }

    private static string GetKey(string conversationId) => $"maf:sessions:{conversationId}";
}