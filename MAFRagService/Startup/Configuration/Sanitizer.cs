namespace MAFRagService.Startup.Configuration;

/// <summary>
/// 日志/诊断输出脱敏工具。当前覆盖 URL 中的 userinfo、查询串里的常见密钥参数。
/// </summary>
public static class Sanitizer
{
    private static readonly string[] SensitiveQueryKeys =
    {
        "password", "pwd", "secret", "token", "apikey", "api_key", "access_key", "accesskey"
    };

    public static string Url(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)) return raw;

        var builder = new UriBuilder(uri);

        // 1) userinfo → ***:***
        if (!string.IsNullOrEmpty(builder.UserName) || !string.IsNullOrEmpty(builder.Password))
        {
            builder.UserName = "***";
            builder.Password = "***";
        }

        // 2) 敏感查询参数遮蔽
        var query = System.Web.HttpUtility.ParseQueryString(builder.Query);
        foreach (var key in query.AllKeys.Where(k => k is not null))
        {
            if (SensitiveQueryKeys.Any(s =>
                    key!.Contains(s, StringComparison.OrdinalIgnoreCase)))
            {
                query[key!] = "***";
            }
        }
        builder.Query = query.ToString() ?? string.Empty;

        return builder.Uri.ToString();
    }
}