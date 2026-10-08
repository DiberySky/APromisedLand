using Microsoft.Extensions.Http.Resilience;

namespace TreeGraph.Blazor.Maui.Infrastructure;

/// <summary>
/// 针对指向 <c>treegrapheavapi</c> 的非幂等写操作，提供统一的 HTTP 弹性策略配置。
/// 与 Blazor 宿主的 NonIdempotentResilience 保持相同参数，确保同一后端行为一致。
/// </summary>
internal static class NonIdempotentResilience
{
    public static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan TotalRequestTimeout = TimeSpan.FromSeconds(120);
    public static readonly TimeSpan CircuitBreakerSamplingDuration = AttemptTimeout * 2;

    public static void Configure(HttpStandardResilienceOptions options)
    {
        options.Retry.MaxRetryAttempts = 1;
        options.Retry.ShouldHandle = _ => ValueTask.FromResult(false);
        options.AttemptTimeout.Timeout = AttemptTimeout;
        options.TotalRequestTimeout.Timeout = TotalRequestTimeout;
        options.CircuitBreaker.SamplingDuration = CircuitBreakerSamplingDuration;
    }
}
