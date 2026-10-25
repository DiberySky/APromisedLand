using System.Text;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor;
using MudBlazor.Services;
using TreeGraph.Blazor.Shared.Common;
using TreeGraph.Blazor.Shared.NodeEavSky.Components.Dialogs;
using TreeGraph.Blazor.Shared.NodeEavSky.Components.Search;
using TreeGraph.Blazor.Shared.NodeEavSky.Services;
using TreeGraph.Blazor.Shared.Platform;
using TreeGraph.Blazor.Shared.TreeEavSky;
using TreeGraph.Blazor.Shared.TreeEavSky.Contracts;
using TreeGraph.Blazor.Shared.TreeEavSky.Dialogs;
using TreeGraph.Blazor.Shared.TreeEavSky.Nodes;
using TreeGraph.Shared.NodeEavSky.Dtos;
using TreeGraph.Shared.NodeSky.Entities;
using TreeGraph.Shared.TreeEavSky.Entities;
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
        // EntityTypeEditDialog 注入 SpaceApiClient（渲染阶段不调用）
        Services.AddSingleton<TreeGraph.Blazor.Shared.SpaceSky.Services.SpaceApiClient>();
        // UnitTreeField 注入 TreeNodeDialogService<UnitTree>（仅渲染，不弹窗）
        Services.AddScoped<TreeGraph.Blazor.Shared.TreeEavSky.Dialogs.BlazorService>();
        Services.AddScoped(
            typeof(TreeGraph.Blazor.Shared.TreeEavSky.Dialogs.TreeNodeDialogService<>));
        // NodeAttributeItemsSky（TreeNodeActionsDialog 属性区）依赖 MessageService
        Services.AddScoped<TreeGraph.Blazor.Shared.TreeEavSky.Dialogs.MessageService>();
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
        TableName: null, TableDisplayName: null, IsRecordMode: false, RefOptionSetId: null,
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

    // ─── TreeNodeActionsDialog（搜索页树宿主直开的宿主操作对话框） ───

    [Fact]
    public async Task TreeNodeActionsDialog_TreeHost_Renders()
    {
        var parent = new UnitTree
        {
            Id = "u-root",
            CanHaveChildren = true,
            Node = new Node { Id = "u-root", HostType = "UnitTree", Name = "上级单位" }
        };
        var unit = new UnitTree
        {
            Id = "u-1",
            ParentId = parent.Id,
            CanHaveChildren = true,
            HasChildren = false,
            Description = "测试描述",
            Node = new Node { Id = "u-1", HostType = "UnitTree", Name = "测试单位" },
            Parent = parent
        };
        var nodeItem = new TreeNodeItem<UnitTree>
        {
            Text = "测试单位",
            Value = unit
        };

        var provider = await OpenAsync<TreeNodeActionsDialog<UnitTree>>(
            new DialogParameters<TreeNodeActionsDialog<UnitTree>>
            {
                { x => x.NodeTemplate, new NodeTemplate<UnitTree> { Node = nodeItem } },
                { x => x.ParentNode, parent },
                { x => x.IsBoot, false },
            });

        AssertDialog(provider, "节点操作");
        var html = provider.Find(".mud-dialog").InnerHtml;
        Assert.Contains("测试单位", html);
        Assert.Contains("上级单位", html);
        // 全量按钮集：非根 + 允许子节点 + 无子节点 → 六个操作全部可见
        Assert.Contains("移动", html);
        Assert.Contains("子项排序", html);
        Assert.Contains("设置修改", html);
        Assert.Contains("属性", html);
        Assert.Contains("创建子项", html);
        Assert.Contains("删除", html);
    }

    // ─── NodePickerDialog（语义搜索节点选择器） ───

    [Fact]
    public async Task NodePickerDialog_Renders_WithConfirmDisabled()
    {
        var provider = await OpenAsync<NodePickerDialog>(
            new DialogParameters<NodePickerDialog>
            {
                { x => x.Title, "选择节点" },
                { x => x.TopK, 20 },
            });

        AssertDialog(provider, "选择节点");

        var html = provider.Find(".mud-dialog").InnerHtml;
        Assert.Contains("搜索内容", html);
        Assert.Contains("输入关键词搜索", html);
        Assert.Contains("取消", html);
        Assert.Contains("确认选择", html);

        // 未选中任何结果 → 确认按钮禁用
        var confirm = provider.FindAll("button")
            .First(b => b.TextContent.Contains("确认选择"));
        Assert.True(confirm.HasAttribute("disabled"));
    }

    [Fact]
    public async Task NodePickerDialog_SelectsRow_AndEnablesConfirm()
    {
        var provider = await OpenAsync<NodePickerDialog>(
            new DialogParameters<NodePickerDialog>
            {
                { x => x.Title, "选择节点" },
                { x => x.InitialQuery, "货币" },
            });

        // 初始搜索词触发自动搜索（OnAfterRenderAsync）→ 结果行出现
        provider.WaitForAssertion(
            () => Assert.Contains("货币", provider.Find(".mud-dialog").InnerHtml),
            TimeSpan.FromSeconds(5));

        // 点结果行 → 出现「已选」提示且确认按钮解禁
        provider.Find(".srl-clickable").Click();
        provider.WaitForAssertion(
            () => Assert.Contains("已选", provider.Find(".mud-dialog").InnerHtml),
            TimeSpan.FromSeconds(5));

        var confirm = provider.FindAll("button")
            .First(b => b.TextContent.Contains("确认选择"));
        Assert.False(confirm.HasAttribute("disabled"));
    }

    /// <summary>返回 200 OK 的桩 HttpClient（/api/inode 属性区回空列表信封，其余仅满足 DI）。</summary>
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? "";
            if (request.Method == HttpMethod.Get &&
                path.StartsWith("/api/inode", StringComparison.Ordinal))
            {
                var body = JsonSerializer.Serialize(
                    new { Success = true, Message = (string?)null, Data = Array.Empty<object>() },
                    WebJson);
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                });
            }

            // 语义搜索：返回一条结果，供 NodePickerDialog 选中流程验证
            if (path.StartsWith("/api/search/semantic", StringComparison.Ordinal))
            {
                var data = new
                {
                    Items = new[]
                    {
                        new
                        {
                            InodeId = "u-1", NodeName = "货币", HostType = "UnitTree",
                            MatchKind = "Node", EntityType = (string?)null,
                            Score = 0.92f, Path = "演示单位/货币", ParentName = "演示单位",
                            RootId = "u-root"
                        }
                    },
                    TotalCount = 1
                };
                var body = JsonSerializer.Serialize(
                    new { Success = true, Message = (string?)null, Data = data },
                    WebJson);
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
