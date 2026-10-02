using System.Net;
using System.Text;
using System.Text.Json;

namespace TreeGraph.Blazor.Tests.TestDoubles;

/// <summary>
/// 轻量 HTTP handler：按 URL 前缀匹配返回预置 JSON。
/// 用于 bUnit 测试中替代真实网络。
///
/// 用法：
///   var handler = new TestHttpMessageHandler()
///       .Map("GET api/eav/metadata/custom-tables/1", dto)
///       .Map("GET api/eav/Product/entities/1/tables/certs", value);
///
/// 未匹配的请求返回 404，便于测试时立即暴露遗漏。
/// </summary>
public sealed class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly List<(string Method, string UrlKey, string Json)> _routes = new();

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public TestHttpMessageHandler Map(string method, string urlKey, object payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        _routes.Add((method.ToUpperInvariant(), urlKey, json));
        return this;
    }

    public TestHttpMessageHandler MapGet(string urlKey, object payload)
        => Map("GET", urlKey, payload);

    public TestHttpMessageHandler MapPut(string urlKey, object payload)
        => Map("PUT", urlKey, payload);

    public TestHttpMessageHandler MapPost(string urlKey, object payload)
        => Map("POST", urlKey, payload);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var method = request.Method.Method.ToUpperInvariant();
        var url = request.RequestUri?.ToString() ?? "";

        foreach (var (m, key, json) in _routes)
        {
            if (m != method) continue;
            if (!url.Contains(key, StringComparison.OrdinalIgnoreCase)) continue;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent(
                $"No route for {method} {url}",
                Encoding.UTF8, "text/plain")
        });
    }
}
