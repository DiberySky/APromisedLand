using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.Caching.Distributed;

namespace MAFWorkFlowApi.Agents;

/// <summary>
/// 基于 Redis (IDistributedCache) 的 AgentSessionStore 实现。
/// 将会话状态序列化为 JSON 后存入 Redis，实现持久化多轮会话。
/// </summary>
public sealed class RedisAgentSessionStore : AgentSessionStore
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisAgentSessionStore> _logger;

    /// <summary>会话绝对过期时间。</summary>
    private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(24);

    public RedisAgentSessionStore(
        IDistributedCache cache,
        ILogger<RedisAgentSessionStore> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// 从 Redis 加载会话。如果不存在或反序列化失败，返回 null。
    /// </summary>
    public override async ValueTask<AgentSession> GetSessionAsync(
        AIAgent agent,
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(conversationId);

        _logger.LogDebug("Loading session from Redis: {CacheKey}", cacheKey);

        var jsonBytes = await _cache.GetAsync(cacheKey, cancellationToken);
        if (jsonBytes is null or { Length: 0 })
        {
            _logger.LogDebug(
                "Session not found in Redis: {ConversationId}", conversationId);
            return null!;
        }

        try
        {
            using var document = JsonDocument.Parse(jsonBytes);
            var session = await agent.DeserializeSessionAsync(
                document.RootElement,
                cancellationToken: cancellationToken);

            _logger.LogDebug(
                "Session loaded successfully: {ConversationId}", conversationId);
            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to deserialize session: {ConversationId}", conversationId);
            return null!;
        }
    }

    /// <summary>
    /// 将会话序列化后保存到 Redis。
    /// </summary>
    public override async ValueTask SaveSessionAsync(
        AIAgent agent,
        string conversationId,
        AgentSession session,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(conversationId);

        _logger.LogDebug("Saving session to Redis: {CacheKey}", cacheKey);

        var jsonElement = await agent.SerializeSessionAsync(
            session,
            cancellationToken: cancellationToken);

        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(jsonElement);

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = SessionTtl
        };

        await _cache.SetAsync(cacheKey, jsonBytes, options, cancellationToken);

        _logger.LogDebug(
            "Session saved successfully: {ConversationId}", conversationId);
    }

    /// <summary>
    /// 从 Redis 中删除指定的会话。
    /// </summary>
    public override async ValueTask DeleteSessionAsync(
        AIAgent agent,
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(conversationId);

        _logger.LogDebug("Deleting session from Redis: {CacheKey}", cacheKey);

        await _cache.RemoveAsync(cacheKey, cancellationToken);

        _logger.LogDebug(
            "Session deleted successfully: {ConversationId}", conversationId);
    }

    /// <summary>
    /// 生成 Redis 缓存键。
    /// </summary>
    private static string GetCacheKey(string conversationId)
        => $"maf:session:{conversationId}";
}