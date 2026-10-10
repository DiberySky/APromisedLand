using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace TreeGraph.Blazor.Infrastructure;

/// <summary>
/// 针对指向 <c>treegrapheavapi</c> 的非幂等写操作，提供统一的 HTTP 弹性策略配置。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么禁止自动重试。</b>
/// 管理台的 PUT / POST 操作不幂等。例如 <c>recalculate-factor</c> 被重放会导致值被平方调整，
/// 属于数据损坏。因此在 <see cref="Configure"/> 中显式关闭重试。
/// </para>
/// <para>
/// <b>为什么 <c>MaxRetryAttempts = 1</c> 而不是 0。</b>
/// <see cref="HttpStandardResilienceOptions"/> 内置校验要求 <c>MaxRetryAttempts &gt;= 1</c>，
/// 传 0 会在启动时抛 <see cref="OptionsValidationException"/>。
/// 因此置 1 通过校验，再用 <c>ShouldHandle</c> 恒 <c>false</c> 让重试永不触发。
/// </para>
/// <para>
/// <b>为什么采样窗口用乘法表达式。</b>
/// 熔断器内置校验要求 <c>SamplingDuration &gt;= 2 × AttemptTimeout</c>。
/// 写成 <c>AttemptTimeout * 2</c> 而不是字面量，可以避免将来修改 <see cref="AttemptTimeout"/>
/// 时忘记同步 <see cref="CircuitBreakerSamplingDuration"/>，导致启动时抛
/// <see cref="OptionsValidationException"/>。
/// </para>
/// <para>
/// <b>为什么两个客户端共用同一套参数。</b>
/// TreeEavSky 客户端与 <c>EavApiClient</c> 访问的是同一个后端。
/// 若超时参数不一致，同一后端操作在不同客户端上的行为会不同，
/// 排查问题时容易误判。统一后行为可预期。
/// </para>
/// <para>
/// <b>为什么不使用全局 <c>ConfigureHttpClientDefaults</c>。</b>
/// <see cref="Microsoft.Extensions.DependencyInjection.ServiceCollectionExtensions"/> 中的
/// <c>AddServiceDefaults</c> 已经注册了一次全局默认弹性策略，
/// 覆盖了健康检查探针、OTel 导出器等客户端。这些客户端的语义与"非幂等写操作"完全不同，
/// 不应共享同一套策略。因此本类只作为 <c>AddStandardResilienceHandler</c> 的回调使用。
/// </para>
/// </remarks>
internal static class NonIdempotentResilience
{
    /// <summary>
    /// 单次 HTTP 尝试的超时。允许后端执行一次重算等较慢操作。
    /// </summary>
    public static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 包含所有重试与退避在内的总请求超时。
    /// 由于重试已关闭，此值相当于对单次尝试的硬上限。
    /// </summary>
    public static readonly TimeSpan TotalRequestTimeout = TimeSpan.FromSeconds(120);

    /// <summary>
    /// 熔断器的采样窗口。必须 ≥ <see cref="AttemptTimeout"/> 的两倍，
    /// 否则启动时内置校验会失败。用乘法表达式保持联动。
    /// </summary>
    public static readonly TimeSpan CircuitBreakerSamplingDuration = AttemptTimeout * 2;

    /// <summary>
    /// 将统一的弹性策略应用到 <see cref="HttpStandardResilienceOptions"/>。
    /// 作为 <c>AddStandardResilienceHandler</c> 的回调传入。
    /// </summary>
    /// <param name="options">由 <c>AddStandardResilienceHandler</c> 提供的配置对象。</param>
    public static void Configure(HttpStandardResilienceOptions options)
    {
        // ★ 禁止重试：
        //   管理台 PUT/POST 不幂等（如 recalculate-factor 重放会导致数据损坏）。
        //   MaxRetryAttempts 校验约束为 1–int.MaxValue（不接受 0），
        //   因此置 1 通过校验，再用 ShouldHandle 恒 false 让重试永不触发。
        options.Retry.MaxRetryAttempts = 1;
        options.Retry.ShouldHandle = _ => ValueTask.FromResult(false);

        // ★ 超时：所有指向 treegrapheavapi 的客户端共用同一套参数。
        options.AttemptTimeout.Timeout = AttemptTimeout;
        options.TotalRequestTimeout.Timeout = TotalRequestTimeout;

        // ★ 熔断器采样窗口必须 ≥ 2 × AttemptTimeout（内置校验）。
        //   使用乘法表达式而非字面量，避免改超时时忘记同步。
        options.CircuitBreaker.SamplingDuration = CircuitBreakerSamplingDuration;
    }
}
