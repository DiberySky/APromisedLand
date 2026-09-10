using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MAFRagService.Startup.Extensions;

public sealed class OllamaHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _factory;
    public OllamaHealthCheck(IHttpClientFactory factory) => _factory = factory;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var http = _factory.CreateClient(HttpClientNames.Ollama);
            using var resp = await http.GetAsync("/api/tags", cancellationToken);
            return resp.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("Ollama 可达")
                : HealthCheckResult.Degraded($"Ollama HTTP {(int)resp.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Ollama 健康检查异常", ex);
        }
    }
}
