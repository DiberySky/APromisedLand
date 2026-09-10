using System.Text.Json;
using MAFRagService.Initializers;
using MAFRagService.Startup.Configuration;
using Microsoft.Extensions.Options;

namespace MAFRagService.Startup.Extensions;

public static class StartupInitializers
{
    /// <summary>
    /// 启动初始化：
    ///   1. Database（必须先，供其他步骤使用）
    ///   2. SeaweedFS / Weaviate / Nebula 三者并行（互不依赖，各自独立 scope）
    ///   3. Ollama 模型可用性检查（仅告警）
    /// </summary>
    public static async Task RunAsync(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        var ct     = CancellationToken.None;

        try
        {
            // ---------- Step 1: Database ----------
            await StepAsync(app, logger, "Database",
                i => i.GetRequiredService<DatabaseInitializer>().InitializeAsync(ct));

            // ---------- Step 2: 并行初始化 ----------
            var parallel = new[]
            {
                StepAsync(app, logger, "SeaweedFS Bucket",
                    i => i.GetRequiredService<SeaweedBucketInitializer>().InitializeAsync(ct)),
                StepAsync(app, logger, "Weaviate Schema",
                    i => i.GetRequiredService<WeaviateSchemaInitializer>().InitializeAsync(ct)),
                StepAsync(app, logger, "NebulaGraph Schema",
                    i => i.GetRequiredService<GraphSchemaInitializer>().InitializeAsync(ct))
            };
            await Task.WhenAll(parallel);

            // ---------- Step 3: Ollama 检查 ----------
            await CheckOllamaAsync(app, logger, ct);

            logger.LogInformation("全部初始化完成。");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "应用初始化失败，终止启动。");
            throw;
        }
    }

    private static async Task StepAsync(
        WebApplication app,
        ILogger logger,
        string label,
        Func<IServiceProvider, Task> body)
    {
        using var scope = app.Services.CreateScope();
        logger.LogInformation("开始初始化 {Step}...", label);
        await body(scope.ServiceProvider);
        logger.LogInformation("{Step} 完成。", label);
    }

    private static async Task CheckOllamaAsync(
        WebApplication app,
        ILogger logger,
        CancellationToken ct)
    {
        var opt = app.Services.GetRequiredService<IOptions<OllamaOptions>>().Value;
        logger.LogInformation("开始校验 Ollama 模型...");

        try
        {
            var http = app.Services.GetRequiredService<IHttpClientFactory>()
                          .CreateClient(HttpClientNames.Ollama);

            using var resp = await http.GetAsync("/api/tags", ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Ollama /api/tags 返回 {Status}", resp.StatusCode);
                return;
            }

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (!doc.RootElement.TryGetProperty("models", out var models))
            {
                logger.LogWarning("Ollama /api/tags 响应中缺少 models 字段。");
                return;
            }

            var names = models.EnumerateArray()
                .Select(m => m.TryGetProperty("name", out var n) ? n.GetString() : null)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToArray();

            static bool HasModel(string[] names, string target) =>
                names.Any(n => n.Equals(target, StringComparison.OrdinalIgnoreCase)
                            || n.StartsWith(target + ":", StringComparison.OrdinalIgnoreCase)
                            || n.StartsWith(target + "-", StringComparison.OrdinalIgnoreCase));

            bool hasChat  = HasModel(names, opt.ChatModel);
            bool hasEmbed = HasModel(names, opt.EmbeddingModel);

            logger.LogInformation(
                "Ollama 模型检查：chat({Chat})={HasChat}, embed({Embed})={HasEmbed}",
                opt.ChatModel, hasChat, opt.EmbeddingModel, hasEmbed);

            if (!hasChat || !hasEmbed)
            {
                logger.LogWarning(
                    "Ollama 缺少模型：chat={Chat}({HasChat}), embed={Embed}({HasEmbed})。" +
                    "请在容器内执行 `ollama pull`。",
                    opt.ChatModel, hasChat, opt.EmbeddingModel, hasEmbed);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ollama 健康检查失败（不阻断启动）。");
        }
    }
}
