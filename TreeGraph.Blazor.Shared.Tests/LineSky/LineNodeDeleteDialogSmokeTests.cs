using System.Net;
using System.Text;
using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using TreeGraph.Blazor.Shared.LineSky;
using TreeGraph.Blazor.Shared.LineSky.Dialogs;
using TreeGraph.Blazor.Shared.LineSky.Services;
using TreeGraph.Blazor.Shared.EdgeSky.Services;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.LineSky;

/// <summary>
/// LineNodeDeleteDialog 冒烟测试（删除预检：前驱 / 后继 / 关系边 阻塞提示与提交禁用）。
/// 覆盖：有阻塞时警告列出阻塞项且提交禁用；无阻塞时提交可用且可完成删除。
/// 模式对齐其它冒烟测试：真实 MudDialogProvider + 桩 HttpClient。
/// </summary>
public class LineNodeDeleteDialogSmokeTests : BunitTestBase
{
    private const string TestLineId = "line-test-001";
    private const string TestNodeId = "del-1";
    private const string TestSpaceId = "sp1";

    public LineNodeDeleteDialogSmokeTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLogging();
        Services.AddMudServices();
        Services.AddSingleton(new HttpClient(new StubHandler())
        {
            BaseAddress = new Uri("http://localhost/")
        });
        Services.AddSingleton<LineNodeManageApiClient>();
        Services.AddSingleton<EdgeApiClient>();
    }

    // ============================================================
    // 桩 HttpClient：按用例静态配置返回值
    // ============================================================

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    /// <summary>同线节点（后继检测）：预检 GET api/LineNode?lineId= 的返回。</summary>
    private static IReadOnlyList<LineNodeDto> LineNodes = [];

    /// <summary>节点所属空间：null 时回 404（GetAsync 降级为 null，跳过关系边检测）。</summary>
    private static object? NodeSpace;

    /// <summary>出/入边（关系边检测）：按 direction=out/in 分流。</summary>
    private static IReadOnlyList<object> OutEdges = [];
    private static IReadOnlyList<object> InEdges = [];

    /// <summary>删除请求是否到达。</summary>
    private static bool DeleteCalled;

    private static string Envelope(object? data)
        => JsonSerializer.Serialize(new { Success = true, Message = (string?)null, Data = data }, WebJson);

    private static object Edge(string edgeType, string src, string dst)
        => new { spaceId = TestSpaceId, edgeType, srcVid = src, rank = 0L, dstVid = dst, props = (object?)null };

    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? "";
            var query = request.RequestUri?.Query ?? "";

            if (request.Method == HttpMethod.Get && path.EndsWith("/api/LineNode"))
                return Task.FromResult(Json(Envelope(LineNodes)));

            if (request.Method == HttpMethod.Get && path.StartsWith("/api/edge/node-space/"))
                return Task.FromResult(NodeSpace is null
                    ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    : Json(Envelope(NodeSpace)));

            if (request.Method == HttpMethod.Get && path.EndsWith("/edge/edges"))
            {
                var edges = query.Contains("direction=out") ? OutEdges : InEdges;
                return Task.FromResult(Json(Envelope(edges)));
            }

            if (request.Method == HttpMethod.Delete && path.EndsWith($"/api/LineNode/{TestNodeId}"))
            {
                DeleteCalled = true;
                return Task.FromResult(Json(Envelope(true)));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static HttpResponseMessage Json(string body)
            => new(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
    }

    // ============================================================
    // 打开弹窗并等预检完成
    // ============================================================

    private async Task<(IRenderedComponent<MudDialogProvider> Provider, IDialogReference Reference)>
        OpenAsync(
            LineNodeDto node,
            IReadOnlyList<LineNodeDto>? lineNodes = null,
            object? nodeSpace = null,
            IReadOnlyList<object>? outEdges = null,
            IReadOnlyList<object>? inEdges = null)
    {
        LineNodes = lineNodes ?? [];
        NodeSpace = nodeSpace;
        OutEdges = outEdges ?? [];
        InEdges = inEdges ?? [];
        DeleteCalled = false;

        var provider = Render<MudDialogProvider>();
        var ds = Services.GetRequiredService<IDialogService>();
        var reference = await ds.ShowAsync<LineNodeDeleteDialog>(string.Empty,
            new DialogParameters { { nameof(LineNodeDeleteDialog.Node), node } },
            new DialogOptions());
        provider.Render();

        // 等预检完成（OnInitializedAsync → 同线节点 + 空间 + 出入边查询）
        for (var i = 0; i < 10; i++)
        {
            await Task.Delay(50);
            provider.Render();
        }
        return (provider, reference);
    }

    private static LineNodeDto Node(string? predecessorId = null, string? predecessorName = null)
        => new()
        {
            Id = TestNodeId,
            LineId = TestLineId,
            Name = "待删节点",
            PredecessorId = predecessorId,
            PredecessorName = predecessorName
        };

    private static IElement SubmitButton(IRenderedComponent<MudDialogProvider> provider)
        => provider.FindAll("button").First(b => b.TextContent.Contains("确认删除"));

    // ============================================================
    // 用例
    // ============================================================

    [Fact]
    public async Task Precheck_Predecessor_ShowsWarning_AndDisablesSubmit()
    {
        var (provider, _) = await OpenAsync(Node(predecessorId: "p1", predecessorName: "牵引所A"));

        var alert = provider.Find(".mud-alert");
        Assert.Contains("前驱节点「牵引所A」", alert.InnerHtml);
        Assert.True(SubmitButton(provider).HasAttribute("disabled"));
    }

    [Fact]
    public async Task Precheck_Successor_ShowsWarning_AndDisablesSubmit()
    {
        var successors = new LineNodeDto[]
        {
            new() { Id = "s1", LineId = TestLineId, Name = "分区所", PredecessorId = TestNodeId }
        };

        var (provider, _) = await OpenAsync(Node(), lineNodes: successors);

        var alert = provider.Find(".mud-alert");
        Assert.Contains("后继节点「分区所」", alert.InnerHtml);
        Assert.True(SubmitButton(provider).HasAttribute("disabled"));
    }

    [Fact]
    public async Task Precheck_RelationEdges_ShowsWarning_AndDisablesSubmit()
    {
        var nodeSpace = new { spaceId = TestSpaceId, spaceName = "空间", spaceType = "Dynamic" };
        // 出边：1 条关系边（对端 x 不在本线）；入边：段边（p1 是同线节点，任意类型都按段边排除）
        var outEdges = new[] { Edge("rel_x", TestNodeId, "x") };
        var inEdges = new[] { Edge("line_segment", "p1", TestNodeId) };
        var lineNodes = new LineNodeDto[]
        {
            new() { Id = "p1", LineId = TestLineId, Name = "牵引所A" }
        };

        var (provider, _) = await OpenAsync(Node(),
            lineNodes: lineNodes, nodeSpace: nodeSpace, outEdges: outEdges, inEdges: inEdges);

        var alert = provider.Find(".mud-alert");
        Assert.Contains("关系边 1 条", alert.InnerHtml);
        Assert.True(SubmitButton(provider).HasAttribute("disabled"));
    }

    [Fact]
    public async Task Precheck_NoBlockers_EnablesSubmit_AndDeletes()
    {
        var (provider, reference) = await OpenAsync(Node());

        Assert.Empty(provider.FindAll(".mud-alert"));
        Assert.False(SubmitButton(provider).HasAttribute("disabled"));

        SubmitButton(provider).Click();

        var result = await reference.Result.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(result.Canceled);
        Assert.True(DeleteCalled);
    }
}
