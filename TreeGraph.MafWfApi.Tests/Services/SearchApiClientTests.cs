using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using TreeGraph.MafWfApi.Services;
using Xunit;

namespace TreeGraph.MafWfApi.Tests.Services;

/// <summary>
/// SearchApiClient 解析测试：TreeGraph.Api 信封 { success, data.items } →
/// SemanticSearchItemDto（nodeName/parentName/path/rootId/hostType/score 全字段）。
/// </summary>
public class SearchApiClientTests
{
    private sealed class FakeHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
    }

    private static SearchApiClient CreateClient(HttpMessageHandler handler)
        => new(new HttpClient(handler) { BaseAddress = new Uri("http://fake/") },
            NullLogger<SearchApiClient>.Instance);

    private const string Envelope = """
        {
          "success": true,
          "message": "",
          "data": {
            "query": "注塑机",
            "total": 2,
            "items": [
              {
                "inodeId": "nd-101",
                "hostType": "SpaceTree",
                "nodeName": "注塑机 A-101",
                "parentName": "注塑车间",
                "path": "空间树 / 生产部 / 注塑车间 / 注塑机 A-101",
                "rootId": "root-1",
                "score": 0.9234
              },
              {
                "inodeId": "nd-102",
                "hostType": "TreeNode",
                "nodeName": null,
                "parentName": null,
                "path": "",
                "rootId": null,
                "score": 0.5
              }
            ]
          }
        }
        """;

    [Fact]
    public async Task 正常响应_解析信封items全字段()
    {
        var client = CreateClient(new FakeHandler(HttpStatusCode.OK, Envelope));

        var result = await client.SearchAsync("注塑机", 5);

        Assert.NotNull(result);
        Assert.Equal(2, result.Items.Count);

        var first = result.Items[0];
        Assert.Equal("nd-101", first.InodeId);
        Assert.Equal("SpaceTree", first.HostType);
        Assert.Equal("注塑机 A-101", first.NodeName);
        Assert.Equal("注塑车间", first.ParentName);
        Assert.Equal("空间树 / 生产部 / 注塑车间 / 注塑机 A-101", first.Path);
        Assert.Equal("root-1", first.RootId);
        Assert.Equal(0.9234f, first.Score, 4);

        var second = result.Items[1];
        Assert.Equal("TreeNode", second.HostType);
        Assert.Null(second.NodeName);
        Assert.Null(second.RootId);
        Assert.Equal("", second.Path);
    }

    [Fact]
    public async Task 服务不可用_返回null()
    {
        var client = CreateClient(new FakeHandler(
            HttpStatusCode.ServiceUnavailable, "{}"));

        var result = await client.SearchAsync("x", 5);

        Assert.Null(result);
    }

    [Fact]
    public async Task 无data信封_返回null()
    {
        var client = CreateClient(new FakeHandler(HttpStatusCode.OK, "{}"));

        var result = await client.SearchAsync("x", 5);

        Assert.Null(result);
    }

    [Fact]
    public async Task items缺失_返回空列表()
    {
        var client = CreateClient(new FakeHandler(HttpStatusCode.OK,
            """{ "success": true, "data": { "query": "x", "total": 0 } }"""));

        var result = await client.SearchAsync("x", 5);

        Assert.NotNull(result);
        Assert.Empty(result.Items);
    }
}
