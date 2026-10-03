namespace TreeGraph.Blazor.E2E.Tests.Fixtures;

/// <summary>
/// 轮询等待 Aspire 编排的服务就绪。
/// 由外部 `dotnet run --project APromisedLand.AppHost` 提供。
/// </summary>
public static class AspireHealthCheck
{
    /// <summary>
    /// 等待 Blazor 前端可访问。
    /// 命中 200 或任何 HTTP 响应（含 302/401）即视为就绪。
    /// </summary>
    public static async Task WaitForBlazorAsync(
        string baseUrl, int timeoutSeconds, CancellationToken ct = default)
    {
        await WaitForHttpAsync(baseUrl + "/", timeoutSeconds, ct);
    }

    /// <summary>
    /// 等待 EAV API 就绪（走 /health 端点）。
    /// </summary>
    public static async Task WaitForApiAsync(
        string baseUrl, int timeoutSeconds, CancellationToken ct = default)
    {
        await WaitForHttpAsync(baseUrl + "/health", timeoutSeconds, ct);
    }

    private static async Task WaitForHttpAsync(
        string url, int timeoutSeconds, CancellationToken ct)
    {
        using var http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        Exception? lastEx = null;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var resp = await http.GetAsync(url, ct);
                // 任何响应（含 4xx）都说明服务已在监听
                if (resp.StatusCode != System.Net.HttpStatusCode.ServiceUnavailable)
                    return;
            }
            catch (Exception ex)
            {
                lastEx = ex;
            }

            await Task.Delay(1000, ct);
        }

        throw new TimeoutException(
            $"服务 {url} 在 {timeoutSeconds}s 内未就绪。" +
            $"请确认 Aspire AppHost 已启动。最后异常: {lastEx?.Message}");
    }
}
