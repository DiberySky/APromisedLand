using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using TreeGraph.Blazor.Shared.LineSky;
using TreeGraph.Blazor.Shared.LineSky.Components;
using TreeGraph.Blazor.Shared.LineSky.Dialogs;
using TreeGraph.Blazor.Shared.LineSky.Services;
using TreeGraph.Blazor.Shared.NodeEavSky.Services;
using TreeGraph.Blazor.Shared.TreeEavSky.Dialogs;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.LineSky;

/// <summary>
/// LineNodeActionsDialog 冒烟测试（宿主操作模式：基本信息内嵌 + 按钮仅返回 LineNodeHostAction）。
/// 覆盖：基本信息/按钮渲染、「线路节点维护」按 ShowLineManage 显隐、按钮关窗动作契约。
/// 模式对齐 LineNodeCreateDialogSmokeTests：真实 MudDialogProvider + 桩 HttpClient。
/// </summary>
public class LineNodeActionsDialogSmokeTests : BunitTestBase
{
    private static readonly LineNodeDto TestNode = new()
    {
        Id = "n1",
        LineId = "line-test-001",
        Name = "牵引所A",
        Description = "测试节点",
        PredecessorId = null,
        PredecessorName = null,
        CreatedAt = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero)
    };

    public LineNodeActionsDialogSmokeTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLogging();
        Services.AddMudServices();
        Services.AddSingleton(new HttpClient(new StubHandler())
        {
            BaseAddress = new Uri("http://localhost/")
        });
        Services.AddSingleton<LineNodeManageApiClient>();
        // NodeAttributeItemsSky 依赖：EavApiClient + MessageService
        Services.AddSingleton<EavApiClient>();
        Services.AddScoped<MessageService>();
    }

    // ============================================================
    // 桩 HttpClient：属性区 GET /api/inode/* 回空列表
    // ============================================================

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private static string Envelope(object? data)
        => JsonSerializer.Serialize(new { Success = true, Message = (string?)null, Data = data }, WebJson);

    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? "";

            if (request.Method == HttpMethod.Get && path.StartsWith("/api/inode", StringComparison.Ordinal))
                return Task.FromResult(Json(Envelope(Array.Empty<object>())));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static HttpResponseMessage Json(string body)
            => new(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
    }

    private async Task<(IRenderedComponent<MudDialogProvider> Provider, IDialogReference Reference)> OpenAsync(
        bool showLineManage)
    {
        var provider = Render<MudDialogProvider>();
        var ds = Services.GetRequiredService<IDialogService>();
        var reference = await ds.ShowAsync<LineNodeActionsDialog>("节点操作",
            new DialogParameters
            {
                { nameof(LineNodeActionsDialog.Node), TestNode },
                { nameof(LineNodeActionsDialog.LineName), "测试线路" },
                { nameof(LineNodeActionsDialog.ShowLineManage), showLineManage }
            },
            new DialogOptions());
        provider.Render();

        // 等属性区加载完成（NodeAttributeItemsSky → GET api/inode/*）
        for (var i = 0; i < 10; i++)
        {
            await Task.Delay(50);
            provider.Render();
        }
        return (provider, reference);
    }

    private static void ClickButton(IRenderedComponent<MudDialogProvider> provider, string text)
    {
        var btn = provider.FindAll("button")
            .First(b => b.TextContent.Trim().Contains(text, StringComparison.Ordinal));
        btn.Click();
    }

    // ============================================================
    // 用例
    // ============================================================

    [Fact]
    public async Task Renders_BasicInfo_AndActionButtons()
    {
        var (provider, _) = await OpenAsync(showLineManage: true);

        var html = provider.Find(".mud-dialog").InnerHtml;
        Assert.Contains("节点操作", html);
        Assert.Contains("测试线路", html);
        Assert.Contains("牵引所A", html);
        Assert.Contains("测试节点", html);
        Assert.Contains("无（首节点）", html);

        Assert.Contains("编辑", html);
        Assert.Contains("属性", html);
        Assert.Contains("删除", html);
        Assert.Contains("线路节点维护", html);
    }

    [Fact]
    public async Task Hides_LineManage_WhenDisabled()
    {
        var (provider, _) = await OpenAsync(showLineManage: false);

        var html = provider.Find(".mud-dialog").InnerHtml;
        Assert.Contains("编辑", html);
        Assert.DoesNotContain("线路节点维护", html);
    }

    [Fact]
    public async Task ClickEdit_ClosesWithEditAction()
    {
        var (provider, reference) = await OpenAsync(showLineManage: true);

        ClickButton(provider, "编辑");

        var result = await reference.Result.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(result.Canceled);
        Assert.Equal(LineNodeHostAction.Edit, Assert.IsType<LineNodeHostAction>(result.Data));
    }

    [Fact]
    public async Task ClickDelete_ClosesWithDeleteAction()
    {
        var (provider, reference) = await OpenAsync(showLineManage: true);

        ClickButton(provider, "删除");

        var result = await reference.Result.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(result.Canceled);
        Assert.Equal(LineNodeHostAction.Delete, Assert.IsType<LineNodeHostAction>(result.Data));
    }
}
