using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace MafStatefulApi.State;

/// <summary>
/// In-memory implementation of agent session store using MemoryCache.
/// Suitable for development and single-instance deployments.
/// </summary>
public class InMemoryAgentSessionStore : IAgentSessionStore
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<InMemoryAgentSessionStore> _logger;
    private readonly TimeSpan _sessionTtl;
    private readonly ConcurrentDictionary<string, bool> _sessionKeys = new();

    public InMemoryAgentSessionStore(
        IMemoryCache cache,
        ILogger<InMemoryAgentSessionStore> logger,
        IConfiguration configuration)
    {
        _cache = cache;
        _logger = logger;
        var ttlMinutes = configuration.GetValue("SessionTtlMinutes", 30);
        _sessionTtl = TimeSpan.FromMinutes(ttlMinutes);
    }

    public Task<string?> GetAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        var key = GetKey(conversationId);
        var found = _cache.TryGetValue(key, out string? value);
        
        _logger.LogInformation(
            "InMemory cache {CacheHitOrMiss} for conversation {ConversationId}",
            found ? "hit" : "miss",
            conversationId);
        
        return Task.FromResult(value);
    }

    public Task SetAsync(string conversationId, string serializedThread, CancellationToken cancellationToken = default)
    {
        var key = GetKey(conversationId);
        var options = new MemoryCacheEntryOptions
        {
            SlidingExpiration = _sessionTtl
        };
        
        _cache.Set(key, serializedThread, options);
        _sessionKeys.TryAdd(conversationId, true);
        
        _logger.LogInformation(
            "InMemory cache stored session for conversation {ConversationId}, size: {SizeBytes} bytes",
            conversationId,
            serializedThread.Length);
        
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        var key = GetKey(conversationId);
        _cache.Remove(key);
        _sessionKeys.TryRemove(conversationId, out _);
        
        _logger.LogInformation(
            "InMemory cache deleted session for conversation {ConversationId}",
            conversationId);
        
        return Task.CompletedTask;
    }

    public Task<IEnumerable<string>> ListSessionsAsync(CancellationToken cancellationToken = default)
    {
        // Return only keys that still exist in cache
        var existingSessions = _sessionKeys.Keys
            .Where(id => _cache.TryGetValue(GetKey(id), out _))
            .ToList();
        
        _logger.LogInformation(
            "Found {Count} sessions in InMemory cache",
            existingSessions.Count);
        
        return Task.FromResult<IEnumerable<string>>(existingSessions);
    }

    private static string GetKey(string conversationId) => $"maf:sessions:{conversationId}";
}
