namespace MAFRagService.Startup.Configuration;

/// <summary>
/// 解析 Aspire 注入的连接字符串或显式配置，得到 (Url, Model)。
/// 支持形式：
///   - "http://host:port"
///   - "Endpoint=http://host:port;Model=qwen2.5:7b"
///   - null / 空
/// </summary>
public static class OllamaEndpoint
{
    public static (string Url, string? Model) Parse(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return (string.Empty, null);

        string url = string.Empty;
        string? model = null;

        foreach (var segment in connectionString.Split(
                     ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = segment.Split('=', 2);
            if (kv.Length == 2)
            {
                switch (kv[0].ToLowerInvariant())
                {
                    case "endpoint":
                    case "url":
                        url = kv[1].TrimEnd('/');
                        continue;
                    case "model":
                        model = kv[1];
                        continue;
                }
            }

            if (Uri.TryCreate(segment, UriKind.Absolute, out var uri))
                url = uri.ToString().TrimEnd('/');
        }

        return (url, model);
    }

    /// <summary>返回第一个非空白候选值；全部为空时返回 <paramref name="fallback"/>。</summary>
    public static string Coalesce(string fallback, params string?[] candidates)
    {
        foreach (var c in candidates)
            if (!string.IsNullOrWhiteSpace(c)) return c!;
        return fallback;
    }
}
