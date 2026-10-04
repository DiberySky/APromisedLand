using System.Net;
using System.Text;
using TreeGraph.TreeSky.Attributes;
using TreeGraph.TreeSky.Models;
using TreeGraph.TreeSky.Services;
using Xunit;

namespace TreeGraph.TreeSky.Tests.Attributes;

public class TreeRouteAttributeTests
{
    // ============================================================
    // 特性声明
    // ============================================================

    [Fact]
    public void StringTreeNode_IsDecorated_WithCurrentUrlPrefix()
    {
        var attr = typeof(StringTreeNode).GetCustomAttributes(false)
            .OfType<TreeRouteAttribute>()
            .SingleOrDefault();

        Assert.NotNull(attr);
        // 与后端 [Route("[controller]")] 的 StringTreeNodeController 一致
        Assert.Equal("StringTreeNode", attr!.Route);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_EmptyRoute_Throws(string route)
    {
        Assert.Throws<ArgumentException>(() => new TreeRouteAttribute(route));
    }

    // ============================================================
    // DiberyTreeApiClient 路由解析（桩 HttpClient）
    // ============================================================

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? LastUrl { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUrl = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"success":true,"data":[]}""", Encoding.UTF8, "application/json"),
            });
        }
    }

    [TreeRoute("custom-nodes")]
    private sealed class DecoratedNode : ITreeNodeBase<DecoratedNode>
    {
        public string Id { get; set; } = "";
        public string? ParentId { get; set; }
        public string? Description { get; set; }
        public bool CanHaveChildren { get; set; }
        public int SortOrder { get; set; }
        public bool HasChildren { get; set; }
        public DecoratedNode? Parent { get; set; }
        public string Text() => Id;
    }

    private sealed class PlainNode : ITreeNodeBase<PlainNode>
    {
        public string Id { get; set; } = "";
        public string? ParentId { get; set; }
        public string? Description { get; set; }
        public bool CanHaveChildren { get; set; }
        public int SortOrder { get; set; }
        public bool HasChildren { get; set; }
        public PlainNode? Parent { get; set; }
        public string Text() => Id;
    }

    [Fact]
    public async Task Client_DecoratedType_UsesAttributeRoute()
    {
        var handler = new RecordingHandler();
        var client = new DiberyTreeApiClient<DecoratedNode>(
            new HttpClient(handler) { BaseAddress = new Uri("https://test/") });

        await client.GetRootNodesAsync();

        Assert.Equal("https://test/custom-nodes/roots", handler.LastUrl!.ToString());
    }

    [Fact]
    public async Task Client_UndecoratedType_FallsBackToTypeName()
    {
        var handler = new RecordingHandler();
        var client = new DiberyTreeApiClient<PlainNode>(
            new HttpClient(handler) { BaseAddress = new Uri("https://test/") });

        await client.GetRootNodesAsync();

        // 未标注 → typeof(T).Name，即 "PlainNode"
        Assert.Equal("https://test/PlainNode/roots", handler.LastUrl!.ToString());
    }

    [Fact]
    public async Task Client_StringTreeNode_UrlUnchanged()
    {
        // 回归护栏：A 方案下现有 URL 一个字符都不能变
        var handler = new RecordingHandler();
        var client = new DiberyTreeApiClient<StringTreeNode>(
            new HttpClient(handler) { BaseAddress = new Uri("https://test/") });

        await client.GetRootNodesAsync();

        Assert.Equal("https://test/StringTreeNode/roots", handler.LastUrl!.ToString());
    }
}
