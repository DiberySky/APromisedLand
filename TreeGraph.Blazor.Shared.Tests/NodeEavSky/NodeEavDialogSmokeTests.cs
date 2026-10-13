using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor;
using MudBlazor.Services;
using TreeGraph.Blazor.Shared.Common;
using TreeGraph.Blazor.Shared.NodeEavSky.Components.Dialogs;
using TreeGraph.Blazor.Shared.NodeEavSky.Services;
using TreeGraph.Blazor.Shared.Platform;
using TreeGraph.Shared.NodeEavSky.Dtos;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.NodeEavSky;

/// <summary>
/// NodeEav 对话框组件冒烟测试。
///
/// 目的：验证由内联 MudDialog 抽取为独立 DialogService 组件的对话框（含行管理/
/// 行编辑两级弹窗）能经真实 MudDialogProvider + IDialogService 链路正常渲染
/// （不抛异常、DialogSky 外壳生效、标题正确）。提交逻辑涉及 API 调用，不在本冒烟范围。
/// </summary>
public class NodeEavDialogSmokeTests : BunitTestBase
{
    public NodeEavDialogSmokeTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IPlatformContext>(new FakePlatformContext());
        // 真实 EavApiClient + 桩 HttpClient（提交才会触发 API，渲染阶段不调用）
        Services.AddSingleton(new HttpClient(new StubHttpMessageHandler())
        {
            BaseAddress = new Uri("http://localhost/")
        });
        Services.AddSingleton<EavApiClient>();
        // UnitTreeField 注入 TreeNodeDialogService<UnitTree>（仅渲染，不弹窗）
        Services.AddScoped<TreeGraph.Blazor.Shared.TreeEavSky.Dialogs.BlazorService>();
        Services.AddScoped(
            typeof(TreeGraph.Blazor.Shared.TreeEavSky.Dialogs.TreeNodeDialogService<>));
    }

    private async Task<IRenderedComponent<MudDialogProvider>> OpenAsync<TDialog>(
        DialogParameters? parameters = null)
        where TDialog : ComponentBase
    {
        var provider = Render<MudDialogProvider>();
        var p = parameters ?? new DialogParameters();
        var ds = Services.GetRequiredService<IDialogService>();
        var _ = await ds.ShowAsync<TDialog>(string.Empty, p, new DialogOptions());
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

    private static OptionSetSummaryDto OptionSet() => new(
        OptionSetId: "os-1", EntityType: "Product", SetName: "colors",
        DisplayName: "颜色", IsDeleted: false);

    private static OptionItemDetailDto OptionItem() => new(
        OptionItemId: "oi-1", Value: "red", Label: "红", DisplayOrder: 1,
        IsDefault: false, IsDeleted: false, CreatedAt: DateTimeOffset.UtcNow);

    private static EntityTypeDetailDto EntityType() => new(
        EntityTypeId: "et-1", EntityType: "Product", DisplayName: "产品",
        Description: null, DisplayOrder: 1, IsDeleted: false,
        CreatedAt: DateTimeOffset.UtcNow, UpdatedAt: DateTimeOffset.UtcNow,
        AttributeCount: 0);

    private static CompositeTypeDetailDto CompositeType() => new(
        CompositeTypeId: "ct-1", EntityType: "Product", TypeName: "address",
        DisplayName: "地址", Version: 1,
        Fields: Array.Empty<CompositeFieldDetailDto>(), IsDeleted: false);

    private static CompositeFieldDetailDto CompositeField() => new(
        FieldId: "f-1", FieldName: "street", DisplayName: "街道",
        DataType: "string", RefCompositeTypeId: null, IsArray: false,
        IsRequired: false, IsSearchable: true, IsSortable: true,
        DisplayOrder: 1, DefaultValue: null, AllowedValues: null,
        ValidationRule: null, UnitId: null, UnitName: null, UnitAbbreviation: null,
        RefOptionSetId: null,
        OptionSetName: null, OptionSetDisplayName: null, IsDeleted: false);

    private static CustomTableDetailDto CustomTable() => new(
        TableDefinitionId: "t-1", EntityType: "Product", TableName: "specs",
        DisplayName: "规格", Version: 1, DisplayOrder: 1,
        Columns: Array.Empty<CustomTableColumnDto>(), IsDeleted: false);

    private static CustomTableColumnDto CustomColumn() => new(
        ColumnId: "c-1", ColumnName: "weight", DisplayName: "重量",
        DataType: "decimal", RefCompositeTypeId: null, IsRequired: false,
        IsSearchable: true, IsSortable: true, IsUnique: false,
        DisplayOrder: 1, DefaultValue: null, AllowedValues: null,
        ValidationRule: null, IsDeleted: false);

    private static AttributeDetailDto Attribute() => new(
        AttributeId: "a-1", EntityType: "Product", AttributeName: "color",
        DisplayName: "颜色", DataType: "string", IsRequired: false,
        IsSearchable: true, IsSortable: true, IsDeleted: false, Version: 1,
        DisplayOrder: 1, DefaultValue: null, CreatedAt: DateTimeOffset.UtcNow,
        UpdatedAt: DateTimeOffset.UtcNow, AllowedValues: null, ValidationRule: null,
        UnitId: null, UnitName: null, UnitAbbreviation: null,
        RefCompositeTypeId: null, CompositeTypeName: null,
        CompositeTypeDisplayName: null, RefTableDefinitionId: null,
        TableName: null, TableDisplayName: null, RefOptionSetId: null,
        OptionSetName: null, OptionSetDisplayName: null);

    // ─── OptionSets ───

    [Fact]
    public async Task OptionSetCreateDialog_Renders()
        => AssertDialog(await OpenAsync<OptionSetCreateDialog>(), "新建选项集");

    [Fact]
    public async Task OptionSetEditDialog_Renders()
        => AssertDialog(await OpenAsync<OptionSetEditDialog>(new DialogParameters<OptionSetEditDialog>
        {
            { x => x.Set, OptionSet() },
        }), "编辑选项集");

    [Fact]
    public async Task OptionSetItemDialog_Renders()
        => AssertDialog(await OpenAsync<OptionSetItemDialog>(new DialogParameters<OptionSetItemDialog>
        {
            { x => x.Set, OptionSet() },
            { x => x.EditingItem, OptionItem() },
            { x => x.InitialDisplayOrder, 1 },
        }), "编辑选项");

    // ─── EntityTypes ───

    [Fact]
    public async Task EntityTypeEditDialog_Renders()
        => AssertDialog(await OpenAsync<EntityTypeEditDialog>(new DialogParameters<EntityTypeEditDialog>
        {
            { x => x.Editing, EntityType() },
            { x => x.InitialDisplayOrder, 1 },
        }), "编辑实体类型");

    // ─── CompositeTypes ───

    [Fact]
    public async Task CompositeTypeCreateDialog_Renders()
    {
        var provider = await OpenAsync<CompositeTypeCreateDialog>();
        AssertDialog(provider, "新建组合类型");
        // 回归：全局共享勾选框必须渲染（曾误用不存在的 MudCheckbox 组件名被当成普通 HTML 元素）
        var html = provider.Find(".mud-dialog").InnerHtml;
        Assert.Contains("mud-checkbox", html);
        Assert.Contains("全局共享", html);
    }

    [Fact]
    public async Task CompositeTypeEditDialog_Renders()
        => AssertDialog(await OpenAsync<CompositeTypeEditDialog>(new DialogParameters<CompositeTypeEditDialog>
        {
            { x => x.Type, CompositeType() },
        }), "编辑组合类型");

    [Fact]
    public async Task CompositeTypeFieldDialog_Renders()
        => AssertDialog(await OpenAsync<CompositeTypeFieldDialog>(new DialogParameters<CompositeTypeFieldDialog>
        {
            { x => x.Type, CompositeType() },
            { x => x.EditingField, CompositeField() },
            { x => x.InitialDisplayOrder, 1 },
            { x => x.OptionSets, new[] { OptionSet() } },
            { x => x.NestedTypes, new[] { CompositeType() } },
        }), "编辑字段");

    // ─── CustomTables ───

    [Fact]
    public async Task CustomTableCreateDialog_Renders()
        => AssertDialog(await OpenAsync<CustomTableCreateDialog>(), "新建自定义表");

    [Fact]
    public async Task CustomTableEditDialog_Renders()
        => AssertDialog(await OpenAsync<CustomTableEditDialog>(new DialogParameters<CustomTableEditDialog>
        {
            { x => x.Table, CustomTable() },
        }), "编辑自定义表");

    [Fact]
    public async Task CustomTableColumnDialog_Renders()
        => AssertDialog(await OpenAsync<CustomTableColumnDialog>(new DialogParameters<CustomTableColumnDialog>
        {
            { x => x.Table, CustomTable() },
            { x => x.EditingColumn, CustomColumn() },
            { x => x.CompositeTypes, new[] { CompositeType() } },
        }), "编辑列");

    [Fact]
    public async Task TableRowManageDialog_Renders()
        => AssertDialog(await OpenAsync<TableRowManageDialog>(new DialogParameters<TableRowManageDialog>
        {
            { x => x.EntityType, "Product" },
            { x => x.EntityId, "p-1" },
            { x => x.TableName, "specs" },
            { x => x.DisplayName, "规格" },
            { x => x.RefTableDefinitionId, "t-1" },
        }), "规格");

    [Fact]
    public async Task TableRowEditDialog_Create_Renders()
        => AssertDialog(await OpenAsync<TableRowEditDialog>(new DialogParameters<TableRowEditDialog>
        {
            { x => x.EntityType, "Product" },
            { x => x.EntityId, "p-1" },
            { x => x.TableName, "specs" },
            { x => x.TableDef, CustomTableWithColumn() },
            { x => x.NextRowOrder, 0 },
        }), "新增行");

    [Fact]
    public async Task TableRowEditDialog_Edit_Renders()
        => AssertDialog(await OpenAsync<TableRowEditDialog>(new DialogParameters<TableRowEditDialog>
        {
            { x => x.EntityType, "Product" },
            { x => x.EntityId, "p-1" },
            { x => x.TableName, "specs" },
            { x => x.TableDef, CustomTableWithColumn() },
            { x => x.EditRow, new CustomTableRowValue
                {
                    RowId = "r-1", RowOrder = 0,
                    Fields = new Dictionary<string, object?> { ["weight"] = 12m }
                } },
        }), "编辑行");

    private static CustomTableDetailDto CustomTableWithColumn() => new(
        TableDefinitionId: "t-1", EntityType: "Product", TableName: "specs",
        DisplayName: "规格", Version: 1, DisplayOrder: 1,
        Columns: new[] { CustomColumn() }, IsDeleted: false);

    // ─── Attributes ───

    [Fact]
    public async Task AttributeCreateDialog_Renders()
        => AssertDialog(await OpenAsync<AttributeCreateDialog>(new DialogParameters<AttributeCreateDialog>
        {
            { x => x.EntityType, "Product" },
            { x => x.InitialDisplayOrder, 1 },
            { x => x.EntityTypes, new[] { new EntityTypeSummaryDto("Product", 0, 0, "产品", "et-1", "产品") } },
            { x => x.CompositeTypes, new[] { CompositeType() } },
            { x => x.CustomTables, new[] { CustomTable() } },
            { x => x.OptionSets, new[] { OptionSet() } },
        }), "新建属性");

    [Fact]
    public async Task AttributeEditDialog_Renders()
        => AssertDialog(await OpenAsync<AttributeEditDialog>(new DialogParameters<AttributeEditDialog>
        {
            { x => x.Attribute, Attribute() },
            { x => x.CompositeTypes, new[] { CompositeType() } },
            { x => x.CustomTables, new[] { CustomTable() } },
            { x => x.OptionSets, new[] { OptionSet() } },
        }), "编辑属性");

    // ─── LocationPick ───

    [Fact]
    public async Task LocationPickDialog_Renders()
        => AssertDialog(await OpenAsync<LocationPickDialog>(), "地图选点");

    [Fact]
    public async Task LocationPickDialog_WithCoords_Renders()
        => AssertDialog(await OpenAsync<LocationPickDialog>(new DialogParameters<LocationPickDialog>
        {
            { x => x.Latitude, 30.67 },
            { x => x.Longitude, 104.06 },
        }), "地图选点");

    /// <summary>返回 200 OK 的桩 HttpClient（渲染阶段不发请求，仅为满足 DI）。</summary>
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
    }
}
