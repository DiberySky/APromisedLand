using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using TreeGraph.Blazor.Shared.LineSky.Dialogs;
using TreeGraph.Blazor.Shared.LineSky.Services;
using TreeGraph.Shared.LineSky.Dtos;
using TreeGraph.Shared.LineSky.Entities;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.LineSky;

/// <summary>
/// LineStructureDialog 组件冒烟测试（R7 边集薄层形态）。
///
/// R7：拓扑模式整体下线（无拓扑字段 / 无拓扑下拉 / 段无 Weight），
/// 段列表 = relation 边列表（起点/终点/类型/显示名），模板只预填端子默认值。
/// 覆盖：初始化加载、诊断 chips、段边列表、端子运行状态、模板预填、空节点提示、请求 DTO 形状。
/// 模式对齐其它冒烟测试：真实 MudDialogProvider + 桩 HttpClient + Route 按 URL 返回 canned JSON。
/// </summary>
public class LineStructureDialogSmokeTests : BunitTestBase
{
    private const string TestLineId = "line-test-001";
    /// <summary>尚无线路节点的线路（空节点提示用例）。</summary>
    private const string TestNewLineId = "line-test-new-001";

    public LineStructureDialogSmokeTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLogging();
        Services.AddMudServices();
        Services.AddSingleton(new HttpClient(new FakeHandler(Route))
        {
            BaseAddress = new Uri("http://localhost/")
        });
        Services.AddSingleton<LineApiClient>();
    }

    private async Task<IRenderedComponent<MudDialogProvider>> OpenAsync(DialogParameters parameters)
    {
        var provider = Render<MudDialogProvider>();
        var ds = Services.GetRequiredService<IDialogService>();
        var _ = await ds.ShowAsync<LineStructureDialog>(string.Empty, parameters, new DialogOptions());
        provider.Render();

        // 多等几次渲染（bUnit 事件循环可能还没跑完所有 await，包括 LoadAsync 里的 API call 和模板加载）
        for (var i = 0; i < 10; i++)
        {
            await Task.Delay(50);
            provider.Render();
        }
        return provider;
    }

    private static string DialogHtml(IRenderedComponent<MudDialogProvider> provider)
        => provider.Find(".mud-dialog").InnerHtml;

    // ============================================================
    // Route: 按 URL 返回 canned JSON（统一 ApiResponse 信封）
    // ============================================================

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);
    // 注意：LineApiClient 的 JsonOptions 没加 JsonStringEnumConverter，
    // 所以枚举字段需要用整数序列化成和后端兼容的格式。

    private static string Envelope(object? data)
        => JsonSerializer.Serialize(new { Success = true, Message = (string?)null, Data = data }, WebJson);

    private static string? Route(string path, HttpMethod method)
    {
        // GET /api/eav/lines/{TestNewLineId}/structure —— 尚无线路节点
        if (path.StartsWith($"/api/eav/lines/{TestNewLineId}/structure") && method == HttpMethod.Get)
            return Envelope(NewStructure());

        // GET /api/eav/lines/{lineId}/structure
        if (path.StartsWith($"/api/eav/lines/{TestLineId}/structure") && method == HttpMethod.Get)
            return Envelope(ExistingStructure());

        // GET /api/eav/line-templates（模板下拉数据源）
        if (path.StartsWith("/api/eav/line-templates") && method == HttpMethod.Get)
            return Envelope(Templates());

        // 任何写操作（POST/PUT/DELETE）回结构（简化）
        if (path.StartsWith($"/api/eav/lines/") && path.Contains("/structure"))
            return Envelope(ExistingStructure());

        return null;
    }

    // ============================================================
    // 测试数据 DTO
    // ============================================================

    private static List<LineTemplateDto> Templates() => new()
    {
        new LineTemplateDto(
            Key: "RailwayFeeding",
            DisplayName: "铁路牵引供电",
            Description: "多电源、允许成环、多所联络",
            DefaultTerminalRole: "FeedIn",
            DefaultOperatingState: "Maintenance")
    };

    private static LineStructureDto NewStructure() => new()
    {
        Exists = false,
        LineId = TestNewLineId,
        LineName = "新建贯通线",
        OrderedNodeIds = [],
        Nodes = [],
        Segments = [],
        Terminals = [],
        CandidateNodes = [],
        Diagnostics = new()
    };

    private static LineStructureDto ExistingStructure() => new()
    {
        Exists = true,
        LineId = TestLineId,
        LineName = "测试贯通线",
        OrderedNodeIds = new List<string> { "node-1", "node-2", "node-3" },
        Segments = new List<LineStructureSegmentDto>
        {
            new() { EdgeId = "seg-1", FromNodeId = "node-1", FromNodeName = "牵引所A",
                    ToNodeId = "node-2", ToNodeName = "分区所",
                    EdgeType = "line_segment", DisplayName = "主线段" },
            new() { EdgeId = "seg-2", FromNodeId = "node-2", FromNodeName = "分区所",
                    ToNodeId = "node-3", ToNodeName = "牵引所B",
                    EdgeType = "line_segment" },
            new() { EdgeId = "seg-3", FromNodeId = "node-3", FromNodeName = "牵引所B",
                    ToNodeId = "node-1", ToNodeName = "牵引所A",
                    EdgeType = "line_segment" }
        },
        Nodes = new List<LineStructureNodeDto>
        {
            new() { NodeId = "node-1", Name = "牵引所A", TerminalRole = TerminalRole.FeedIn,
                    OperatingState = TerminalOperatingState.Energized },
            new() { NodeId = "node-2", Name = "分区所" },
            new() { NodeId = "node-3", Name = "牵引所B", TerminalRole = TerminalRole.Tie,
                    OperatingState = TerminalOperatingState.ColdStandby }
        },
        Terminals = new List<LineStructureNodeDto>
        {
            new() { NodeId = "node-1", Name = "牵引所A", TerminalRole = TerminalRole.FeedIn,
                    OperatingState = TerminalOperatingState.Energized },
            new() { NodeId = "node-3", Name = "牵引所B", TerminalRole = TerminalRole.Tie,
                    OperatingState = TerminalOperatingState.ColdStandby }
        },
        CandidateNodes = [],
        Diagnostics = new LineStructureDiagnosticsDto
        {
            Valid = true,
            Errors = new List<string>(),
            Warnings = new List<string>
            {
                "线路下有 1 个节点未纳入结构段（游离）：游离节点X"
            },
            NodeCount = 3, EdgeCount = 3,
            NodeDegreeMax = 2, NodeDegreeOneCount = 0,
            HasCycle = true,
            FeedInCount = 1, FeedInEnergizedCount = 1,
            TieCount = 1
        }
    };

    // ============================================================
    // 用例
    // ============================================================

    [Fact]
    public async Task ExistingStructure_RendersWithoutException()
    {
        var provider = await OpenAsync(new DialogParameters
        {
            { nameof(LineStructureDialog.LineId), TestLineId },
            { nameof(LineStructureDialog.LineName), "测试贯通线" }
        });

        var html = DialogHtml(provider);
        Assert.Contains("线路结构", html);
        Assert.Contains("测试贯通线", html);
    }

    [Fact]
    public async Task ExistingStructure_DiagnosticsShowsCounts()
    {
        var provider = await OpenAsync(new DialogParameters
        {
            { nameof(LineStructureDialog.LineId), TestLineId },
            { nameof(LineStructureDialog.LineName), "测试贯通线" }
        });

        var html = DialogHtml(provider);
        // Valid Chip（R7：拓扑校验 → 诊断）
        Assert.Contains("诊断无异常", html);
        // FeedIn + 投运数
        Assert.Contains("电源接入点 1（投运 1）", html);
        // Tie
        Assert.Contains("联络点 1", html);
        // Warnings 出现（游离节点）
        Assert.Contains("游离节点X", html);
        // 成环
        Assert.Contains("成环 是", html);
    }

    [Fact]
    public async Task ExistingStructure_SegmentTable_RendersEdgeColumns()
    {
        var provider = await OpenAsync(new DialogParameters
        {
            { nameof(LineStructureDialog.LineId), TestLineId },
            { nameof(LineStructureDialog.LineName), "测试贯通线" }
        });

        var html = DialogHtml(provider);
        // 段表列头：起点/终点/类型/显示名
        Assert.Contains("起点", html);
        Assert.Contains("终点", html);
        Assert.Contains("类型", html);
        Assert.Contains("显示名", html);
        // line_segment → 「结构段」标签
        Assert.Contains("结构段", html);
        // 段显示名（relation.name）
        Assert.Contains("主线段", html);
        // 段两端节点名
        Assert.Contains("牵引所A", html);
        Assert.Contains("分区所", html);
    }

    [Fact]
    public async Task ExistingStructure_TerminalOperatingStateColumn_Renders()
    {
        var provider = await OpenAsync(new DialogParameters
        {
            { nameof(LineStructureDialog.LineId), TestLineId },
            { nameof(LineStructureDialog.LineName), "测试贯通线" }
        });

        var html = DialogHtml(provider);
        // 状态列标题存在
        Assert.Contains("运行状态", html);
        // 两个端子各自的状态文本
        Assert.Contains("投运", html);
        Assert.Contains("冷备用", html);
    }

    [Fact]
    public async Task TemplateDropdown_ControlExists_TopologySelectorRemoved()
    {
        // R7：拓扑模式选择器整体删除；模板下拉保留（只预填端子默认值）。
        var provider = await OpenAsync(new DialogParameters
        {
            { nameof(LineStructureDialog.LineId), TestLineId },
            { nameof(LineStructureDialog.LineName), "测试贯通线" }
        });

        var html = DialogHtml(provider);

        // 模板 MudSelect 的 label 存在 → 确认控件渲染成功
        Assert.Contains("模板", html);
        // 拓扑模式 / 边类型选择器已下线
        Assert.DoesNotContain("拓扑模式", html);
        Assert.DoesNotContain("线路型边类型", html);
    }

    [Fact]
    public async Task SelectingTemplate_AppliesTerminalDefaults()
    {
        // 模板切换后的状态验证（模板只预填端子默认值：角色 FeedIn / 状态 Maintenance）。
        var provider = await OpenAsync(new DialogParameters
        {
            { nameof(LineStructureDialog.LineId), TestLineId },
            { nameof(LineStructureDialog.LineName), "测试贯通线" }
        });

        // bUnit 无法打开 MudSelect 弹层（选项在 MudPopover 内且需要 JS 定位），
        // 因此直接调用 Dialog 的模板选择处理器（MudSelect 的 ValueChanged 绑定的正是该方法）。
        var dialog = provider.FindComponent<LineStructureDialog>();
        typeof(LineStructureDialog)
            .GetMethod("OnSelectedTemplateKeyChanged", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(dialog.Instance, new object?[] { "RailwayFeeding" });
        dialog.Render();

        // 端子默认值字段（private string，枚举名）
        var type = typeof(LineStructureDialog);
        var role = type.GetField("_terminalRole",
            BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(dialog.Instance);
        var state = type.GetField("_terminalOperatingState",
            BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(dialog.Instance);
        Assert.Equal(nameof(TerminalRole.FeedIn), role);
        Assert.Equal(nameof(TerminalOperatingState.Maintenance), state);

        // Snackbar 确认消息
        var snackbar = Services.GetRequiredService<ISnackbar>();
        Assert.Contains(snackbar.ShownSnackbars,
            s => s.Message.Contains("已应用铁路牵引供电预设"));
    }

    [Fact]
    public async Task EmptyNodes_RendersInfoAlert()
    {
        // 该线路尚无线路节点：提示先去「线路节点」添加。
        var provider = await OpenAsync(new DialogParameters
        {
            { nameof(LineStructureDialog.LineId), TestNewLineId },
            { nameof(LineStructureDialog.LineName), "新建贯通线" }
        });

        var html = DialogHtml(provider);
        Assert.Contains("该线路暂无线路节点", html);
    }

    [Fact]
    public void LineStructureRequest_Serialization_OnlySegmentsAndTerminals()
    {
        // R7：线路结构请求只携带段/端子，不再携带拓扑模式与边类型。
        var request = new LineStructureRequest
        {
            Segments = new List<LineStructureSegmentInput>
            {
                new() { FromNodeId = "node-1", ToNodeId = "node-2", DisplayName = "主线段" }
            }
        };

        var json = JsonSerializer.Serialize(request, WebJson);

        Assert.Contains("segments", json);
        Assert.DoesNotContain("topologyMode", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("edgeType", json, StringComparison.OrdinalIgnoreCase);
    }

    // ============================================================
    // FakeHandler
    // ============================================================

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
