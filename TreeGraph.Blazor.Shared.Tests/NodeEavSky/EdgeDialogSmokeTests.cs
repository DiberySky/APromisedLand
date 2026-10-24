using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using MudBlazor.Services;
using TreeGraph.Blazor.Shared.EdgeSky.Components.Dialogs;
using TreeGraph.Blazor.Shared.EdgeSky.Services;
using TreeGraph.Blazor.Shared.NodeEavSky.Components.Dialogs;
using TreeGraph.Blazor.Shared.NodeEavSky.Services;
using TreeGraph.Blazor.Shared.Platform;
using TreeGraph.Shared.EdgeSky.Dtos;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.NodeEavSky;

/// <summary>
/// Edge（关系/边）对话框组件冒烟测试。
/// 模式对齐 NodeEavDialogSmokeTests：真实 MudDialogProvider + 桩 HttpClient，
/// 仅验证对话框能正常渲染（标题正确、不抛异常），提交逻辑不在本冒烟范围。
/// R4：边不再有类型表，类型为裸字符串（TypeName），对话框只剩「关系类型名 + 节点对 + 有向/无向」。
/// </summary>
public class EdgeDialogSmokeTests : BunitTestBase
{
    public EdgeDialogSmokeTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLogging();
        Services.AddMudServices();
        Services.AddSingleton<IPlatformContext>(new FakePlatformContext());

        // Edge 对话框初始化时会按类型名调 GetSchemaAsync 加载 EAV 属性定义
        Services.AddSingleton(new HttpClient(new FakeHandler(Route))
        {
            BaseAddress = new Uri("http://localhost/")
        });
        Services.AddSingleton<EavApiClient>();
        Services.AddSingleton<EdgeApiClient>();
        Services.AddScoped<TreeGraph.Blazor.Shared.TreeEavSky.Dialogs.MessageService>();
    }

    private async Task<IRenderedComponent<MudDialogProvider>> OpenAsync<TDialog>(
        DialogParameters? parameters = null)
        where TDialog : ComponentBase
    {
        var provider = Render<MudDialogProvider>();
        var ds = Services.GetRequiredService<IDialogService>();
        var _ = await ds.ShowAsync<TDialog>(string.Empty, parameters ?? new DialogParameters(),
            new DialogOptions());
        provider.Render();
        return provider;
    }

    private static void AssertDialog(IRenderedComponent<MudDialogProvider> provider, string title)
    {
        var dialog = provider.Find(".mud-dialog");
        Assert.NotNull(dialog);
        Assert.Contains(title, provider.Find(".mud-dialog-title").TextContent,
            StringComparison.Ordinal);
    }

    // ─── Edge DTOs（测试数据） ───

    private static EdgeInstanceDto EdgeInstance(bool isDirected = true) => new(
        EdgeId: "e-1",
        TypeName: "Collaboration",
        FromNodeId: "node-a",
        FromNodeText: "牵引所A",
        ToNodeId: "node-b",
        ToNodeText: "牵引所B",
        IsDirected: isDirected,
        CreatedAt: DateTimeOffset.UtcNow,
        UpdatedAt: DateTimeOffset.UtcNow,
        Properties: null);

    // ─── 路由桩：按 URL 返回 canned JSON ───

    private static string? Route(string path, HttpMethod method)
    {
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        string Envelope(object? data)
            => JsonSerializer.Serialize(new { success = true, message = (string?)null, data }, web);

        // 编辑模式 / 预填类型名：按类型名加载 EAV schema（返回空属性列表）
        if (path.StartsWith("/api/eav/Collaboration/schema") && method == HttpMethod.Get)
            return Envelope(new { attributes = Array.Empty<object>() });

        // 宿主 inode 选择器：语义搜索
        if (path.StartsWith("/api/search/semantic") && method == HttpMethod.Post)
            return Envelope(new { query = "q", total = 0, items = Array.Empty<object>() });

        return null;
    }

    // ─── 测试用例 ───

    [Fact]
    public async Task EdgeDialog_CreateMode_Renders()
    {
        // 创建模式：传 PreFillFromNodeId（跳过 1st API 可能已在 Init 调列表）
        var provider = await OpenAsync<EdgeDialog>(new DialogParameters
        {
            { "PreFillFromNodeId", "node-A" }
        });

        AssertDialog(provider, "创建边");
        var html = provider.Find(".mud-dialog").InnerHtml;
        Assert.Contains("关系类型名", html);
        Assert.Contains("起点节点", html);
        Assert.Contains("终点节点", html);
        // 有向/无向开关存在（标签由组件状态决定）
        Assert.Equal("有向（起点 → 终点）",
            provider.FindComponent<MudSwitch<bool>>().Instance.Label);
    }

    [Fact]
    public async Task EdgeDialog_EditMode_Renders()
    {
        var provider = await OpenAsync<EdgeDialog>(new DialogParameters
        {
            { "Editing", EdgeInstance() }
        });

        AssertDialog(provider, "编辑边");
        var html = provider.Find(".mud-dialog").InnerHtml;
        // 编辑模式：类型名只读展示 + 两端显示名来自后端回填的 FromNodeText/ToNodeText
        Assert.Contains("Collaboration", html);
        Assert.Contains("牵引所A", html);
        Assert.Contains("牵引所B", html);
    }

    [Fact]
    public async Task EdgeDialog_CreateMode_FromNodeIsReadOnly()
    {
        // 约束：创建流程来源于宿主 inode —— 起点由宿主节点固定，不可修改
        var provider = await OpenAsync<EdgeDialog>(new DialogParameters
        {
            { "PreFillFromNodeId", "host-node-1" }
        });

        var html = provider.Find(".mud-dialog").InnerHtml;
        Assert.Contains("来自宿主节点，不可修改", html);

        var fromControl = provider.FindAll(".mud-input-control").FirstOrDefault(c =>
            c.TextContent.Contains("起点节点", StringComparison.Ordinal));
        Assert.NotNull(fromControl);
        var input = fromControl!.QuerySelector("input");
        Assert.NotNull(input);
        Assert.True(input!.HasAttribute("readonly"));
    }

    [Fact]
    public async Task InodeSelectorDialog_Renders()
    {
        // 宿主 inode 选择器（语义搜索包装，DialogSky 外壳）
        var provider = await OpenAsync<InodeSelectorDialog>();

        AssertDialog(provider, "选择宿主节点");

        var html = provider.Find(".mud-dialog").InnerHtml;
        Assert.Contains("搜索内容", html);
        Assert.Contains("确定", html);
    }

    [Fact]
    public async Task EdgeDialog_CreateMode_ToNodeUsesSelector()
    {
        // 终点 = 宿主 inode 选择器（语义搜索），不再自由输入
        var provider = await OpenAsync<EdgeDialog>(new DialogParameters
        {
            { "PreFillFromNodeId", "host-node-1" }
        });

        var html = provider.Find(".mud-dialog").InnerHtml;
        Assert.Contains("点击右侧按钮选择节点", html);
        Assert.Contains("语义搜索选择终点节点", html);
    }

    [Fact]
    public async Task EdgeDialog_PreFillTypeName_SkipsTypeSelection()
    {
        var provider = await OpenAsync<EdgeDialog>(new DialogParameters
        {
            { "PreFillFromNodeId", "X" },
            { "PreFillTypeName", "Collaboration" }
        });

        AssertDialog(provider, "创建边");
        // 类型为文本输入（无下拉选择）；预填的类型名直接回填
        var html = provider.Find(".mud-dialog").InnerHtml;
        Assert.Contains("关系类型名", html);
        Assert.Contains("Collaboration", html);
    }

    [Fact]
    public async Task EdgeDialog_CreateMode_ShowsDirectedSwitch()
    {
        // 新建边默认为有向，开关标签随状态切换；权重/显示名输入已移除（R4）
        var provider = await OpenAsync<EdgeDialog>(new DialogParameters
        {
            { "PreFillFromNodeId", "host-node-1" }
        });

        var html = provider.Find(".mud-dialog").InnerHtml;
        Assert.Equal("有向（起点 → 终点）",
            provider.FindComponent<MudSwitch<bool>>().Instance.Label);
        Assert.DoesNotContain("权重", html);
        Assert.DoesNotContain("显示名", html);
    }

    [Fact]
    public async Task EdgeDialog_EditMode_UndirectedSwitchState()
    {
        // 编辑无向边：开关标签应显示「无向（双向等同）」且类型名只读
        var provider = await OpenAsync<EdgeDialog>(new DialogParameters
        {
            { "Editing", EdgeInstance(isDirected: false) }
        });

        AssertDialog(provider, "编辑边");
        Assert.Equal("无向（双向等同）",
            provider.FindComponent<MudSwitch<bool>>().Instance.Label);
    }

    // ─── FakeHandler ───

    private sealed class FakeHandler(Func<string, HttpMethod, string?> route)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? "";
            var body = route(path, request.Method);
            return Task.FromResult(new HttpResponseMessage(
                body is null ? HttpStatusCode.NotFound : HttpStatusCode.OK)
            {
                Content = new StringContent(body ?? "", Encoding.UTF8, "application/json")
            });
        }
    }
}
