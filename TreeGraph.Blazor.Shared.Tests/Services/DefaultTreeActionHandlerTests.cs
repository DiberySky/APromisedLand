using System.Net;
using System.Text;
using System.Text.Json;
using TreeGraph.Blazor.Shared.TreeEavSky.Models;
using TreeGraph.Blazor.Shared.TreeEavSky.Services;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.Services;

/// <summary>
/// DefaultTreeActionHandler 单测。
/// TreeApiClient 的方法非虚，Moq 无法拦截，
/// 因此用桩 HttpMessageHandler + 真实 HttpClient 走完整 HTTP 管道，
/// 同时验证 URL 路由、方法谓词与请求体 DTO 构造。
/// </summary>
public class DefaultTreeActionHandlerTests
{
    /// <summary>记录最近一次请求并返回预置 JSON 的桩处理器。</summary>
    private sealed class StubHttpHandler : HttpMessageHandler
    {
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
        public string ResponseJson { get; set; } = """{"success":true,"data":null}""";
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is not null
                ? await request.Content.ReadAsStringAsync(cancellationToken)
                : null;

            return new HttpResponseMessage(StatusCode)
            {
                Content = new StringContent(ResponseJson, Encoding.UTF8, "application/json"),
            };
        }
    }

    private static (DefaultTreeActionHandler<StringTreeNode> Handler, StubHttpHandler Stub) CreateHandler()
    {
        var stub = new StubHttpHandler();
        var client = new TreeApiClient<StringTreeNode>(
            new HttpClient(stub) { BaseAddress = new Uri("https://test/") });
        return (new DefaultTreeActionHandler<StringTreeNode>(client), stub);
    }

    private static StringTreeNode Node(string id, string? parentId = null, int sortOrder = 0)
        => new() { Id = id, Name = id, ParentId = parentId, SortOrder = sortOrder };

    private static string OkDataJson(string json) => $$"""{"success":true,"data":{{json}}}""";

    // ============================================================
    // CreateChildAsync
    // ============================================================

    [Fact]
    public async Task CreateChildAsync_PostsDto_WithParentId_AndReturnsValue()
    {
        var (handler, stub) = CreateHandler();
        stub.ResponseJson = OkDataJson(
            """{"id":"new-id","text":"子","value":{"id":"new-id","name":"子"}}""");

        var parent = Node("p1");
        var child = Node("", parentId: "p1");
        child.Name = "子";

        var result = await handler.CreateChildAsync(parent, child);

        Assert.NotNull(result);
        Assert.Equal("new-id", result!.Id);
        Assert.Equal(HttpMethod.Post, stub.Request!.Method);
        Assert.Equal("https://test/StringTreeNode", stub.Request.RequestUri!.ToString());

        var body = JsonDocument.Parse(stub.RequestBody!);
        Assert.Equal("p1", body.RootElement.GetProperty("parentId").GetString());
        Assert.Equal("子", body.RootElement.GetProperty("value").GetProperty("name").GetString());
    }

    // ============================================================
    // UpdateNodeAsync
    // ============================================================

    [Fact]
    public async Task UpdateNodeAsync_PutsToNodeUrl()
    {
        var (handler, stub) = CreateHandler();
        stub.ResponseJson = OkDataJson("""{"id":"n1","value":{"id":"n1","name":"新名"}}""");

        var result = await handler.UpdateNodeAsync(Node("n1", parentId: "p9"));

        Assert.NotNull(result);
        Assert.Equal("新名", result!.Name);
        Assert.Equal(HttpMethod.Put, stub.Request!.Method);
        Assert.Equal("https://test/StringTreeNode/n1", stub.Request.RequestUri!.ToString());

        var body = JsonDocument.Parse(stub.RequestBody!);
        Assert.Equal("p9", body.RootElement.GetProperty("parentId").GetString());
    }

    // ============================================================
    // DeleteNodeAsync
    // ============================================================

    [Fact]
    public async Task DeleteNodeAsync_Delete_ReturnsTrue()
    {
        var (handler, stub) = CreateHandler();
        stub.ResponseJson = """{"success":true,"data":true}""";

        var ok = await handler.DeleteNodeAsync(Node("n1"));

        Assert.True(ok);
        Assert.Equal(HttpMethod.Delete, stub.Request!.Method);
        Assert.Equal("https://test/StringTreeNode/n1", stub.Request.RequestUri!.ToString());
    }

    [Fact]
    public async Task DeleteNodeAsync_NotFound_ReturnsFalse()
    {
        var (handler, stub) = CreateHandler();
        stub.StatusCode = HttpStatusCode.NotFound;
        // 404 分支不解析响应体
        stub.ResponseJson = "";

        var ok = await handler.DeleteNodeAsync(Node("missing"));

        Assert.False(ok);
    }

    // ============================================================
    // MoveNodeAsync
    // ============================================================

    [Fact]
    public async Task MoveNodeAsync_PostsMoveRoute_WithNodeAndParentIds()
    {
        var (handler, stub) = CreateHandler();
        stub.ResponseJson = """{"success":true,"data":true}""";

        var ok = await handler.MoveNodeAsync(Node("n1"), Node("p2"));

        Assert.True(ok);
        Assert.Equal(HttpMethod.Post, stub.Request!.Method);
        Assert.Equal("/StringTreeNode/move", stub.Request.RequestUri!.AbsolutePath);
        var query = System.Web.HttpUtility.ParseQueryString(stub.Request.RequestUri.Query);
        Assert.Equal("n1", query["nodeId"]);
        Assert.Equal("p2", query["newParentId"]);
    }

    [Fact]
    public async Task MoveNodeAsync_ToRoot_OmitsNewParentId()
    {
        var (handler, stub) = CreateHandler();
        stub.ResponseJson = """{"success":true,"data":true}""";

        var ok = await handler.MoveNodeAsync(Node("n1"), newParent: null);

        Assert.True(ok);
        var query = System.Web.HttpUtility.ParseQueryString(stub.Request!.RequestUri!.Query);
        Assert.Equal("n1", query["nodeId"]);
        Assert.Null(query["newParentId"]);
    }

    // ============================================================
    // SortChildrenAsync
    // ============================================================

    [Fact]
    public async Task SortChildrenAsync_PostsChildrenInGivenOrder_IgnoringOldSortOrder()
    {
        var (handler, stub) = CreateHandler();

        var parent = Node("p1");
        // 旧 SortOrder：a=0、b=1；对话框拖拽后的新顺序为 [b, a]
        var a = Node("a", parentId: "p1", sortOrder: 0);
        var b = Node("b", parentId: "p1", sortOrder: 1);

        var ok = await handler.SortChildrenAsync(parent, [b, a]);

        Assert.True(ok);
        Assert.Equal(HttpMethod.Post, stub.Request!.Method);
        Assert.Equal("https://test/StringTreeNode/children", stub.Request.RequestUri!.ToString());

        var body = JsonDocument.Parse(stub.RequestBody!);
        var children = body.RootElement.GetProperty("children");
        Assert.Equal(2, children.GetArrayLength());

        var first = children[0];
        var second = children[1];
        // 列表位置即新顺序：b 在前且重编号为 0，a 在后重编号为 1
        Assert.Equal("b", first.GetProperty("id").GetString());
        Assert.Equal(0, first.GetProperty("sortOrder").GetInt32());
        Assert.Equal("a", second.GetProperty("id").GetString());
        Assert.Equal(1, second.GetProperty("sortOrder").GetInt32());
        Assert.Equal("p1", first.GetProperty("parentId").GetString());
    }
}
