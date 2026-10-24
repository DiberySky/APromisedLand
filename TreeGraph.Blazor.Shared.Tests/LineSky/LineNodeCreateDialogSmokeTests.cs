using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using TreeGraph.Blazor.Shared.LineSky;
using TreeGraph.Blazor.Shared.LineSky.Dialogs;
using TreeGraph.Blazor.Shared.LineSky.Services;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.LineSky;

/// <summary>
/// LineNodeCreateDialog 冒烟测试（R7：创建时可选择「前驱」，选后自动建立 前驱 → 新节点 段边）。
/// 覆盖：前驱下拉渲染与候选加载、创建请求携带/归一化 PredecessorId。
/// 模式对齐 LineStructureDialogSmokeTests：真实 MudDialogProvider + 桩 HttpClient。
/// </summary>
public class LineNodeCreateDialogSmokeTests : BunitTestBase
{
    private const string TestLineId = "line-test-001";

    public LineNodeCreateDialogSmokeTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLogging();
        Services.AddMudServices();
        Services.AddSingleton(new HttpClient(new CapturingHandler())
        {
            BaseAddress = new Uri("http://localhost/")
        });
        Services.AddSingleton<LineNodeManageApiClient>();
    }

    // ============================================================
    // 桩 HttpClient：GET 回两个候选节点；POST 捕获请求体
    // ============================================================

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private static string? LastPostBody;

    private static string Envelope(object? data)
        => JsonSerializer.Serialize(new { Success = true, Message = (string?)null, Data = data }, WebJson);

    private sealed class CapturingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? "";

            if (request.Method == HttpMethod.Get && path.EndsWith("/api/LineNode"))
            {
                return Json(Envelope(new[]
                {
                    new LineNodeDto { Id = "n1", LineId = TestLineId, Name = "牵引所A" },
                    new LineNodeDto { Id = "n2", LineId = TestLineId, Name = "分区所" }
                }));
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/api/LineNode"))
            {
                LastPostBody = request.Content is null
                    ? null
                    : await request.Content.ReadAsStringAsync(cancellationToken);
                return Json(Envelope(new LineNodeDto
                {
                    Id = "n-new", LineId = TestLineId, Name = "新节点"
                }));
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage Json(string body)
            => new(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
    }

    private async Task<IRenderedComponent<MudDialogProvider>> OpenAsync()
    {
        LastPostBody = null;
        var provider = Render<MudDialogProvider>();
        var ds = Services.GetRequiredService<IDialogService>();
        var _ = await ds.ShowAsync<LineNodeCreateDialog>(string.Empty,
            new DialogParameters { { nameof(LineNodeCreateDialog.LineId), TestLineId } },
            new DialogOptions());
        provider.Render();

        // 等候选节点加载完成（OnInitializedAsync → GET api/LineNode?lineId=...）
        for (var i = 0; i < 10; i++)
        {
            await Task.Delay(50);
            provider.Render();
        }
        return provider;
    }

    private static object? Field(IRenderedComponent<LineNodeCreateDialog> dialog, string name)
        => typeof(LineNodeCreateDialog)
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(dialog.Instance);

    private static Task InvokeCreateAsync(IRenderedComponent<LineNodeCreateDialog> dialog)
        => (Task)typeof(LineNodeCreateDialog)
            .GetMethod("CreateAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(dialog.Instance, null)!;

    // ============================================================
    // 用例
    // ============================================================

    [Fact]
    public async Task Renders_PredecessorSelect_AndLoadsCandidates()
    {
        var provider = await OpenAsync();

        var html = provider.Find(".mud-dialog").InnerHtml;
        Assert.Contains("新建线路节点", html);
        // 前驱下拉 label（选项在 MudPopover 内，bUnit 不展开）
        Assert.Contains("前驱（可选，选后接在该节点之后）", html);

        // 候选已加载（同线已有节点）
        var dialog = provider.FindComponent<LineNodeCreateDialog>();
        var nodes = (IReadOnlyList<LineNodeDto>)Field(dialog, "_existingNodes")!;
        Assert.Equal(2, nodes.Count);
        Assert.Contains(nodes, n => n.Id == "n1");
    }

    [Fact]
    public async Task CreateAsync_WithPredecessor_PostsPredecessorId()
    {
        var provider = await OpenAsync();
        var dialog = provider.FindComponent<LineNodeCreateDialog>();

        var form = (LineNodeDto)Field(dialog, "_form")!;
        form.Name = "新节点";
        form.PredecessorId = "n1";

        await InvokeCreateAsync(dialog);

        Assert.NotNull(LastPostBody);
        Assert.Contains("\"predecessorId\":\"n1\"", LastPostBody);
        Assert.Contains("\"lineId\":\"" + TestLineId + "\"", LastPostBody);
    }

    [Fact]
    public async Task CreateAsync_EmptyPredecessor_NormalizesToNull()
    {
        var provider = await OpenAsync();
        var dialog = provider.FindComponent<LineNodeCreateDialog>();

        var form = (LineNodeDto)Field(dialog, "_form")!;
        form.Name = "首节点";
        form.PredecessorId = "";   // 下拉「无（首节点）」/ 清空后为空串

        await InvokeCreateAsync(dialog);

        Assert.NotNull(LastPostBody);
        // 空串归一化为 null：首节点不建边
        Assert.Contains("\"predecessorId\":null", LastPostBody);
    }
}
