using Microsoft.Extensions.Http.Resilience;
using TreeGraph.Blazor.Shared.MafWfSky.Extensions;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.MafWfSky;

/// <summary>SseResilience.Configure 策略断言。</summary>
public class SseResilienceTests
{
    private static HttpStandardResilienceOptions Configured()
    {
        var options = new HttpStandardResilienceOptions();
        SseResilience.Configure(options);
        return options;
    }

    [Fact]
    public void Configure_禁用重试_通过校验的最小次数()
    {
        var o = Configured();

        // MaxRetryAttempts 校验要求 ≥1，置 1 过校验
        Assert.Equal(1, o.Retry.MaxRetryAttempts);
    }

    [Fact]
    public async Task Configure_禁用重试_ShouldHandle恒False()
    {
        var o = Configured();

        // 重试永不触发（SSE 流不能重放）
        Assert.False(await o.Retry.ShouldHandle(default!));
    }

    [Fact]
    public void Configure_超时给极大窗口()
    {
        var o = Configured();

        // 长连接超时交给流自身生命周期，HTTP 层给 2h 极大窗口
        Assert.Equal(TimeSpan.FromHours(2), o.AttemptTimeout.Timeout);
        Assert.Equal(TimeSpan.FromHours(2), o.TotalRequestTimeout.Timeout);
    }

    [Fact]
    public void Configure_熔断采样窗口满足内置校验()
    {
        var o = Configured();

        // 内置校验：SamplingDuration ≥ 2 × AttemptTimeout
        Assert.True(o.CircuitBreaker.SamplingDuration >= o.AttemptTimeout.Timeout * 2,
            $"SamplingDuration {o.CircuitBreaker.SamplingDuration} 应 ≥ 2 × AttemptTimeout {o.AttemptTimeout.Timeout * 2}");
    }

    [Fact]
    public void Configure_熔断宽松_90失败率_最少5请求()
    {
        var o = Configured();

        Assert.Equal(0.9, o.CircuitBreaker.FailureRatio);
        Assert.Equal(5, o.CircuitBreaker.MinimumThroughput);
    }
}
