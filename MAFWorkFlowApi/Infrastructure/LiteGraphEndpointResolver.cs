using System.Data.Common;
using System.Text;

namespace MAFWorkFlowApi.Infrastructure;

/// <summary>
/// LiteGraph 端点解析器。
/// 优先级：Aspire 服务发现 → ConnectionStrings → appsettings → 硬编码回退。
/// ★ Resolve 内部日志降为 Debug —— 它是实现细节，进程内会被调用多次。
/// ★ DescribeResolution 保留 Information，并用 Interlocked 保证只输出一次。
/// </summary>
internal static class LiteGraphEndpointResolver
{
    private const string Fallback = "http://localhost:8701";

    private static readonly string[] ServiceDiscoveryVars =
    [
        "services__litegraph__http__0",
        "services__litegraph__https__0",
        "LITEGRAPH_ENDPOINT",
    ];

    private const string ConnectionStringVar = "ConnectionStrings__litegraph";

    /// <summary>诊断输出仅打一次（Interlocked 保证多线程安全）。</summary>
    private static int _diagnosticPrinted;

    public static string Resolve(LiteGraphOptions options, ILogger? logger = null)
    {
        // ① Aspire 服务发现
        foreach (var name in ServiceDiscoveryVars)
        {
            var raw = Environment.GetEnvironmentVariable(name);
            var normalized = NormalizeEndpoint(raw);
            if (normalized is not null)
            {
                logger?.LogDebug(
                    "[LiteGraph Endpoint] 采用 Aspire 服务发现：{Var} = {Value}",
                    name, normalized);
                return normalized;
            }
        }

        // ② ConnectionStrings__litegraph（Aspire 默认注入纯 URL）
        var connStr = Environment.GetEnvironmentVariable(ConnectionStringVar);
        if (!string.IsNullOrWhiteSpace(connStr))
        {
            // 先尝试 "Endpoint=..." 格式
            var fromConnStr = TryExtractEndpointFromConnectionString(connStr);
            if (fromConnStr is not null)
            {
                var normalized = AddSchemeIfMissing(fromConnStr);
                logger?.LogDebug(
                    "[LiteGraph Endpoint] 采用 ConnectionStrings (Endpoint=)：{Value}",
                    normalized);
                return normalized;
            }

            // ★ Aspire 默认注入的就是纯 URL —— 直接采用
            var asUrl = NormalizeEndpoint(connStr);
            if (asUrl is not null)
            {
                logger?.LogDebug(
                    "[LiteGraph Endpoint] 采用 ConnectionStrings (URL)：{Value}", asUrl);
                return asUrl;
            }
        }

        // ③ appsettings.json 显式配置
        var fromOptions = NormalizeEndpoint(options.Endpoint);
        if (fromOptions is not null)
        {
            logger?.LogDebug(
                "[LiteGraph Endpoint] 采用 appsettings：LiteGraph:Endpoint = {Value}",
                fromOptions);
            return fromOptions;
        }

        // ④ 回退
        logger?.LogDebug(
            "[LiteGraph Endpoint] 未找到任何端点配置，回退到默认值 {Fallback}",
            Fallback);
        return Fallback;
    }

    /// <summary>
    /// 生成诊断文本并保证全进程只打印一次。
    /// 应该在 LiteGraphRestClient 构造时调用，不要在其他地方调用。
    /// </summary>
    public static void DescribeResolutionOnce(LiteGraphOptions options, ILogger logger)
    {
        if (Interlocked.Exchange(ref _diagnosticPrinted, 1) != 0)
            return;

        logger.LogInformation("{Diagnostics}", DescribeResolution(options));
    }

    /// <summary>生成诊断文本（不打印）。</summary>
    public static string DescribeResolution(LiteGraphOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine("LiteGraph 端点解析诊断：");

        foreach (var v in ServiceDiscoveryVars)
        {
            var raw = Environment.GetEnvironmentVariable(v);
            sb.AppendLine($"  {v} = {raw ?? "(未设置)"}");
        }

        var cs = Environment.GetEnvironmentVariable(ConnectionStringVar);
        sb.AppendLine($"  {ConnectionStringVar} = {cs ?? "(未设置)"}");
        sb.AppendLine($"  LiteGraph:Endpoint (appsettings) = {options.Endpoint ?? "(null)"}");
        // ★ 用纯 ASCII ">>" 替代 "➜"，避免 Windows GBK 控制台显示为 ?
        sb.AppendLine($"  >> 最终采用 = {Resolve(options)}");

        return sb.ToString();
    }

    // ─── 私有辅助 ──────────────────────────────────────

    private static string? NormalizeEndpoint(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var trimmed = raw.Trim().TrimEnd('/');
        if (trimmed.Length == 0) return null;

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