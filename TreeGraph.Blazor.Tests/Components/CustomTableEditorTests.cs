using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using MudBlazor.Services;
using TreeGraph.Blazor.Components.Shared;
using TreeGraph.Blazor.Services;
using TreeGraph.Blazor.Tests.TestDoubles;
using TreeGraph.Shared.Eav.Dtos;
using Xunit;

namespace TreeGraph.Blazor.Tests.Components;

/// <summary>
/// CustomTableEditor 组件测试。
///
/// 焦点：
///   - 加载后标题 chip / 行数 / 列布局
///   - IsUnique 跨行重复 → 出现"存在错误"chip + 错误文案
///   - 加载失败时的 MudAlert
///   - RefTableDefinitionId 缺失 → Snackbar 提示
///
/// 不测：具体控件（MudTextField 等）的键盘交互（依赖 JS）。
/// </summary>
public class CustomTableEditorTests : TestContext
{
    public CustomTableEditorTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // ============================================================
    // 测试数据
    // ============================================================

    private static CustomTableDetailDto MakeTable(
        bool uniqueCertName = false,
        bool requiredCertName = false,
        JsonElement? certNameRule = null)
        => new(
            TableDefinitionId: "00000000-0000-0000-0000-000000000100",
            EntityType: "Product",
            TableName: "certs",
            DisplayName: "认证证书",
            Version: 1,
            DisplayOrder: 1,
            Columns: new[]
            {
                new CustomTableColumnDto(
                    ColumnId: "00000000-0000-0000-0000-000000000001",
                    ColumnName: "cert_name",
                    DisplayName: "证书名称",
                    DataType: "string",
                    RefCompositeTypeId: null,
                    IsRequired: requiredCertName,
                    IsSearchable: true,
                    IsSortable: false,
                    IsUnique: uniqueCertName,
                    DisplayOrder: 1,
                    DefaultValue: null,
                    AllowedValues: null,
                    ValidationRule: certNameRule),
                new CustomTableColumnDto(
                    ColumnId: "00000000-0000-0000-0000-000000000002",
                    ColumnName: "issuer",
                    DisplayName: "颁发机构",
                    DataType: "string",
                    RefCompositeTypeId: null,
                    IsRequired: false,
                    IsSearchable: true,
                    IsSortable: false,
                    IsUnique: false,
                    DisplayOrder: 2,
                    DefaultValue: null,
                    AllowedValues: null,
                    ValidationRule: null),
            });

    private static CustomTableValue MakeRows(params (string CertName, string Issuer)[] rows)
    {
        var result = new CustomTableValue { TableName = "certs" };
        for (int i = 0; i < rows.Length; i++)
        {
            result.Rows.Add(new CustomTableRowValue
            {
                RowId = $"00000000-0000-0000-0000-{(i + 1):D12}",
                RowOrder = i,
                Fields = new Dictionary<string, object?>
                {
                    ["cert_name"] = rows[i].CertName,
                    ["issuer"] = rows[i].Issuer
                }
            });
        }
        return result;
    }

    private IRenderedComponent<CustomTableEditor> RenderEditor(
        CustomTableDetailDto? table,
        CustomTableValue? value,
        string? refTableDefinitionId = "00000000-0000-0000-0000-000000000100")
    {
        var handler = new TestHttpMessageHandler();
        if (table is not null)
            handler.MapGet($"api/eav/metadata/custom-tables/{table.TableDefinitionId}", table);
        if (value is not null)
            handler.MapGet($"api/eav/Product/entities/1/tables/certs", value);

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var api = new EavApiClient(http, NullLogger<EavApiClient>.Instance);

        Services.AddSingleton(api);
        Services.AddSingleton<IEavFieldValidator, EavFieldValidator>();

        return RenderComponent<CustomTableEditor>(p => p
            .Add(x => x.EntityType, "Product")
            .Add(x => x.EntityId, "1")
            .Add(x => x.AttributeName, "certs")
            .Add(x => x.TableName, "certs")
            .Add(x => x.DisplayName, "认证证书")
            .Add(x => x.RefTableDefinitionId, refTableDefinitionId));
    }

    // ============================================================
    // 基础渲染
    // ============================================================

    [Fact]
    public async Task LoadSuccess_RendersRowsCount()
    {
        var cut = RenderEditor(
            MakeTable(),
            MakeRows(("CE", "TUV"), ("FCC", "SGS")));

        // 等待 OnAfterRenderAsync → LoadAsync
        cut.WaitForState(() => cut.Markup.Contains("2 行"), TimeSpan.FromSeconds(3));

        Assert.Contains("2 行", cut.Markup);
        Assert.Contains("认证证书", cut.Markup);
    }

    [Fact]
    public async Task LoadSuccess_RendersColumnLabels()
    {
        var cut = RenderEditor(MakeTable(), MakeRows(("CE", "TUV")));

        cut.WaitForState(() => cut.Markup.Contains("证书名称"), TimeSpan.FromSeconds(3));

        Assert.Contains("证书名称", cut.Markup);
        Assert.Contains("颁发机构", cut.Markup);
    }

    // ============================================================
    // 已删除列：数据编辑器不显示（与管理页 CustomTables.razor 的语义分工）
    // ============================================================

    [Fact]
    public async Task LoadTable_ExcludesDeletedColumns()
    {
        // 后端 ToCustomTableDto 不过滤 IsDeleted（恢复 UI 需要），
        // 数据编辑器必须自行过滤：3 列中 legacy 已删除。
        var table = new CustomTableDetailDto(
            TableDefinitionId: "00000000-0000-0000-0000-000000000100",
            EntityType: "Product",
            TableName: "certs",
            DisplayName: "认证",
            Version: 1,
            DisplayOrder: 1,
            Columns: new[]
            {
                new CustomTableColumnDto("00000000-0000-0000-0000-000000000001", "cert_name", "证书", "string",
                    null, true, true, false, false, 1, null, null, null,
                    IsDeleted: false),
                new CustomTableColumnDto("00000000-0000-0000-0000-000000000002", "issuer", "机构", "string",
                    null, false, true, false, false, 2, null, null, null,
                    IsDeleted: false),
                new CustomTableColumnDto("00000000-0000-0000-0000-000000000003", "legacy", "旧列", "string",
                    null, false, false, false, false, 3, null, null, null,
                    IsDeleted: true),
            });

        // 行数据里同时含活动列与已删除列的键（历史数据）
        var value = new CustomTableValue { TableName = "certs" };
        value.Rows.Add(new CustomTableRowValue
        {
            RowId = "00000000-0000-0000-0000-000000000001",
            RowOrder = 0,
            Fields = new Dictionary<string, object?>
            {
                ["cert_name"] = "CE",
                ["issuer"] = "TUV",
                ["legacy"] = "old-data"
            }
        });

        var cut = RenderEditor(table, value);
        cut.WaitForState(() => cut.Markup.Contains("证书"), TimeSpan.FromSeconds(3));

        Assert.Contains("证书", cut.Markup);
        Assert.Contains("机构", cut.Markup);
        Assert.DoesNotContain("旧列", cut.Markup);
        // 已删除列的值也不得出现在任何控件中（防止随保存回传）
        Assert.DoesNotContain("old-data", cut.Markup);
    }

    // ============================================================
    // IsUnique 跨行重复 → 错误 chip
    // ============================================================

    [Fact]
    public async Task UniqueViolation_ShowsErrorChip()
    {
        var cut = RenderEditor(
            MakeTable(uniqueCertName: true),
            MakeRows(("CE", "TUV"), ("CE", "SGS")));   // 两行 cert_name 相同

        cut.WaitForState(
            () => cut.Markup.Contains("存在错误"),
            TimeSpan.FromSeconds(3));

        Assert.Contains("存在错误", cut.Markup);
        Assert.Contains("在列中重复", cut.Markup);
    }

    [Fact]
    public async Task UniqueOk_NoErrorChip()
    {
        var cut = RenderEditor(
            MakeTable(uniqueCertName: true),
            MakeRows(("CE", "TUV"), ("FCC", "SGS")));

        cut.WaitForState(() => cut.Markup.Contains("2 行"), TimeSpan.FromSeconds(3));

        Assert.DoesNotContain("存在错误", cut.Markup);
        Assert.DoesNotContain("在列中重复", cut.Markup);
    }

    // ============================================================
    // 必填列 + 空值 → 存在错误
    // ============================================================

    [Fact]
    public async Task RequiredColumnWithEmptyValue_ShowsError()
    {
        var cut = RenderEditor(
            MakeTable(requiredCertName: true),
            MakeRows(("", "TUV")));   // cert_name 空

        cut.WaitForState(
            () => cut.Markup.Contains("存在错误"),
            TimeSpan.FromSeconds(3));

        Assert.Contains("必填字段", cut.Markup);
    }

    // ============================================================
    // 列规则：maxLength 越界
    // ============================================================

    [Fact]
    public async Task MaxLengthViolation_ShowsError()
    {
        var rule = JsonDocument.Parse("""{"maxLength": 2}""").RootElement;
        var cut = RenderEditor(
            MakeTable(certNameRule: rule),
            MakeRows(("LONG_NAME", "TUV")));

        cut.WaitForState(
            () => cut.Markup.Contains("存在错误"),
            TimeSpan.FromSeconds(3));

        Assert.Contains("长度不能超过 2", cut.Markup);
    }

    // ============================================================
    // RefTableDefinitionId 缺失
    // ============================================================

    [Fact]
    public async Task MissingRefTableDefinitionId_ShowsWarningAndNoRows()
    {
        var cut = RenderEditor(
            table: MakeTable(),
            value: MakeRows(("CE", "TUV")),
            refTableDefinitionId: null);

        cut.WaitForState(
            () => cut.Markup.Contains("无法加载表结构"),
            TimeSpan.FromSeconds(3));

        Assert.Contains("无法加载表结构", cut.Markup);
    }

    // ============================================================
    // 表结构加载失败（404）→ MudAlert
    // ============================================================

    [Fact]
    public async Task TableDefLoad404_ShowsAlert()
    {
        // 只注册数据 URL，不注册表结构 URL → 表结构 GET 会 404
        var handler = new TestHttpMessageHandler();
        handler.MapGet("api/eav/Product/entities/1/tables/certs", MakeRows(("CE", "TUV")));

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var api = new EavApiClient(http, NullLogger<EavApiClient>.Instance);

        Services.AddSingleton(api);
        Services.AddSingleton<IEavFieldValidator, EavFieldValidator>();

        var cut = RenderComponent<CustomTableEditor>(p => p
            .Add(x => x.EntityType, "Product")
            .Add(x => x.EntityId, "1")
            .Add(x => x.AttributeName, "certs")
            .Add(x => x.TableName, "certs")
            .Add(x => x.DisplayName, "认证证书")
            .Add(x => x.RefTableDefinitionId, "00000000-0000-0000-0000-000000000100"));

        cut.WaitForState(
            () => cut.Markup.Contains("无法加载表结构"),
            TimeSpan.FromSeconds(3));

        Assert.Contains("无法加载表结构", cut.Markup);
    }

    // ============================================================
    // 空表结构（无列）→ Info alert
    // ============================================================

    [Fact]
    public async Task TableWithNoColumns_ShowsInfo()
    {
        var emptyTable = new CustomTableDetailDto(
            TableDefinitionId: "00000000-0000-0000-0000-000000000100",
            EntityType: "Product",
            TableName: "certs",
            DisplayName: "空表",
            Version: 1,
            DisplayOrder: 1,
            Columns: Array.Empty<CustomTableColumnDto>());

        var cut = RenderEditor(emptyTable, MakeRows());

        cut.WaitForState(
            () => cut.Markup.Contains("没有定义任何列"),
            TimeSpan.FromSeconds(3));

        Assert.Contains("没有定义任何列", cut.Markup);
    }
}
