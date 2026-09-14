using System.Text.Json;
using MAFWorkFlowApi.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace MAFWorkFlowApi.Agents;

/// <summary>
/// 基于 Redis (IDistributedCache) 的 AgentSessionStore 实现，
/// 同时实现 IConversationCatalog 以支持会话列举、删除与历史读取。
/// </summary>
public sealed class RedisAgentSessionStore : AgentSessionStore, IConversationCatalog
{
    private const int MaxConversationIdLength = 128;
    private const string CacheKeyPrefix = "maf:session:";

    private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan SlidingTtl = TimeSpan.FromHours(4);

    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer _multiplexer;
    private readonly ILogger<RedisAgentSessionStore> _logger;

    public RedisAgentSessionStore(
        IDistributedCache cache,
        IConnectionMultiplexer multiplexer,
        ILogger<RedisAgentSessionStore> logger)
    {
        _cache = cache;
        _multiplexer = multiplexer;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────
    // AgentSessionStore 抽象实现
    // ─────────────────────────────────────────────────────────────

    public override async ValueTask<AgentSession> GetSessionAsync(
        AIAgent agent,
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        ValidateConversationId(conversationId);
        var cacheKey = GetCacheKey(conversationId);

        _logger.LogDebug("Loading session from Redis: {ConversationId}", conversationId);

        byte[]? jsonBytes;
        try
        {
            jsonBytes = await _cache.GetAsync(cacheKey, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis 读取失败：{ConversationId}", conversationId);
            throw new InvalidOperationException(
                $"从 Redis 加载会话失败：{conversationId}", ex);
        }

        if (jsonBytes is null or { Length: 0 })
            return null!;

        try
        {
            using var document = JsonDocument.Parse(jsonBytes);
            return await agent.DeserializeSessionAsync(
                document.RootElement,
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "会话 JSON 损坏，已清理并回退为新会话：{ConversationId}",
                conversationId);
            await TryRemoveAsync(cacheKey, cancellationToken);
            return null!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "会话反序列化失败：{ConversationId}", conversationId);
            throw;
        }
    }

    public override async ValueTask SaveSessionAsync(
        AIAgent agent,
        string conversationId,
        AgentSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ValidateConversationId(conversationId);

        var cacheKey = GetCacheKey(conversationId);

        var jsonElement = await agent.SerializeSessionAsync(
            session, cancellationToken: cancellationToken);
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(jsonElement);

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = SessionTtl,
            SlidingExpiration = SlidingTtl,
        };

        try
        {
            await _cache.SetAsync(cacheKey, jsonBytes, options, cancellationToken);
            _logger.LogDebug(
                "Session saved successfully: {ConversationId} ({Size} bytes)",
                conversationId, jsonBytes.Length);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis 写入失败：{ConversationId}", conversationId);
            throw new InvalidOperationException(
                $"保存会话到 Redis 失败：{conversationId}", ex);
        }
    }

    public override async ValueTask DeleteSessionAsync(
        AIAgent agent,
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        ValidateConversationId(conversationId);
        await TryRemoveAsync(GetCacheKey(conversationId), cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────
    // IConversationCatalog 实现
    // ─────────────────────────────────────────────────────────────

    public async ValueTask<IReadOnlyList<string>> ListConversationIdsAsync(
        CancellationToken cancellationToken = default)
    {
        var result = new List<string>();

        var server = _multiplexer.GetServers().FirstOrDefault();
        if (server is null)
        {
            _logger.LogWarning("未找到可用的 Redis 服务器连接，无法列举会话。");
            return result;
        }

        try
        {
            var pattern = $"{CacheKeyPrefix}*";
            await foreach (var key in server
                .KeysAsync(pattern: pattern, pageSize: 250)
                .WithCancellation(cancellationToken))
            {
                var raw = (string?)key;
                if (string.IsNullOrEmpty(raw)) continue;

                var id = raw.StartsWith(CacheKeyPrefix, StringComparison.Ordinal)
                    ? raw[CacheKeyPrefix.Length..]
                    : raw;

                if (!string.IsNullOrEmpty(id))
                    result.Add(id);
            }

            result.Sort(StringComparer.Ordinal);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "列举 Redis 会话失败");
            throw new InvalidOperationException("列举会话失败", ex);
        }

        return result;
    }

    public async ValueTask<bool> DeleteConversationAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        ValidateConversationId(conversationId);
        var cacheKey = GetCacheKey(conversationId);

        try
        {
            var existed = await _cache.GetAsync(cacheKey, cancellationToken) is not null;
            await _cache.RemoveAsync(cacheKey, cancellationToken);
            return existed;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除 Redis 会话失败：{ConversationId}", conversationId);
            throw new InvalidOperationException(
                $"删除会话失败：{conversationId}", ex);
        }
    }

    /// <summary>
    /// 从已持久化的会话 JSON 中提取消息列表（用于历史回放）。
    /// 兼容 MAF 序列化格式的常见字段命名，缺失时返回空列表。
    /// </summary>
    public async ValueTask<IReadOnlyList<SessionMessage>> GetSessionMessagesAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        ValidateConversationId(conversationId);
        var cacheKey = GetCacheKey(conversationId);

        byte[]? jsonBytes;
        try
        {
            jsonBytes = await _cache.GetAsync(cacheKey, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis 读取失败：{ConversationId}", conversationId);
            throw new InvalidOperationException(
                $"从 Redis 读取会话失败：{conversationId}", ex);
        }

        if (jsonBytes is null or { Length: 0 })
            return [];

        try
        {
            using var document = JsonDocument.Parse(jsonBytes);
            var root = document.RootElement;

            // 兼容大小写：找 "messages" 或 "Messages"
            if (!TryGetPropertyIgnoreCase(root, "messages", out var messagesEl) ||
                messagesEl.ValueKind != JsonValueKind.Array)
                return [];

            var result = new List<SessionMessage>();
            foreach (var msg in messagesEl.EnumerateArray())
            {
                var role = TryGetStringIgnoreCase(msg, "role") ?? "unknown";
                var text = TryGetStringIgnoreCase(msg, "text") ?? string.Empty;
                var author = TryGetStringIgnoreCase(msg, "authorName");

                if (!string.IsNullOrWhiteSpace(text))
                {
                    result.Add(new SessionMessage(
                        Role: role,
                        Text: text,
                        AuthorName: author));
                }
            }

            return result;
        }
        catch (OperationCanceledException) { throw; }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "会话 JSON 无法解析，返回空消息列表：{ConversationId}", conversationId);
            return [];
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 私有辅助
    // ─────────────────────────────────────────────────────────────

    private async Task TryRemoveAsync(string cacheKey, CancellationToken ct)
    {
        try
        {
            await _cache.RemoveAsync(cacheKey, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "清理 Redis 缓存失败：{CacheKey}", cacheKey);
        }
    }

    private static bool TryGetPropertyIgnoreCase(
        JsonElement element, string name, out JsonElement value)
    {
        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private static string? TryGetStringIgnoreCase(JsonElement element, string name)
    {
        if (!TryGetPropertyIgnoreCase(element, name, out var prop))
            return null;
        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : null;
    }

    private static void ValidateConversationId(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            throw new ArgumentException("会话 ID 不能为空。", nameof(conversationId));

        if (conversationId.Length > MaxConversationIdLength)
            throw new ArgumentException(
                $"会话 ID 长度超过限制（最多 {MaxConversationIdLength} 字符）。",
                nameof(conversationId));
    }

    private static string GetCacheKey(string conversationId)
        => $"{CacheKeyPrefix}{conversationId}";
}