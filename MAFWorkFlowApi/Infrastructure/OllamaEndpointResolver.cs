using System.Data.Common;

namespace MAFWorkFlowApi.Infrastructure;

/// <summary>
/// Ollama 端点统一解析器。
/// 消除 Program.cs / OllamaWarmupService / RerankerService 三处重复的端点解析逻辑。
///
/// 优先级：
///   1. 显式传入的 endpoint（来自 Options 或配置）
///   2. ConnectionStrings:ollama 或 ConnectionStrings:chat-model
///   3. Aspire 服务发现环境变量
///   4. Ollama:Endpoint 配置
///   5. 硬编码回退
/// </summary>
internal static class OllamaEndpointResolver
{
    private const string DefaultFallback = "http://localhost:11434";

    private static readonly string[] ServiceDiscoveryVars =
    [
        "services__ollama__http__0",
        "services__ollama__https__0",
        "services__chat-model__http__0",
        "services__chat-model__https__0",
    ];

    private static readonly string[] ConnectionStringKeys =
    [
        "ConnectionStrings:ollama",
        "ConnectionStrings:chat-model",
        "ConnectionStrings__ollama",
        "ConnectionStrings__chat-model",
    ];

    /// <summary>
    /// 解析 Ollama 端点。
    /// </summary>
    /// <param name="config">配置对象。</param>
    /// <param name="explicitEndpoint">显式指定的端点（如 Options.Endpoint），优先使用。</param>
    /// <param name="fallback">所有来源都未命中时的回退 URL。</param>
    /// <returns>规范化后的端点 URL（含 http:// 前缀，无尾部斜杠）。</returns>
    public static string Resolve(
        IConfiguration config,
        string? explicitEndpoint = null,
        string? fallback = null)
    {
        // ① 显式端点
        var fromExplicit = Normalize(explicitEndpoint);
        if (fromExplicit is not null) return fromExplicit;

        // ② ConnectionStrings
        foreach (var key in ConnectionStringKeys)
        {
            var raw = config[key];
            var normalized = Normalize(raw);
            if (normalized is not null) return normalized;
        }

        // ③ Aspire 服务发现环境变量
        foreach (var name in ServiceDiscoveryVars)
        {
            var raw = Environment.GetEnvironmentVariable(name);
            var normalized = Normalize(raw);
            if (normalized is not null) return normalized;
        }

        // ④ Ollama:Endpoint 配置
        var fromConfig = Normalize(config["Ollama:Endpoint"]);
        if (fromConfig is not null) return fromConfig;

        // ⑤ 回退
        return Normalize(fallback) ?? DefaultFallback;
    }

    /// <summary>
    /// 仅从连接字符串中提取 Endpoint。
    /// 用于 embedding 等专用连接字符串场景。
    /// </summary>
    /// <returns>提取并规范化后的端点 URL；若不是连接字符串格式则返回 null。</returns>
    public static string? FromConnectionString(string? connectionString)
        => Normalize(connectionString);

    // ─── 私有辅助 ──────────────────────────────────────

    private static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var trimmed = raw.Trim().TrimEnd('/');
        if (trimmed.Length == 0) return null;

        // 连接字符串格式：Endpoint=http://...
        if (trimmed.Contains('='))
        {
            var extracted = TryExtractEndpointFromConnectionString(trimmed);
            return extracted is not null ? AddSchemeIfMissing(extracted) : null;
        }

        return AddSchemeIfMissing(trimmed);
    }

    private static string? TryExtractEndpointFromConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return null;

        try
        {
            var builder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };
            if (builder.TryGetValue("Endpoint", out var v)
                && v is string s
                && !string.IsNullOrWhiteSpace(s))
            {
                return s.Trim();
            }
        }
        catch (ArgumentException) { }

        return null;
    }

    private static string AddSchemeIfMissing(string url)
    {
        var trimmed = url.Trim().TrimEnd('/');
        if (trimmed.Length == 0) return trimmed;

        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        return "http://" + trimmed;
    }
}
