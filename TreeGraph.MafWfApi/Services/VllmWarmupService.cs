using Microsoft.Extensions.AI;

namespace TreeGraph.MafWfApi.Services;

/// <summary>
/// 应用启动时异步发一个极短请求，触发 vLLM 加载模型到显存。
/// 让第一个真实用户请求不用等冷加载。
///
/// ★ 带重试：vLLM 容器进入 Running 时模型权重仍在异步加载（30s~数分钟），
///   单次 ping 大概率撞在加载窗口上。这里按退避间隔重试，直到成功
///   或超过总预算（默认 6 分钟，覆盖最慢的冷加载场景）。
/// </summary>
public sealed class VllmWarmupService(
    IChatClient chatClient,
    ILogger<VllmWarmupService> logger) : BackgroundService
{
    /// <summary>预热总预算：超过即放弃（非致命，只是首个请求偏慢）。</summary>
    private static readonly TimeSpan TotalBudget = TimeSpan.FromMinutes(6);

    /// <summary>首次 ping 前的启动延迟。</summary>
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(2);

    /// <summary>重试间隔：15s → 30s → 45s → 60s 封顶（线性增长）。</summary>
    private static readonly TimeSpan InitialRetryInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan MaxRetryInterval = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(InitialDelay, ct);
        }
        catch (OperationCanceledException) { return; }

        var deadline = DateTimeOffset.UtcNow + TotalBudget;
        var retryInterval = InitialRetryInterval;
        var totalSw = System.Diagnostics.Stopwatch.StartNew();
        var attempt = 0;

        while (true)
        {
            attempt++;
            var attemptSw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                logger.LogInformation("vLLM warmup: ping (attempt {Attempt})...", attempt);

                await chatClient.GetResponseAsync(
                    [new ChatMessage(ChatRole.User, "hi /no_think")],
                    new ChatOptions { MaxOutputTokens = 1 },
                    ct);

                logger.LogInformation(
                    "vLLM warmup done in {Ms}ms (attempt {Attempt}, total {TotalMs}ms) — 模型已就绪",
                    attemptSw.ElapsedMilliseconds, attempt, totalSw.ElapsedMilliseconds);
                return;
            }
            catch (OperationCanceledException)
            {
                // 应用关闭，静默退出
                return;
            }
            catch (Exception ex)
            {
                if (DateTimeOffset.UtcNow + retryInterval >= deadline)
                {
                    logger.LogWarning(ex,
                        "vLLM warmup gave up after {Attempt} attempt(s) / {TotalSec:F0}s (non-fatal)；" +
                        "模型可能仍在加载，首个真实请求会偏慢。可用 GET /api/health/deep 确认就绪状态",
                        attempt, totalSw.Elapsed.TotalSeconds);
                    return;
                }

                logger.LogInformation(
                    "vLLM not ready yet ({Reason})，{RetrySec:F0}s 后重试 (attempt {Attempt})",
                    ex.Message, retryInterval.TotalSeconds, attempt);
            }

            try
            {
                await Task.Delay(retryInterval, ct);
            }
            catch (OperationCanceledException) { return; }

            // 线性退避：15s, 30s, 45s, 60s, 60s, ...
            retryInterval = retryInterval + InitialRetryInterval;
            if (retryInterval > MaxRetryInterval)
                retryInterval = MaxRetryInterval;
        }
    }
}
