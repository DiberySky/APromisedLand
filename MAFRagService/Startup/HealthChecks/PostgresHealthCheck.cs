using APromisedLand.Api.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MAFRagService.Startup.HealthChecks;

public sealed class PostgresHealthCheck(MafRagContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var ok = await db.Database.CanConnectAsync(cancellationToken);
            return ok
                ? HealthCheckResult.Healthy("Postgres 连接正常")
                : HealthCheckResult.Unhealthy("Postgres 无法连接");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Postgres 健康检查异常", ex);
        }
    }
}
