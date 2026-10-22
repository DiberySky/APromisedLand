using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using MudBlazor.Services;
using TreeGraph.Blazor.Shared.EdgeSky.Services;
using TreeGraph.Blazor.Shared.LineSky.Dialogs;
using TreeGraph.Blazor.Shared.LineSky.Services;
using TreeGraph.Shared.EdgeSky.Dtos;
using TreeGraph.Shared.LineSky.Dtos;
using TreeGraph.Shared.LineSky.Entities;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.LineSky;

/// <summary>
/// LineStructureDialog 组件冒烟测试。
///
/// 覆盖：初始化加载、拓扑模式渲染、端子下拉状态选项、诊断区 Warnings/TieCount/FeedInEnergizedCount 展示。
/// 模式对齐 EdgeDialogSmokeTests：真实 MudDialogProvider + 桩 HttpClient + Route 按 URL 返回 canned JSON。
/// </summary>
public class LineStructureDialogSmokeTests : BunitTestBase
{
    private const string TestLineId = "line-test-001";

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
        Services.AddSingleton<EdgeApiClient>();
    }

    private async Task<IRenderedComponent<MudDialogProvider>> OpenAsync(DialogParameters parameters)
    {
        var provider = Render<MudDialogProvider>();
        var ds = Services.GetRequiredService<IDialogService>();
        var _ = await ds.ShowAsync<LineStructureDialog>(string.Empty, parameters, new DialogOptions());
        provider.Render();

        // 多等几次渲染（bUnit 事件循环可能还没跑完所有 await）
        for (var i = 0; i < 3; i++)
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
        // GET /api/eav/lines/{lineId}/structure
        if (path.StartsWith($"/api/eav/lines/{TestLineId}/structure") && method == HttpMethod.Get)
            return Envelope(ExistingStructure());

        // GET /api/eav/metadata/edge-types
        if (path.StartsWith("/api/eav/metadata/edge-types") && method == HttpMethod.Get)
            return Envelope(EdgeTypes());

        // 任何写操作（POST/PUT/DELETE）回结构（简化）
        if (path.StartsWith($"/api/eav/lines/") && path.Contains("/structure"))
            return Envelope(ExistingStructure());

        return null;
    }

    // ============================================================
    // 测试数据 DTO
    // ============================================================

    private static EdgeTypeDto EdgeType() => new(
        EdgeTypeId: "et-line-1", TypeName: "RailLine", DisplayName: "铁路贯通线",
        FromNodeType: "LineNode", ToNodeType: "LineNode",
        IsGlobal: true, IsDirected: true,
        Description: null, IsActive: true, IsLine: true, IsLinear: true);

    private static List<EdgeTypeDto> EdgeTypes() => new() { EdgeType() };

    private static LineStructureDto ExistingStructure() => new()
    {
        Exists = true,
        LineId = TestLineId,
        LineName = "测试贯通线",
        EdgeTypeId = "et-line-1",
        EdgeTypeName = "RailLine",
        EdgeTypeDisplayName = "铁路贯通线",
        TopologyMode = TopologyMode.Network,
        OrderedNodeIds = new List<string> { "node-1", "node-2", "node-3" },
        Segments = new List<LineStructureSegmentDto>
        {
            new() { EdgeId = "seg-1", FromNodeId = "node-1", ToNodeId = "node-2", Weight = null },
            new() { EdgeId = "seg-2", FromNodeId = "node-2", ToNodeId = "node-3", Weight = null },
            new() { EdgeId = "seg-3", FromNodeId = "node-3", ToNodeId = "node-1", Weight = null }
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
        CandidateNodes = new List<LineStructureNodeDto>
        {
            new() { NodeId = "node-1", Name = "牵引所A" },
            new() { NodeId = "node-2", Name = "分区所" },
            new() { NodeId = "node-3", Name = "牵引所B" },
            new() { NodeId = "node-orphan", Name = "游离节点X" }
        },
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
        // FeedIn + 投运数
        Assert.Contains("电源接入点 1（投运 1）", html);
        // Tie
        Assert.Contains("联络点 1", html);
        // Warnings 出现（游离节点）
        Assert.Contains("游离节点X", html);
        // 端点 / 成环
        Assert.Contains("成环 是", html);
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
    public async Task ExistingStructure_TopologyModeDefaultIsNetwork()
    {
        var provider = await OpenAsync(new DialogParameters
        {
            { nameof(LineStructureDialog.LineId), TestLineId },
            { nameof(LineStructureDialog.LineName), "测试贯通线" }
        });

        var html = DialogHtml(provider);
        // 诊断区 Valid Chip
        Assert.Contains("拓扑校验通过", html);
        // Network 模式的 tip 文字（用 Network TopologyUsage 里真实存在的文字）
        Assert.Contains("牵引供电", html);
    }

    [Fact]
    public async Task TerminalOperatingState_Dropdown_ControlExists()
    {
        // 下拉控件本身渲染（选项是点击后才渲染的 mud-select-menu，这里只验证控件存在）
        var provider = await OpenAsync(new DialogParameters
        {
            { nameof(LineStructureDialog.LineId), TestLineId },
            { nameof(LineStructureDialog.LineName), "测试贯通线" }
        });

        var html = DialogHtml(provider);
        // 运行状态下拉的 label 存在
        Assert.Contains("运行状态", html);
        // 已选端子的状态 Chip 正确渲染（投运、冷备用）
        Assert.Contains("投运", html);
        Assert.Contains("冷备用", html);
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
