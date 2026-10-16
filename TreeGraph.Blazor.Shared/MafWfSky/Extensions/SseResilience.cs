using Microsoft.Extensions.Http.Resilience;

namespace TreeGraph.Blazor.Shared.MafWfSky.Extensions;

/// <summary>
/// SSE 长连接专用弹性策略。与 <c>NonIdempotentResilience</c> 的区别：
/// 流式响应生命周期由流自身控制，任何"超时/重试"都会切断 SSE 流，必须全部关闭。
/// 只保留一只宽熔断器，用于后端长时间故障时快速失败，避免前端无限挂起。
/// </summary>
internal static class SseResilience
{
    /// <summary>配置 SSE 客户端：禁重试、禁超时、宽熔断。</summary>
    public static void Configure(HttpStandardResilienceOptions options)
    {
        // ★ 禁重试：SSE 流不能重放；MaxRetryAttempts 校验要求 ≥1，置 1 后用 ShouldHandle 恒 false。
        options.Retry.MaxRetryAttempts = 1;
        options.Retry.ShouldHandle = _ => ValueTask.FromResult(false);

        // ★ 禁超时：单次尝试与总请求都给极大窗口，超时交给 IAsyncEnumerable 的
        //   CancellationToken 与流自身（服务端 event:done / 客户端断开）控制。
        //   AttemptTimeout 同时充当熔断 SamplingDuration 的下限基数（校验要求 ≥ 2×AttemptTimeout）。
        var longWindow = TimeSpan.FromHours(2);
        options.AttemptTimeout.Timeout = longWindow;
        options.TotalRequestTimeout.Timeout = longWindow;

        // ★ 宽熔断：采样窗口必须 ≥ 2 × AttemptTimeout（内置校验）。
        //   对长连接而言熔断意义有限（失败率采样被拉长），仅作后端硬故障兜底。
        options.CircuitBreaker.SamplingDuration = longWindow * 2;
        options.CircuitBreaker.FailureRatio = 0.9;               // 90% 失败才断
        options.CircuitBreaker.MinimumThroughput = 5;            // 至少 5 个请求才开始统计
    }
}
