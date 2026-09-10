using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MAFRagService.Startup.HealthChecks;

public sealed class RedisHealthCheck(IDistributedCache cache) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var key = $"__health_{Guid.NewGuid():N}";
            await cache.SetStringAsync(key, "1",
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5) }, ct);
            var v = await cache.GetStringAsync(key, ct);
            return v == "1"
                ? HealthCheckResult.Healthy("Redis 读写正常")
                : HealthCheckResult.Degraded("Redis 写入成功但读取不匹配");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis 健康检查异常", ex);
        }
    }
}