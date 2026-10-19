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
using TreeGraph.Shared.NodeEavSky.Dtos;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.NodeEavSky;

/// <summary>
/// Edge（关系/边）对话框组件冒烟测试。
/// 模式对齐 NodeEavDialogSmokeTests：真实 MudDialogProvider + 桩 HttpClient，
/// 仅验证对话框能正常渲染（标题正确、不抛异常），提交逻辑不在本冒烟范围。
/// </summary>
public class EdgeDialogSmokeTests : BunitTestBase
{
    public EdgeDialogSmokeTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLogging();
        Services.AddMudServices();
        Services.AddSingleton<IPlatformContext>(new FakePlatformContext());

        // Edge 对话框初始化时会调：ListEdgeTypes, ListEntityTypes
        Services.AddSingleton(new HttpClient(new FakeHandler(Route))
        {
            BaseAddress = new Uri("http://localhost/")
        });
        Services.AddSingleton<EavApiClient>();
        Services.AddSingleton<EdgeApiClient>();
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

    // ─── EdgeDefinition DTOs（测试数据） ───

    private static EdgeTypeDto EdgeType() => new(
        EdgeTypeId: "et-edge-1", TypeName: "Collaboration", DisplayName: "协作关系",
        FromNodeType: "Department", ToNodeType: "Department",
        IsGlobal: true, IsDirected: true,
        Description: null, IsActive: true, IsLine: false, IsLinear: true);

    private static EdgeInstanceDto EdgeInstance() => new(
        EdgeId: "e-1", EdgeTypeId: "et-edge-1",
        EdgeTypeName: "Collaboration", EdgeTypeDisplayName: "协作关系",
        FromNodeId: "node-a", FromNodeText: "",
        ToNodeId: "node-b", ToNodeText: "",
        Weight: 3.14m, DisplayName: null,
        CreatedAt: DateTimeOffset.UtcNow,
        UpdatedAt: DateTimeOffset.UtcNow, Properties: null);

    private static List<EntityTypeSummaryDto> EntityTypeSummaries() => new()
    {
        new EntityTypeSummaryDto("Department", 0, 0, "部门", "dept-1", "部门"),
        new EntityTypeSummaryDto("Project", 0, 0, "项目", "proj-1", "项目")
    };

    private static List<EdgeTypeDto> EdgeTypes() => new() { EdgeType() };

    // ─── 路由桩：按 URL 返回 canned JSON ───

    private static string? Route(string path, HttpMethod method)
    {
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        string Envelope(object? data)
            => JsonSerializer.Serialize(new { success = true, message = (string?)null, data }, web);

        if (path.StartsWith("/api/eav/metadata/edge-types") && method == HttpMethod.Get)
            return Envelope(EdgeTypes());

        if (path.StartsWith("/api/eav/metadata/entity-types/summary") && method == HttpMethod.Get)
            return Envelope(EntityTypeSummaries());

        // EdgeDialog 创建模式下可能还会调 GetSchemaAsync
        if (path.StartsWith("/api/eav/Collaboration/schema") && method == HttpMethod.Get)
            return Envelope(new { attributes = Array.Empty<object>() });

        // 宿主 inode 选择器：语义搜索
        if (path.StartsWith("/api/search/semantic") && method == HttpMethod.Post)
            return Envelope(new { query = "q", total = 0, items = Array.Empty<object>() });

        return null;
    }

    // ─── 测试用例 ───

    [Fact]
    public async Task EdgeDefinitionCreateDialog_Renders()
    {
        var provider = await OpenAsync<EdgeDefinitionCreateDialog>();
        AssertDialog(provider, "新建边类型");

        // 回归：PascalCase 校验 HelperText 应渲染
        var html = provider.Find(".mud-dialog").InnerHtml;
        Assert.Contains("类型名", html);
        Assert.Contains("显示名", html);
    }

    [Fact]
    public async Task EdgeDefinitionEditDialog_Renders()
    {
        var provider = await OpenAsync<EdgeDefinitionEditDialog>(
            new DialogParameters<EdgeDefinitionEditDialog>
            {
                { x => x.Type, EdgeType() }
            });

        AssertDialog(provider, "编辑边类型");
        var html = provider.Find(".mud-dialog").InnerHtml;
        // TypeName 在编辑模式只读
        Assert.Contains("类型名", html);
        Assert.Contains("全局共享", html);
        Assert.Contains("有向", html);
    }

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
        Assert.Contains("边类型", html);
        Assert.Contains("起点节点", html);
        Assert.Contains("终点节点", html);
        Assert.Contains("权重", html);
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
        // 编辑模式：TypeName 只读展示
        Assert.Contains("协作关系", html);
        Assert.Contains("权重", html);
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
        Assert.Contains("点击右侧按钮选择宿主节点", html);
        Assert.Contains("语义搜索选择宿主节点", html);
    }

    [Fact]
    public async Task EdgeDialog_PreFillEdgeType_SkipsTypeSelection()
    {
        var provider = await OpenAsync<EdgeDialog>(new DialogParameters
        {
            { "PreFillFromNodeId", "X" },
            { "PreFillEdgeTypeId", "et-edge-1" }
        });

        AssertDialog(provider, "创建边");
        // 类型选择下拉应该渲染
        var html = provider.Find(".mud-dialog").InnerHtml;
        Assert.Contains("边类型", html);
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
