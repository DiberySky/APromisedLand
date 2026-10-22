using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TreeGraph.MafWfApi.Infrastructure;
using Xunit;

namespace TreeGraph.MafWfApi.Tests.Infrastructure;

/// <summary>
/// NebulaGraphRestClient 测试：POST /api/v1/graph/query 的
/// 正常返回 / NebulaGraph 业务失败（success=false）/ HTTP 层失败 三例。
/// </summary>
public class NebulaGraphRestClientTests
{
    private sealed class FakeHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null)
                RequestBody = await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class FakeFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler);
    }

    private static NebulaGraphRestClient CreateClient(HttpMessageHandler handler)
        => new(new FakeFactory(handler),
            Options.Create(new NebulaGraphOptions { Endpoint = "http://fake/" }),
            NullLogger<NebulaGraphRestClient>.Instance);

    [Fact]
    public async Task 正常响应_返回数据行且请求带space与query()
    {
        const string body = """
            {
              "success": true,
              "error_code": 0,
              "data": [ { "vid": "11111111-1111-1111-1111-111111111111", "name": "根节点A" } ],
              "columns": [ "vid", "name" ],
              "row_count": 1
            }
            """;
        var handler = new FakeHandler(HttpStatusCode.OK, body);
        var client = CreateClient(handler);

        var result = await client.QueryAsync("MATCH (n) RETURN id(n) AS vid, n.name AS name");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var row = Assert.Single(result.Data);
        Assert.Equal("根节点A", row["name"].GetString());
        Assert.Equal("11111111-1111-1111-1111-111111111111", row["vid"].GetString());

        Assert.NotNull(handler.RequestBody);
        Assert.Contains("\"space\":\"treegraph\"", handler.RequestBody);
        Assert.Contains("\"query\":\"MATCH (n)", handler.RequestBody);
    }

    [Fact]
    public async Task 业务失败_success为false_抛InvalidOperationException()
    {
        const string body = """
            { "success": false, "error_code": -1, "error_msg": "SyntaxError: near `MATC'" }
            """;
        var client = CreateClient(new FakeHandler(HttpStatusCode.OK, body));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.QueryAsync("MATC (n) RETURN n"));

        Assert.Contains("SyntaxError", ex.Message);
    }

    [Fact]
    public async Task HTTP500_抛HttpRequestException()
    {
        var client = CreateClient(new FakeHandler(
            HttpStatusCode.InternalServerError, "{}"));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.QueryAsync("MATCH (n) RETURN n"));
    }
}
