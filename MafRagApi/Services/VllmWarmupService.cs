using Microsoft.Extensions.AI;

namespace MafRagApi.Services;

/// <summary>
/// 应用启动时异步发一个极短请求，触发 vLLM 加载模型到显存。
/// 让第一个真实用户请求不用等冷加载。
/// </summary>
public sealed class VllmWarmupService(
    IChatClient chatClient,
    ILogger<VllmWarmupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // vLLM 容器健康检查通过后引擎已就绪，这里再等一会让显存分配稳定
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }
        catch (OperationCanceledException) { return; }

        try
        {
            logger.LogInformation("vLLM warmup: sending ping...");
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var response = await chatClient.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "hi /no_think")],
                new ChatOptions { MaxOutputTokens = 1 },
                ct);

            sw.Stop();
            logger.LogInformation(
                "vLLM warmup done in {Ms}ms", sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            // 应用关闭，忽略
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "vLLM warmup failed (non-fatal)");
        }
    }
}