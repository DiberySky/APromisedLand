using Microsoft.Extensions.AI;

namespace MafSampleApi.Services;

/// <summary>
/// 应用启动时异步发一个极短请求，触发 Ollama 加载模型到显存。
/// 让第一个真实用户请求不用等冷加载（37s → 100ms）。
/// </summary>
public sealed class OllamaWarmupService(
    IChatClient chatClient,
    ILogger<OllamaWarmupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // 稍等片刻，给 Ollama 容器一点启动时间
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3), ct);
        }
        catch (OperationCanceledException) { return; }

        try
        {
            logger.LogInformation("Ollama warmup: sending ping...");
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var response = await chatClient.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "hi")],
                new ChatOptions { MaxOutputTokens = 1 },
                ct);

            sw.Stop();
            logger.LogInformation(
                "Ollama warmup done in {Ms}ms", sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            // 应用关闭，忽略
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ollama warmup failed (non-fatal)");
        }
    }
}