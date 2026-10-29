using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using TreeGraph.Blazor.Shared.EdgeSky.Dialogs;
using TreeGraph.Blazor.Shared.EdgeSky.Services;
using TreeGraph.Shared.EdgeSky;
using TreeGraph.Shared.EdgeSky.Dtos;
using TreeGraph.Shared.NodeSky.Entities;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.EdgeSky;

/// <summary>
/// EdgeInstanceCreateDialog 组件测试（新建节点关系 = 边实例）。
/// 覆盖：按边类型 Schema 动态渲染属性（含 NOT NULL 默认值预填）、
/// 出边/入边两种方向组装五元组、可空属性留空不入 Props。
/// </summary>
public class EdgeInstanceCreateDialogTests : BunitTestBase
{
    public EdgeInstanceCreateDialogTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLogging();
        Services.AddMudServices();
        Services.AddSingleton(new HttpClient(new FakeHandler())
        {
            BaseAddress = new Uri("http://localhost/")
        });
        Services.AddSingleton<EdgeApiClient>();
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"success":true,"message":null,"data":[]}""",
                    Encoding.UTF8, "application/json")
            });
    }

    private static EdgeTypeDto LineSegment() => new()
    {
        Name = "line_segment",
        DisplayName = "线段",
        Props =
        [
            new EdgeTypePropDto
            {
                PropName = "name", DisplayName = "名称",
                DataType = EdgePropDataTypes.String, Nullable = true
            },
            new EdgeTypePropDto
            {
                PropName = "degree",
                DataType = EdgePropDataTypes.Int, Nullable = false,
                DefaultValue = JsonSerializer.SerializeToElement(1)
            }
        ]
    };

    private async Task<(IRenderedComponent<MudDialogProvider> Provider, IDialogReference Ref)> OpenAsync()
    {
        var provider = Render<MudDialogProvider>();
        var ds = Services.GetRequiredService<IDialogService>();
        var reference = await ds.ShowAsync<EdgeInstanceCreateDialog>(
            string.Empty,
            new DialogParameters<EdgeInstanceCreateDialog>
            {
                { x => x.SpaceId, "space-x" },
                { x => x.SpaceName, "单位" },
                { x => x.SpaceType, "Unit" },
                { x => x.NodeId, "node-self" },
                { x => x.Types, new List<EdgeTypeDto> { LineSegment() } }
            },
            new DialogOptions());
        provider.Render();
        for (var i = 0; i < 5; i++)
        {
            await Task.Delay(20);
            provider.Render();
        }
        return (provider, reference);
    }

    /// <summary>反射注入已选对端节点（正常流程经搜索表「选择」按钮）。</summary>
    private static void SetPeer(IRenderedComponent<MudDialogProvider> provider, Node peer)
    {
        var dialog = provider.FindComponent<EdgeInstanceCreateDialog>().Instance;
        typeof(EdgeInstanceCreateDialog)
            .GetField("_peer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(dialog, peer);
        provider.Render();
    }

    private static void SetDirection(IRenderedComponent<MudDialogProvider> provider, string direction)
    {
        var dialog = provider.FindComponent<EdgeInstanceCreateDialog>().Instance;
        typeof(EdgeInstanceCreateDialog)
            .GetField("_direction", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(dialog, direction);
        provider.Render();
    }

    private static void ClickSubmit(IRenderedComponent<MudDialogProvider> provider)
        => provider.FindAll("button").First(b => b.TextContent.Trim() == "创建").Click();

    [Fact]
    public async Task Renders_PropFields_BySchema()
    {
        var (provider, _) = await OpenAsync();

        Assert.Contains("关系类型", provider.Markup);
        Assert.Contains("方向", provider.Markup);
        Assert.Contains("对端节点", provider.Markup);
        Assert.Contains("属性", provider.Markup);

        // STRING → 文本框；INT → 数字输入（Common/NumberInputField）
        Assert.Contains("名称（name）", provider.Markup);
        Assert.Contains("degree *", provider.Markup);
    }

    [Fact]
    public async Task Submit_OutEdge_PrefillsDefaultAndSkipsNullableEmpty()
    {
        var (provider, reference) = await OpenAsync();
        SetPeer(provider, new Node { Id = "node-peer", Name = "对端", HostType = "UnitTree" });

        ClickSubmit(provider);

        var result = await reference.Result;
        var created = Assert.IsType<EdgeInstanceCreateDialog.CreatedEdgeInstance>(result.Data);
        Assert.Equal("line_segment", created.EdgeType);
        Assert.Equal("node-self", created.SrcVid);
        Assert.Equal("node-peer", created.DstVid);
        Assert.Equal(0L, created.Rank);

        // NOT NULL INT 属性取 Schema 默认值 1；可空 STRING 留空不写入
        Assert.Equal(1, created.Props["degree"].GetInt64());
        Assert.False(created.Props.ContainsKey("name"));
    }

    [Fact]
    public async Task Submit_InEdge_SwapsEndpoints()
    {
        var (provider, reference) = await OpenAsync();
        SetPeer(provider, new Node { Id = "node-peer", Name = "对端", HostType = "UnitTree" });
        SetDirection(provider, "in");

        ClickSubmit(provider);

        var result = await reference.Result;
        var created = Assert.IsType<EdgeInstanceCreateDialog.CreatedEdgeInstance>(result.Data);
        Assert.Equal("node-peer", created.SrcVid);
        Assert.Equal("node-self", created.DstVid);
    }

    [Fact]
    public async Task Submit_WithoutPeer_StaysOpen()
    {
        var (provider, reference) = await OpenAsync();

        ClickSubmit(provider);

        Assert.False(reference.Result.IsCompleted);
    }
}
