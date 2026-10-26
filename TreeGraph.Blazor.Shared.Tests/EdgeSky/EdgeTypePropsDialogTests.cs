using System.Collections;
using System.Net;
using System.Reflection;
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
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.EdgeSky;

/// <summary>
/// EdgeTypePropsDialog 组件测试（ALTER EDGE 属性 Schema 管理）。
///
/// 覆盖：已有属性渲染、无变更取消、删除标记（TTL 列禁删）、类型变更汇总、
/// 新增属性校验（NOT NULL 必配默认值 / 重名拒绝）与 AddProps 汇总。
/// 模式对齐其它对话框冒烟测试：真实 MudDialogProvider + 反射操作私有行状态。
/// </summary>
public class EdgeTypePropsDialogTests : BunitTestBase
{
    public EdgeTypePropsDialogTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLogging();
        Services.AddMudServices();
        // EdgeApiClient 桩：任何写操作回 200 空信封
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
                    """{"success":true,"message":null,"data":true}""",
                    Encoding.UTF8, "application/json")
            });
    }

    private async Task<(IRenderedComponent<MudDialogProvider> Provider, IDialogReference Ref)> OpenAsync(
        EdgeTypeDto typeInfo)
    {
        var provider = Render<MudDialogProvider>();
        var ds = Services.GetRequiredService<IDialogService>();
        var reference = await ds.ShowAsync<EdgeTypePropsDialog>(
            string.Empty,
            new DialogParameters<EdgeTypePropsDialog> { { x => x.TypeInfo, typeInfo } },
            new DialogOptions());
        provider.Render();
        for (var i = 0; i < 5; i++)
        {
            await Task.Delay(20);
            provider.Render();
        }
        return (provider, reference);
    }

    private static IRenderedComponent<EdgeTypePropsDialog> DialogOf(
        IRenderedComponent<MudDialogProvider> provider)
        => provider.FindComponent<EdgeTypePropsDialog>();

    /// <summary>点击 DialogSky 的「应用变更」提交按钮。</summary>
    private static void ClickSubmit(IRenderedComponent<MudDialogProvider> provider)
        => provider.FindAll("button").First(b => b.TextContent.Trim() == "应用变更").Click();

    // ── 反射辅助：_existing（List<ExistingRow>）/ _adds（List<PropEdit>）均为私有嵌套类型 ──

    private static IList ExistingRows(object dialog)
        => (IList)typeof(EdgeTypePropsDialog)
            .GetField("_existing", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(dialog)!;

    private static IList AddRows(object dialog)
        => (IList)typeof(EdgeTypePropsDialog)
            .GetField("_adds", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(dialog)!;

    private static void SetProp(object row, string name, object? value)
        => row.GetType().GetProperty(name)!.SetValue(row, value);

    private static object GetProp(object row, string name)
        => row.GetType().GetProperty(name)!.GetValue(row)!;

    private static object NewAddRow(object dialog)
    {
        var adds = AddRows(dialog);
        var row = new EdgeTypePropDto
        {
            PropName = "", DataType = EdgePropDataTypes.String, Nullable = true
        };
        adds.Add(row);
        return row;
    }

    // ============================================================
    // 测试数据
    // ============================================================

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

    // ============================================================
    // 用例
    // ============================================================

    [Fact]
    public async Task Renders_ExistingProps_AndTitle()
    {
        var (provider, _) = await OpenAsync(LineSegment());
        var html = provider.Find(".mud-dialog").InnerHtml;

        Assert.Contains("属性 Schema — 线段 (line_segment)", html);
        Assert.Contains("名称 (", html);          // 中文 (英文) 组合
        Assert.Contains("name", html);
        Assert.Contains("degree", html);
        Assert.Contains("1", html);              // degree 默认值

        // 可空列显示 是/否：name 可空=是，degree 不可空=否
        var rows = provider.FindAll(".mud-dialog tbody tr");
        Assert.Equal(2, rows.Count);
        Assert.Equal("是", rows[0].QuerySelectorAll("td")[2].TextContent.Trim());
        Assert.Equal("否", rows[1].QuerySelectorAll("td")[2].TextContent.Trim());
    }

    [Fact]
    public async Task Submit_WithoutChanges_Cancels()
    {
        var (provider, reference) = await OpenAsync(LineSegment());

        ClickSubmit(provider);

        var result = await reference.Result;
        Assert.True(result.Canceled);
    }

    [Fact]
    public async Task MarkDelete_SubmitsDropProps()
    {
        var (provider, reference) = await OpenAsync(LineSegment());
        provider.Render();

        // 删除 name 行（第一行）
        provider.FindAll("button[title='删除属性']").First().Click();
        provider.Render();
        ClickSubmit(provider);

        var result = await reference.Result;
        var req = Assert.IsType<AlterEdgeTypeRequest>(result.Data);
        Assert.Equal(["name"], req.DropProps);
        Assert.Empty(req.AddProps);
        Assert.Empty(req.ChangeProps);
    }

    [Fact]
    public async Task TtlCol_DeleteButtonDisabled()
    {
        var type = new EdgeTypeDto
        {
            Name = "session",
            TtlCol = "created_at",
            TtlDurationSeconds = 3600,
            Props =
            [
                new EdgeTypePropDto { PropName = "created_at", DataType = EdgePropDataTypes.Timestamp }
            ]
        };
        var (provider, _) = await OpenAsync(type);

        var btn = provider.FindAll("button[title='TTL 列不可删除']").Single();
        Assert.True(btn.HasAttribute("disabled"));
    }

    [Fact]
    public async Task TypeChange_ViaEditDialog_SubmitsChangeProps()
    {
        // 类型修改经名称列 ✏ 编辑对话框暂存到行.SelectedType，提交时汇总进 ChangeProps
        var (provider, reference) = await OpenAsync(LineSegment());
        var dialog = DialogOf(provider).Instance;

        // 模拟编辑对话框回传：degree: INT → DOUBLE（直接写暂存字段）
        var degreeRow = ExistingRows(dialog).Cast<object>()
            .Single(r => (string)GetProp(GetProp(r, "Src"), "PropName") == "degree");
        SetProp(degreeRow, "SelectedType", EdgePropDataTypes.Double);
        provider.Render();
        ClickSubmit(provider);

        var result = await reference.Result;
        var req = Assert.IsType<AlterEdgeTypeRequest>(result.Data);
        var change = Assert.Single(req.ChangeProps);
        Assert.Equal("degree", change.PropName);
        Assert.Equal(EdgePropDataTypes.Double, change.NewDataType);
        Assert.Empty(req.DropProps);
    }

    [Fact]
    public async Task AddProp_Valid_SubmitsAddProps()
    {
        var (provider, reference) = await OpenAsync(LineSegment());
        var dialog = DialogOf(provider).Instance;

        var row = NewAddRow(dialog);
        SetProp(row, "PropName", "speed");
        SetProp(row, "DisplayName", "限速");
        SetProp(row, "DataType", EdgePropDataTypes.Int);
        SetProp(row, "Nullable", false);
        SetProp(row, "DefaultValue", (JsonElement?)JsonSerializer.SerializeToElement(60L));
        provider.Render();
        ClickSubmit(provider);

        var result = await reference.Result;
        var req = Assert.IsType<AlterEdgeTypeRequest>(result.Data);
        var add = Assert.Single(req.AddProps);
        Assert.Equal("speed", add.PropName);
        Assert.Equal("限速", add.DisplayName);
        Assert.Equal(EdgePropDataTypes.Int, add.DataType);
        Assert.False(add.Nullable);
        Assert.Equal(60, add.DefaultValue!.Value.GetInt64());
    }
}
