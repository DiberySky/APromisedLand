// RemoveAllResilienceHandlers 为实验性 API（EXTEXP0001），用于移除宿主全局默认弹性，
// 与本客户端的禁重试/禁超时策略叠加会冲突。仓库内 Maui/TreeGraph.Blazor 同款用法。
#pragma warning disable EXTEXP0001
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using TreeGraph.Blazor.Shared.MafWfSky.Services;

namespace TreeGraph.Blazor.Shared.MafWfSky.Extensions;

/// <summary>MafWfSky（MafWfApi 客户端 + SSE 读取器）注册。</summary>
public static class MafWfSkyServiceCollectionExtensions
{
    /// <summary>
    /// 以 Aspire 服务发现名注册。
    /// </summary>
    /// <param name="serviceName">
    /// AppHost 中 <c>AddProject&lt;Projects.TreeGraph_MafWfApi&gt;</c> 的资源名（默认
    /// <c>TreeGraphMafWfApi</c>，服务发现 URL 为 <c>https+http://treegraphmafwfapi</c>）。
    /// 本地脱离 Aspire 调试时可传完整基址（如 <c>http://localhost:5323</c>）。
    /// </param>
    public static IServiceCollection AddMafWfSky(
        this IServiceCollection services,
        string serviceName = "TreeGraphMafWfApi",
        Action<MafWfSkyOptions>? configure = null)
    {
        var options = new MafWfSkyOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        var baseAddress = serviceName.Contains("://", StringComparison.Ordinal)
            ? serviceName.TrimEnd('/') + "/"
            : $"https+http://{serviceName.ToLowerInvariant()}/";

        services.AddHttpClient<MafWfApiClient>(client =>
        {
            client.BaseAddress = new Uri(baseAddress);
            client.Timeout = TimeSpan.FromMinutes(10);
        })
        // ★ 先移除宿主 AddServiceDefaults 挂的全局默认弹性（10s 超时 + 3 次重试），
        //   否则与本客户端策略叠加，默认 10s 先触发，对话/工作流一律超时。
        .RemoveAllResilienceHandlers()
        // 非流式调用：禁重试（对话/工作流均不幂等），超时走 AttemptTimeout，
        // 放宽到 5 分钟（本地 vLLM 节点展开/Writer-Critic 全程可能数分钟，由 TotalRequestTimeout 兜底）。
        .AddStandardResilienceHandler(o =>
        {
            o.Retry.MaxRetryAttempts = 1;
            o.Retry.ShouldHandle = _ => ValueTask.FromResult(false);
            o.AttemptTimeout.Timeout = TimeSpan.FromMinutes(5);
            o.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(10);
            o.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(11); // 校验要求 ≥ 2× AttemptTimeout
        });

        // SSE 长连接：专用策略（禁重试/禁超时，仅宽熔断），见 SseResilience。
        services.AddHttpClient<MafWfSseReader>(client =>
        {
            client.BaseAddress = new Uri(baseAddress);
            client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
        })
        // 同样先移除全局默认弹性，再挂 SSE 专用策略。
        .RemoveAllResilienceHandlers()
        .AddStandardResilienceHandler(SseResilience.Configure);

        return services;
    }
}
