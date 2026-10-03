using System.Text.Json;

namespace TreeGraph.Blazor.E2E.Tests.Fixtures;

public class E2ESettings
{
    public string BlazorBaseUrl { get; init; } = "http://localhost:5783";
    public string ApiBaseUrl { get; init; } = "http://localhost:5773";
    public int HealthProbeTimeoutSeconds { get; init; } = 120;
    public int DefaultTimeoutMs { get; init; } = 15000;

    /// <summary>
    /// 读取 appsettings.json（可选），环境变量 E2E_ 前缀覆盖。
    /// 不引入 Microsoft.Extensions.Configuration，保持测试项目依赖最小。
    /// </summary>
    public static E2ESettings Load()
    {
        string? blazor = null, api = null;
        int? health = null, timeout = null;

        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (File.Exists(path))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.TryGetProperty("E2E", out var s))
                {
                    if (s.TryGetProperty("BlazorBaseUrl", out var v) && v.ValueKind == JsonValueKind.String)
                        blazor = v.GetString();
                    if (s.TryGetProperty("ApiBaseUrl", out var v2) && v2.ValueKind == JsonValueKind.String)
                        api = v2.GetString();
                    if (s.TryGetProperty("HealthProbeTimeoutSeconds", out var v3) && v3.TryGetInt32(out var h))
                        health = h;
                    if (s.TryGetProperty("DefaultTimeoutMs", out var v4) && v4.TryGetInt32(out var t))
                        timeout = t;
                }
            }
            catch (JsonException)
            {
                // 配置文件损坏时退回默认值
            }
        }

        return new E2ESettings
        {
            BlazorBaseUrl = Environment.GetEnvironmentVariable("E2E_BlazorBaseUrl") ?? blazor ?? "http://localhost:5783",
            ApiBaseUrl = Environment.GetEnvironmentVariable("E2E_ApiBaseUrl") ?? api ?? "http://localhost:5773",
            HealthProbeTimeoutSeconds =
                int.TryParse(Environment.GetEnvironmentVariable("E2E_HealthProbeTimeoutSeconds"), out var eh)
                    ? eh : health ?? 120,
            DefaultTimeoutMs =
                int.TryParse(Environment.GetEnvironmentVariable("E2E_DefaultTimeoutMs"), out var et)
                    ? et : timeout ?? 15000
        };
    }
}
