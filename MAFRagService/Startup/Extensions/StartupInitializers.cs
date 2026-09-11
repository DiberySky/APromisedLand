using System.Text.Json;
using MAFRagService.Initializers;
using MAFRagService.Startup.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace MAFRagService.Startup.Extensions;

/// <summary>
/// 启动初始化管线。
///
/// <para><b>执行顺序：</b></para>
/// <list type="number">
///   <item>Database —— 必须先，供其他步骤使用；</item>
///   <item>SeaweedFS / Weaviate / Nebula 三者并行（按 FeatureFlags 条件执行）；</item>
///   <item>Ollama 模型可用性检查（仅告警，不阻断）；</item>
///   <item>健康预检（tags=ready，带独立超时）。</item>
/// </list>
/// </summary>
public static class StartupInitializers
{
    /// <summary>
    /// 健康预检总超时。任一检查挂起时，启动在这个时间内失败，避免无限等待。
    /// 可通过环境变量 STARTUP_PREFLIGHT_TIMEOUT 覆盖（格式："00:01:00"）。
    /// </summary>
    private static readonly TimeSpan PreflightTimeout = ResolvePreflightTimeout();

    private static TimeSpan ResolvePreflightTimeout()
    {
        var raw = Environment.GetEnvironmentVariable("STARTUP_PREFLIGHT_TIMEOUT");
        if (!string.IsNullOrWhiteSpace(raw) &&
            TimeSpan.TryParse(raw, out var parsed) &&
            parsed > TimeSpan.Zero)
        {
            return parsed;
        }
        return TimeSpan.FromSeconds(30);
    }

    public static async Task RunAsync(this WebApplication app)
    {
        var logger   = app.Services.GetRequiredService<ILogger<Program>>();
        var features = app.Services.GetRequiredService<FeatureFlags>();
        var ct       = CancellationToken.None;

        try
        {
            // ---------- Step 1: Database ----------
            await StepAsync(app, logger, "Database",
                i => i.GetRequiredService<DatabaseInitializer>().InitializeAsync(ct));

            // ---------- Step 2: 按开关注册的并行初始化 ----------
            var parallel = new List<Task>();

            if (features.Rag || features.Indexing)
            {
                parallel.Add(StepAsync(app, logger, "SeaweedFS Bucket",
                    i => i.GetRequiredService<SeaweedBucketInitializer>().InitializeAsync(ct)));
            }

            if (features.Rag)
            {
                parallel.Add(StepAsync(app, logger, "Weaviate Schema",
                    i => i.GetRequiredService<WeaviateSchemaInitializer>().InitializeAsync(ct)));
            }

            if (features.Graph)
            {
                parallel.Add(StepAsync(app, logger, "NebulaGraph Schema",
                    i => i.GetRequiredService<GraphSchemaInitializer>().InitializeAsync(ct)));
            }

            await Task.WhenAll(parallel);

            // ---------- Step 3: Ollama 检查 ----------
            if (features.Rag || features.Entity)
                await CheckOllamaAsync(app, logger, ct);
            else
                logger.LogInformation("跳过 Ollama 检查（Rag 与 Entity 均关闭）。");

            // ---------- Step 4: 健康预检 ----------
            await PreflightHealthAsync(app, logger, ct);

            logger.LogInformation(
                "全部初始化完成。Features: Rag={Rag}, Graph={Graph}, Entity={Entity}, Indexing={Indexing}",
                features.Rag, features.Graph, features.Entity, features.Indexing);
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

    // ============================================================
    // 健康预检
    //   · 独立超时（PreflightTimeout），避免某个检查挂起阻塞启动。
    //   · 超时/异常转换为清晰消息，指明可能挂起的组件。
    //   · 只把 Unhealthy 当致命；Degraded 视为告警继续启动。
    //   · 日志按状态分级：Unhealthy → Error，Degraded → Warning。
    // ============================================================
    private static async Task PreflightHealthAsync(
        WebApplication app, ILogger logger, CancellationToken ct)
    {
        var health = app.Services.GetRequiredService<HealthCheckService>();

        logger.LogInformation(
            "开始健康预检（tags=ready, timeout={Timeout}s）...",
            PreflightTimeout.TotalSeconds);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linkedCts.CancelAfter(PreflightTimeout);

        HealthReport report;
        try
        {
            report = await health.CheckHealthAsync(
                r => r.Tags.Contains("ready"),
                linkedCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // 外部 ct 未取消，说明是预检自身超时
            logger.LogError(
                "健康预检在 {Timeout}s 内未完成，可能以下组件挂起：postgres / redis / weaviate / nebula。" +
                "请检查容器状态与连接串，或通过 STARTUP_PREFLIGHT_TIMEOUT 调大超时。",
                PreflightTimeout.TotalSeconds);

            throw new TimeoutException(
                $"健康预检超时（>{PreflightTimeout.TotalSeconds:F0}s）。");
        }

        // 逐条打印
        foreach (var entry in report.Entries)
        {
            var line = $"  {entry.Key}: {entry.Value.Status} " +
                       $"({entry.Value.Duration.TotalMilliseconds:F0}ms)";

            if (!string.IsNullOrWhiteSpace(entry.Value.Description))
                line += $" - {entry.Value.Description}";

            // Unhealthy 用 Error 级别，Degraded 用 Warning，Healthy 用 Information
            switch (entry.Value.Status)
            {
                case HealthStatus.Unhealthy:
                    logger.LogError("{Line}", line);
                    break;
                case HealthStatus.Degraded:
                    logger.LogWarning("{Line}", line);
                    break;
                default:
                    logger.LogInformation("{Line}", line);
                    break;
            }
        }

        logger.LogInformation("健康预检总体状态：{Status}", report.Status);

        if (report.Status == HealthStatus.Unhealthy)
        {
            var failed = string.Join(", ", report.Entries
                .Where(e => e.Value.Status == HealthStatus.Unhealthy)
                .Select(e => e.Key));

            throw new InvalidOperationException(
                $"关键依赖健康检查失败：{failed}。请检查连接串 / 容器状态后重启。");
        }
    }

    // ============================================================
    // Ollama 模型可用性检查（仅告警，不阻断启动）
    // ============================================================
    private static async Task CheckOllamaAsync(
        WebApplication app, ILogger logger, CancellationToken ct)
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

            // ★ 修复 CS8620：Where 过滤后收窄为 string[]，避免传参时可空性不匹配
            var names = models.EnumerateArray()
                .Select(m => m.TryGetProperty("name", out var n) ? n.GetString() : null)
                .Where(n => !string.IsNullOrEmpty(n))
                .Select(n => n!)                 // 收窄：已通过 IsNullOrEmpty 过滤
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