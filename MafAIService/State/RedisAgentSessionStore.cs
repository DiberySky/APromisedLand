using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace MafAIService.State
{
    /// <summary>
    /// Redis-based implementation of agent session store using IDistributedCache.
    /// Suitable for production and multi-instance deployments.
    /// </summary>
    public class RedisAgentSessionStore : IAgentSessionStore
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<RedisAgentSessionStore> _logger;
        private readonly TimeSpan _sessionTtl;
        private readonly IConnectionMultiplexer? _redis;

        public RedisAgentSessionStore(
            IDistributedCache cache,
            ILogger<RedisAgentSessionStore> logger,
            IConfiguration configuration,
            IConnectionMultiplexer? redis = null)
        {
            _cache = cache;
            _logger = logger;
            _redis = redis;
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
        
            _logger.LogInformation(
                "Redis cache stored session for conversation {ConversationId}, size: {SizeBytes} bytes",
                conversationId,
                serializedThread.Length);
        }

        public async Task DeleteAsync(string conversationId, CancellationToken cancellationToken = default)
        {
            var key = GetKey(conversationId);
            await _cache.RemoveAsync(key, cancellationToken);
        
            _logger.LogInformation(
                "Redis cache deleted session for conversation {ConversationId}",
                conversationId);
        }

        public async Task<IEnumerable<string>> ListSessionsAsync(CancellationToken cancellationToken = default)
        {
            if (_redis == null)
            {
                _logger.LogWarning("Redis connection not available for listing sessions");
                return [];
            }

            try
            {
                var db = _redis.GetDatabase();
                // Note: In a clustered or sentinel configuration, you may need to specify the server endpoint
                var server = _redis.GetServers().FirstOrDefault();
            
                if (server == null)
                {
                    _logger.LogWarning("No Redis server available");
                    return [];
                }

                var pattern = "maf:sessions:*";
                // Note: Keys() blocks Redis and should be replaced with SCAN for production with many keys
                var keys = server.Keys(pattern: pattern).ToList();
            
                var conversationIds = keys
                    .Select(key => key.ToString().Replace("maf:sessions:", ""))
                    .ToList();
            
                _logger.LogInformation(
                    "Found {Count} sessions in Redis",
                    conversationIds.Count);
            
                return conversationIds;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing sessions from Redis");
                return [];
            }
        }

        private static string GetKey(string conversationId) => $"maf:sessions:{conversationId}";
    }
}
