using Microsoft.Extensions.Caching.Distributed;
using TreeGraph.Shared.Identity;
using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.IdentityAdmin.Revocation;

/// <summary>基于 Redis 分布式缓存的撤销标记存储。</summary>
public sealed class RedisIdentityRevocationStore : IIdentityRevocationStore
{
    private const string KeyPrefix = IdentityConstants.RevocationKeyPrefix;
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    private readonly IDistributedCache _cache;

    public RedisIdentityRevocationStore(IDistributedCache cache) => _cache = cache;

    public Task MarkRevokedAsync(string subject, DateTimeOffset revokedAt, CancellationToken ct = default)
        => _cache.SetStringAsync(
            KeyPrefix + subject,
            revokedAt.ToUnixTimeSeconds().ToString(),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl },
            ct);

    public Task ClearRevokedAsync(string subject, CancellationToken ct = default)
        => _cache.RemoveAsync(KeyPrefix + subject, ct);

    public async Task<bool> IsRevokedAsync(string subject, CancellationToken ct = default)
    {
        var value = await _cache.GetStringAsync(KeyPrefix + subject, ct).ConfigureAwait(false);
        return !string.IsNullOrEmpty(value);
    }
}
