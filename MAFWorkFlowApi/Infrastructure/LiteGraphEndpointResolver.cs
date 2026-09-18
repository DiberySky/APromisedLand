using System.Data.Common;

namespace MAFWorkFlowApi.Infrastructure;

/// <summary>
/// LiteGraph 端点解析器。
/// 优先级：Aspire 服务发现环境变量 → ConnectionStrings → 显式配置 → 硬编码回退。
/// </summary>
internal static class LiteGraphEndpointResolver
{
    private const string Fallback = "http://localhost:8701";

    public static string Resolve(LiteGraphOptions options)
    {
        // ① Aspire 服务发现：services__{resource}__{endpoint}__{index}
        string?[] serviceDiscoveryVars =
        [
            "services__litegraph__http__0",
            "services__litegraph__https__0",
            "LITEGRAPH_ENDPOINT",
        ];

        foreach (var name in serviceDiscoveryVars)
        {
            var normalized = NormalizeEndpoint(
                Environment.GetEnvironmentVariable(name));
            if (normalized is not null)
                return normalized;
        }

        // ② ConnectionStrings__litegraph
        var fromConnStr = TryExtractEndpointFromConnectionString(
            Environment.GetEnvironmentVariable("ConnectionStrings__litegraph"));
        if (fromConnStr is not null)
            return AddSchemeIfMissing(fromConnStr);

        // ③ appsettings.json 中的显式配置
        var fromOptions = NormalizeEndpoint(options.Endpoint);
        if (fromOptions is not null)
            return fromOptions;

        // ④ 最终回退
        return Fallback;
    }

    // ─── 私有辅助 ──────────────────────────────────────

    private static string? NormalizeEndpoint(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var trimmed = raw.Trim().TrimEnd('/');

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

        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        return "http://" + trimmed;
    }
}