using MAFRagService.Startup.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MAFRagService.Startup.HealthChecks;

public sealed class WeaviateHealthCheck(IHttpClientFactory factory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var http = factory.CreateClient(HttpClientNames.Weaviate);
            using var resp = await http.GetAsync("/v1/.well-known/ready", ct);
            return resp.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("Weaviate ready")
                : HealthCheckResult.Degraded($"Weaviate HTTP {(int)resp.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Weaviate 健康检查异常", ex);
        }
    }
}