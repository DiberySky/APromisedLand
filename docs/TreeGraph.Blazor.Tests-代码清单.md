# TreeGraph.Blazor.Tests C# 代码清单

- 生成时间：2026-10-04 21:37:24
- 文件总数：6
- 排除：bin/、obj/、csproj、README.md
- 项目状态：宿主 Blazor 测试 81 项

## 文件 1/6 TreeGraph.Blazor.Tests/Components/CustomTableEditorTests.cs

```csharp
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
```

## 文件 2/6 TreeGraph.Blazor.Tests/Components/QueryFilterBuilderTests.cs

```csharp
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using TreeGraph.Blazor.Components.Shared;
using TreeGraph.Blazor.Services;
using TreeGraph.Shared.Eav.Dtos;
using Xunit;

namespace TreeGraph.Blazor.Tests.Components;

/// <summary>
/// QueryFilterBuilder 组件测试。
///
/// 焦点：AddFilter / RemoveFilter / ClearAll 是否触发正确的状态与回调。
/// 不测 MudSelect 的下拉交互（依赖 JS，bUnit 不覆盖）。
/// </summary>
public class QueryFilterBuilderTests : TestContext
{
    public QueryFilterBuilderTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static AttributeSchemaDto MakeAttr(
        string name, string dataType = "string", bool searchable = true)
        => new(
            AttributeName: name,
            DisplayName: name,
            DataType: dataType,
            IsRequired: false,
            IsSearchable: searchable,
            IsSortable: false,
            DisplayOrder: 0,
            AllowedValues: null,
            ValidationRule: null,
            CompositeType: null);

    // ============================================================
    // 空状态
    // ============================================================

    [Fact]
    public void NoSupportedAttributes_RendersInfoAlert()
    {
        var cut = RenderComponent<QueryFilterBuilder>(p => p
            .Add(x => x.Filters, new List<AttributeFilter>())
            .Add(x => x.SupportedAttributes, Array.Empty<AttributeSchemaDto>()));

        Assert.Contains("没有可搜索的属性", cut.Markup);
    }

    [Fact]
    public void EmptyFilters_DoesNotRenderApplyButton()
    {
        var cut = RenderComponent<QueryFilterBuilder>(p => p
            .Add(x => x.Filters, new List<AttributeFilter>())
            .Add(x => x.SupportedAttributes, new[]
            {
                MakeAttr("name")
            }));

        // 无 filters 时，只有「添加条件」，没有「应用查询 / 清空条件」
        Assert.Contains("添加条件", cut.Markup);
        Assert.DoesNotContain("应用查询", cut.Markup);
        Assert.DoesNotContain("清空条件", cut.Markup);
    }

    // ============================================================
    // AddFilter
    // ============================================================

    [Fact]
    public void AddFilter_AddsToFiltersCollection()
    {
        var filters = new List<AttributeFilter>();
        var cut = RenderComponent<QueryFilterBuilder>(p => p
            .Add(x => x.Filters, filters)
            .Add(x => x.SupportedAttributes, new[] { MakeAttr("name") }));

        // 找「添加条件」按钮并点击
        var addBtn = cut.FindAll("button")
            .First(b => b.TextContent.Contains("添加条件"));
        addBtn.Click();

        Assert.Single(filters);
        // 默认属性名与默认运算符由 catalog 决定
        Assert.Equal("name", filters[0].AttributeName);
        Assert.False(string.IsNullOrEmpty(filters[0].Operator));
    }

    [Fact]
    public void AddFilter_WhenFiltersNonEmpty_RendersApplyAndClearButtons()
    {
        var filters = new List<AttributeFilter>
        {
            new() { AttributeName = "name", Operator = "eq" }
        };

        var cut = RenderComponent<QueryFilterBuilder>(p => p
            .Add(x => x.Filters, filters)
            .Add(x => x.SupportedAttributes, new[] { MakeAttr("name") }));

        Assert.Contains("应用查询", cut.Markup);
        Assert.Contains("清空条件", cut.Markup);
    }

    // ============================================================
    // RemoveFilter
    // ============================================================

    [Fact]
    public async Task RemoveFilter_RemovesFromCollectionAndInvokesOnApply()
    {
        var filters = new List<AttributeFilter>
        {
            new() { AttributeName = "name", Operator = "eq" },
        };
        var appliedCount = 0;

        var cut = RenderComponent<QueryFilterBuilder>(p => p
            .Add(x => x.Filters, filters)
            .Add(x => x.SupportedAttributes, new[] { MakeAttr("name") })
            .Add(x => x.OnApply, EventCallback.Factory.Create(this, () => appliedCount++)));

        // ★ 修复：用 title 属性定位，不依赖 SVG 内部字符串
        var removeBtn = cut.FindAll("button")
            .First(b => b.GetAttribute("title") == "移除"
                     || b.GetAttribute("aria-label") == "移除");
        await cut.InvokeAsync(() => removeBtn.Click());

        Assert.Empty(filters);
        Assert.Equal(1, appliedCount);
    }

    // ============================================================
    // ClearAll
    // ============================================================

    [Fact]
    public async Task ClearAll_RemovesAllAndInvokesOnApply()
    {
        var filters = new List<AttributeFilter>
        {
            new() { AttributeName = "name", Operator = "eq" },
            new() { AttributeName = "name", Operator = "neq" }
        };
        var appliedCount = 0;

        var cut = RenderComponent<QueryFilterBuilder>(p => p
            .Add(x => x.Filters, filters)
            .Add(x => x.SupportedAttributes, new[] { MakeAttr("name") })
            .Add(x => x.OnApply, EventCallback.Factory.Create(this, () => appliedCount++)));

        var clearBtn = cut.FindAll("button")
            .First(b => b.TextContent.Contains("清空条件"));
        await cut.InvokeAsync(() => clearBtn.Click());

        Assert.Empty(filters);
        Assert.Equal(1, appliedCount);
    }

    // ============================================================
    // AddFilter 默认运算符来自 FilterOperatorCatalog
    // ============================================================

    [Fact]
    public void AddFilter_ForNumericAttribute_PicksNumericOperator()
    {
        var filters = new List<AttributeFilter>();
        var cut = RenderComponent<QueryFilterBuilder>(p => p
            .Add(x => x.Filters, filters)
            .Add(x => x.SupportedAttributes, new[]
            {
                MakeAttr("price", dataType: "decimal")
            }));

        cut.FindAll("button").First(b => b.TextContent.Contains("添加条件")).Click();

        Assert.Single(filters);
        Assert.Equal("price", filters[0].AttributeName);
        // 数值属性首个运算符是 eq（catalog 里定义的第一项）
        Assert.Equal("eq", filters[0].Operator);
    }

    [Fact]
    public void AddFilter_ForBoolAttribute_PicksBoolOperator()
    {
        var filters = new List<AttributeFilter>();
        var cut = RenderComponent<QueryFilterBuilder>(p => p
            .Add(x => x.Filters, filters)
            .Add(x => x.SupportedAttributes, new[]
            {
                MakeAttr("active", dataType: "bool")
            }));

        cut.FindAll("button").First(b => b.TextContent.Contains("添加条件")).Click();

        Assert.Single(filters);
        Assert.Equal("eq", filters[0].Operator);
    }
}
```

## 文件 3/6 TreeGraph.Blazor.Tests/EavFieldValidatorTests.cs

```csharp
using System.Text.Json;
using TreeGraph.Blazor.Services;
using TreeGraph.Shared.Eav.Dtos;
using Xunit;

namespace TreeGraph.Blazor.Tests;

/// <summary>
/// EavFieldValidator 的完整校验链测试。
///
/// 焦点是 **EavFieldValidator 自身逻辑**（FieldValidationRules 已在
/// FieldValidationRulesTests 覆盖）：
///   - 必填检查 + single_choice 默认值放行
///   - int 拒绝小数（EavFieldValidator 独有）
///   - NumericInput 的 UnitId 约束（attr.Unit 为 null 时不允许指定）
///   - 组合类型递归
///   - date / time / json 的解析失败
///   - IsEmpty 对空数组的处理
/// </summary>
public class EavFieldValidatorTests
{
    private readonly EavFieldValidator _validator = new();

    // ============================================================
    // 构造助手
    // ============================================================

    private static AttributeSchemaDto Attr(
        string dataType,
        bool isRequired = false,
        JsonElement? rule = null,
        JsonElement? allowedValues = null,
        CompositeTypeSchemaDto? composite = null,
        UnitSchemaDto? unit = null,
        IReadOnlyList<UnitSchemaDto>? availableUnits = null,
        OptionSetSchemaDto? optionSet = null)
        => new(
            AttributeName: "field",
            DisplayName: "字段",
            DataType: dataType,
            IsRequired: isRequired,
            IsSearchable: true,
            IsSortable: false,
            DisplayOrder: 0,
            AllowedValues: allowedValues,
            ValidationRule: rule,
            CompositeType: composite,
            Unit: unit,
            AvailableUnits: availableUnits,
            OptionSet: optionSet);

    private static CompositeTypeSchemaDto Composite(
        params CompositeFieldSchemaDto[] fields)
        => new("TestType", fields);

    private static CompositeFieldSchemaDto Field(
        string name,
        string dataType,
        bool isRequired = false,
        bool isArray = false,
        JsonElement? rule = null,
        CompositeTypeSchemaDto? nested = null,
        OptionSetSchemaDto? optionSet = null)
        => new(
            FieldName: name,
            DisplayName: name,
            DataType: dataType,
            IsArray: isArray,
            IsRequired: isRequired,
            IsSearchable: true,
            DisplayOrder: 0,
            ValidationRule: rule,
            NestedType: nested,
            OptionSet: optionSet);

    private static UnitSchemaDto Unit(string symbol = "kg")
        => new(Guid.NewGuid(), "weight", symbol, symbol, IsBaseUnit: true);

    private static OptionSetSchemaDto OptionSet(
        params (string Value, bool IsDefault)[] items)
        => new(
            OptionSetId: "00000000-0000-0000-0000-000000000001",
            SetName: "test",
            DisplayName: "test",
            Items: items.Select((x, i) => new OptionItemSchemaDto(
                $"00000000-0000-0000-0000-{(i + 1):D12}", x.Value, x.Value, i, x.IsDefault)).ToList());

    private static JsonElement Json(string s) => JsonDocument.Parse(s).RootElement;

    // ============================================================
    // int 拒绝小数（EavFieldValidator 独有）
    // ============================================================

    [Fact]
    public void Int_WithDecimalValue_ReturnsError()
    {
        var errors = _validator.Validate(Attr("int"), 3.14m);
        Assert.Single(errors);
        Assert.Contains("int 类型（无单位）不接受小数", errors[0].Message);
    }

    [Fact]
    public void Int_WithLongValue_Passes()
    {
        var errors = _validator.Validate(Attr("int"), 42L);
        Assert.Empty(errors);
    }

    [Fact]
    public void Int_WithStringDecimalRepresentation_ReturnsError()
    {
        var errors = _validator.Validate(Attr("int"), "3.14");
        Assert.Single(errors);
        Assert.Contains("int 类型（无单位）不接受小数", errors[0].Message);
    }

    // ============================================================
    // 必填
    // ============================================================

    [Fact]
    public void Required_NullValue_ReturnsError()
    {
        var errors = _validator.Validate(Attr("string", isRequired: true), null);
        Assert.Single(errors);
        Assert.Contains("必填字段", errors[0].Message);
    }

    [Fact]
    public void Required_EmptyString_ReturnsError()
    {
        var errors = _validator.Validate(Attr("string", isRequired: true), "   ");
        Assert.Single(errors);
        Assert.Contains("必填字段", errors[0].Message);
    }

    [Fact]
    public void Required_EmptyArray_ReturnsError()
    {
        var errors = _validator.Validate(
            Attr("string", isRequired: true),
            new List<object?>());
        Assert.Single(errors);
        Assert.Contains("必填字段", errors[0].Message);
    }

    // ============================================================
    // single_choice 默认值放行
    // ============================================================

    [Fact]
    public void RequiredSingleChoice_WithDefaultOption_NullValue_Passes()
    {
        var optionSet = OptionSet(("a", true), ("b", false));
        var errors = _validator.Validate(
            Attr("single_choice", isRequired: true, optionSet: optionSet),
            null);
        Assert.Empty(errors);
    }

    [Fact]
    public void RequiredSingleChoice_NoDefaultOption_NullValue_ReturnsError()
    {
        var optionSet = OptionSet(("a", false), ("b", false));
        var errors = _validator.Validate(
            Attr("single_choice", isRequired: true, optionSet: optionSet),
            null);
        Assert.Single(errors);
        Assert.Contains("必填字段", errors[0].Message);
    }

    [Fact]
    public void SingleChoice_InvalidValue_ReturnsError()
    {
        var optionSet = OptionSet(("a", false), ("b", false));
        var errors = _validator.Validate(
            Attr("single_choice", optionSet: optionSet),
            "c");
        Assert.Single(errors);
        Assert.Contains("不在选项集中", errors[0].Message);
    }

    // ============================================================
    // NumericInput 的 UnitId 约束（P0-5）
    // ============================================================

    [Fact]
    public void NumericInput_WhenAttrHasNoUnit_WithUnitId_ReturnsError()
    {
        var value = new NumericInput { Value = 5m, UnitId = Guid.NewGuid() };
        var errors = _validator.Validate(Attr("decimal"), value);
        Assert.Single(errors);
        Assert.Contains("未绑定基准单位", errors[0].Message);
    }

    [Fact]
    public void NumericInput_WhenAttrHasUnit_WithForeignUnitId_ReturnsError()
    {
        var baseUnit = Unit("kg");
        var foreignUnit = Unit("cm");
        var value = new NumericInput { Value = 5m, UnitId = foreignUnit.Id };

        var errors = _validator.Validate(
            Attr("decimal",
                unit: baseUnit,
                availableUnits: new[] { baseUnit }),
            value);

        Assert.Single(errors);
        Assert.Contains("不属于该属性的可用单位", errors[0].Message);
    }

    [Fact]
    public void NumericInput_WhenAttrHasUnit_WithMatchingUnitId_Passes()
    {
        var baseUnit = Unit("kg");
        var value = new NumericInput { Value = 5m, UnitId = baseUnit.Id };

        var errors = _validator.Validate(
            Attr("decimal",
                unit: baseUnit,
                availableUnits: new[] { baseUnit }),
            value);

        Assert.Empty(errors);
    }

    [Fact]
    public void NumericInput_BareDecimal_NoUnit_Passes()
    {
        var errors = _validator.Validate(Attr("decimal"), 5m);
        Assert.Empty(errors);
    }

    // ============================================================
    // 组合递归
    // ============================================================

    [Fact]
    public void Composite_ValueNotDictionary_ReturnsError()
    {
        var composite = Composite(
            Field("name", "string"));

        var errors = _validator.Validate(
            Attr("composite", composite: composite),
            "not-a-dictionary");

        Assert.Single(errors);
        Assert.Contains("期望字典结构", errors[0].Message);
    }

    [Fact]
    public void Composite_RequiredNestedField_Missing_ReturnsError()
    {
        var composite = Composite(
            Field("name", "string", isRequired: true));

        // ★ 修复：非空字典，仅缺目标字段
        // （空字典会被 IsEmpty 判定为"整个 composite 未提供"，走顶层必填路径）
        var dict = new Dictionary<string, object?> { ["unrelated"] = "x" };

        var errors = _validator.Validate(
            Attr("composite", composite: composite),
            dict);

        Assert.Single(errors);
        Assert.Contains("name", errors[0].Path);
        Assert.Contains("必填字段", errors[0].Message);
    }

    [Fact]
    public void Composite_NestedSingleChoiceWithDefault_Missing_Passes()
    {
        var optionSet = OptionSet(("x", true));
        var composite = Composite(
            Field("choice", "single_choice",
                isRequired: true, optionSet: optionSet));

        // ★ 修复：非空字典，缺 choice 字段；因选项集有默认值 → 放行
        var dict = new Dictionary<string, object?> { ["unrelated"] = "x" };

        var errors = _validator.Validate(
            Attr("composite", composite: composite),
            dict);

        Assert.Empty(errors);
    }

    [Fact]
    public void Composite_ArrayFieldWithRequiredElements_Missing_ReturnsError()
    {
        var composite = Composite(
            Field("items", "string", isRequired: true, isArray: true));

        // ★ 修复：非空字典，缺 items 字段
        var dict = new Dictionary<string, object?> { ["unrelated"] = "x" };

        var errors = _validator.Validate(
            Attr("composite", composite: composite),
            dict);

        Assert.Single(errors);
        Assert.Contains("items", errors[0].Path);
    }

    [Fact]
    public void Composite_ArrayFieldWithElements_ValidatesEach()
    {
        var composite = Composite(
            Field("items", "string", isArray: true));

        var dict = new Dictionary<string, object?>
        {
            ["items"] = new List<object?> { "a", "b", "c" }
        };

        var errors = _validator.Validate(
            Attr("composite", composite: composite),
            dict);

        Assert.Empty(errors);
    }

    // ============================================================
    // date / time / json 解析
    // ============================================================

    [Fact]
    public void Date_InvalidString_ReturnsError()
    {
        var errors = _validator.Validate(Attr("date"), "not-a-date");
        Assert.Single(errors);
        Assert.Contains("日期格式错误", errors[0].Message);
    }

    [Fact]
    public void Date_ValidString_Passes()
    {
        var errors = _validator.Validate(Attr("date"), "2026-10-02");
        Assert.Empty(errors);
    }

    [Fact]
    public void Time_InvalidString_ReturnsError()
    {
        var errors = _validator.Validate(Attr("time"), "not-a-time");
        Assert.Single(errors);
        Assert.Contains("时间格式错误", errors[0].Message);
    }

    [Fact]
    public void Json_InvalidJsonString_ReturnsError()
    {
        var errors = _validator.Validate(Attr("json"), "{ not valid json");
        Assert.Single(errors);
        Assert.Contains("JSON 格式错误", errors[0].Message);
    }

    [Fact]
    public void Json_ValidJsonString_Passes()
    {
        var errors = _validator.Validate(Attr("json"), """{"a": 1}""");
        Assert.Empty(errors);
    }

    // ============================================================
    // 顶层规则（走 FieldValidationRules）
    // ============================================================

    [Fact]
    public void Numeric_BelowMin_ReturnsError()
    {
        var rule = Json("""{"min": 5}""");
        var errors = _validator.Validate(Attr("decimal", rule: rule), 3m);
        Assert.Single(errors);
        Assert.Contains("不能小于 5", errors[0].Message);
    }

    [Fact]
    public void String_RegexMismatch_ReturnsError()
    {
        var rule = Json("""{"regex": "^\\d{4}$", "message": "必须是4位数字"}""");
        var errors = _validator.Validate(Attr("string", rule: rule), "12");
        Assert.Single(errors);
        Assert.Equal("必须是4位数字", errors[0].Message);
    }

    // ============================================================
    // ValidateJsonText（独立入口）
    // ============================================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateJsonText_NullOrWhitespace_ReturnsNull(string? text)
    {
        Assert.Null(_validator.ValidateJsonText(text));
    }

    [Fact]
    public void ValidateJsonText_ValidJson_ReturnsNull()
    {
        Assert.Null(_validator.ValidateJsonText("""{"a": 1}"""));
    }

    [Fact]
    public void ValidateJsonText_InvalidJson_ReturnsMessage()
    {
        var error = _validator.ValidateJsonText("{not-json");
        Assert.NotNull(error);
        Assert.Contains("JSON 格式错误", error);
    }
}
```

## 文件 4/6 TreeGraph.Blazor.Tests/FieldValidationRulesTests.cs

```csharp
using System.Text.Json;
using TreeGraph.Blazor.Services;
using Xunit;

namespace TreeGraph.Blazor.Tests;

/// <summary>
/// FieldValidationRules 纯静态规则引擎的单测。
///
/// 覆盖：
///   - ValidateNumeric：min / max / 两者 / 空 rule / 非对象 rule
///   - ValidateString：AllowedValues / minLength / maxLength / regex（默认+自定义 message）
///   - ValidateDate：minDate / maxDate
///   - ValidateTime：minTime / maxTime
///   - TryGetDecimal：number / string / 非法输入
///
/// 关键约束：非法正则必须被吞掉（不抛异常），与 EavFieldValidator 的容错语义一致。
/// </summary>
public class FieldValidationRulesTests
{
    // ============================================================
    // ValidateNumeric
    // ============================================================

    [Fact]
    public void ValidateNumeric_NullRule_ReturnsEmpty()
    {
        var errors = FieldValidationRules.ValidateNumeric(5m, null);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateNumeric_NonObjectRule_ReturnsEmpty()
    {
        var rule = JsonDocument.Parse("\"not an object\"").RootElement;
        var errors = FieldValidationRules.ValidateNumeric(5m, rule);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateNumeric_BelowMin_ReturnsError()
    {
        var rule = JsonDocument.Parse("""{"min": 5}""").RootElement;
        var errors = FieldValidationRules.ValidateNumeric(3m, rule);
        Assert.Single(errors);
        Assert.Contains("不能小于 5", errors[0]);
    }

    [Fact]
    public void ValidateNumeric_AboveMax_ReturnsError()
    {
        var rule = JsonDocument.Parse("""{"max": 10}""").RootElement;
        var errors = FieldValidationRules.ValidateNumeric(15m, rule);
        Assert.Single(errors);
        Assert.Contains("不能大于 10", errors[0]);
    }

    [Fact]
    public void ValidateNumeric_InRange_ReturnsEmpty()
    {
        var rule = JsonDocument.Parse("""{"min": 0, "max": 100}""").RootElement;
        var errors = FieldValidationRules.ValidateNumeric(50m, rule);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateNumeric_AtBoundary_ReturnsEmpty()
    {
        var rule = JsonDocument.Parse("""{"min": 5, "max": 10}""").RootElement;
        Assert.Empty(FieldValidationRules.ValidateNumeric(5m, rule));
        Assert.Empty(FieldValidationRules.ValidateNumeric(10m, rule));
    }

    [Fact]
    public void ValidateNumeric_InvertedRange_ValueInBetween_ReturnsTwoErrors()
    {
        // min=10, max=5（元数据配置错误）；value=7 同时越界两侧
        var rule = JsonDocument.Parse("""{"min": 10, "max": 5}""").RootElement;
        var errors = FieldValidationRules.ValidateNumeric(7m, rule);
        Assert.Equal(2, errors.Count);
    }

    // ============================================================
    // ValidateString — AllowedValues
    // ============================================================

    [Fact]
    public void ValidateString_NotInAllowed_ReturnsError()
    {
        var allowed = JsonDocument.Parse("""["a","b","c"]""").RootElement;
        var errors = FieldValidationRules.ValidateString("x", null, allowed);
        Assert.Single(errors);
        Assert.Contains("值必须是以下之一", errors[0]);
    }

    [Fact]
    public void ValidateString_InAllowed_ReturnsEmpty()
    {
        var allowed = JsonDocument.Parse("""["a","b"]""").RootElement;
        Assert.Empty(FieldValidationRules.ValidateString("a", null, allowed));
    }

    [Fact]
    public void ValidateString_EmptyAllowedArray_NoConstraint()
    {
        var allowed = JsonDocument.Parse("[]").RootElement;
        Assert.Empty(FieldValidationRules.ValidateString("anything", null, allowed));
    }

    // ============================================================
    // ValidateString — minLength / maxLength
    // ============================================================

    [Fact]
    public void ValidateString_TooShort_ReturnsError()
    {
        var rule = JsonDocument.Parse("""{"minLength": 5}""").RootElement;
        var errors = FieldValidationRules.ValidateString("abc", rule, null);
        Assert.Single(errors);
        Assert.Contains("长度不能少于 5", errors[0]);
    }

    [Fact]
    public void ValidateString_TooLong_ReturnsError()
    {
        var rule = JsonDocument.Parse("""{"maxLength": 3}""").RootElement;
        var errors = FieldValidationRules.ValidateString("abcd", rule, null);
        Assert.Single(errors);
        Assert.Contains("长度不能超过 3", errors[0]);
    }

    [Fact]
    public void ValidateString_ExactLength_ReturnsEmpty()
    {
        var rule = JsonDocument.Parse("""{"minLength": 3, "maxLength": 3}""").RootElement;
        Assert.Empty(FieldValidationRules.ValidateString("abc", rule, null));
    }

    // ============================================================
    // ValidateString — regex
    // ============================================================

    [Fact]
    public void ValidateString_RegexFails_ReturnsDefaultMessage()
    {
        var rule = JsonDocument.Parse("""{"regex": "^\\d{4}$"}""").RootElement;
        var errors = FieldValidationRules.ValidateString("12", rule, null);
        Assert.Single(errors);
        Assert.Contains("格式不正确", errors[0]);
    }

    [Fact]
    public void ValidateString_RegexFails_ReturnsCustomMessage()
    {
        var rule = JsonDocument.Parse(
            """{"regex": "^\\d{4}$", "message": "必须是4位数字"}""").RootElement;
        var errors = FieldValidationRules.ValidateString("12", rule, null);
        Assert.Single(errors);
        Assert.Equal("必须是4位数字", errors[0]);
    }

    [Fact]
    public void ValidateString_RegexPasses_ReturnsEmpty()
    {
        var rule = JsonDocument.Parse("""{"regex": "^\\d{4}$"}""").RootElement;
        Assert.Empty(FieldValidationRules.ValidateString("2026", rule, null));
    }

    [Fact]
    public void ValidateString_InvalidRegex_SilentlyIgnored()
    {
        // 非法正则不能抛异常（用户手输规则可能写错）
        var rule = JsonDocument.Parse("""{"regex": "[unclosed"}""").RootElement;
        Assert.Empty(FieldValidationRules.ValidateString("x", rule, null));
    }

    // ============================================================
    // ValidateDate
    // ============================================================

    [Fact]
    public void ValidateDate_BeforeMin_ReturnsError()
    {
        var rule = JsonDocument.Parse("""{"minDate": "2026-01-01"}""").RootElement;
        var errors = FieldValidationRules.ValidateDate(new DateOnly(2025, 12, 31), rule);
        Assert.Single(errors);
        Assert.Contains("日期不能早于", errors[0]);
    }

    [Fact]
    public void ValidateDate_AfterMax_ReturnsError()
    {
        var rule = JsonDocument.Parse("""{"maxDate": "2026-12-31"}""").RootElement;
        var errors = FieldValidationRules.ValidateDate(new DateOnly(2027, 1, 1), rule);
        Assert.Single(errors);
        Assert.Contains("日期不能晚于", errors[0]);
    }

    [Fact]
    public void ValidateDate_InRange_ReturnsEmpty()
    {
        var rule = JsonDocument.Parse(
            """{"minDate": "2026-01-01", "maxDate": "2026-12-31"}""").RootElement;
        Assert.Empty(FieldValidationRules.ValidateDate(new DateOnly(2026, 6, 15), rule));
    }

    // ============================================================
    // ValidateTime
    // ============================================================

    [Fact]
    public void ValidateTime_BeforeMin_ReturnsError()
    {
        var rule = JsonDocument.Parse("""{"minTime": "09:00:00"}""").RootElement;
        var errors = FieldValidationRules.ValidateTime(new TimeOnly(8, 0), rule);
        Assert.Single(errors);
        Assert.Contains("时间不能早于", errors[0]);
    }

    [Fact]
    public void ValidateTime_AfterMax_ReturnsError()
    {
        var rule = JsonDocument.Parse("""{"maxTime": "17:00:00"}""").RootElement;
        var errors = FieldValidationRules.ValidateTime(new TimeOnly(18, 0), rule);
        Assert.Single(errors);
        Assert.Contains("时间不能晚于", errors[0]);
    }

    [Fact]
    public void ValidateTime_InRange_ReturnsEmpty()
    {
        var rule = JsonDocument.Parse(
            """{"minTime": "09:00:00", "maxTime": "17:00:00"}""").RootElement;
        Assert.Empty(FieldValidationRules.ValidateTime(new TimeOnly(12, 0), rule));
    }

    // ============================================================
    // TryGetDecimal
    // ============================================================

    [Theory]
    [InlineData("42", 42)]
    [InlineData("3.14", 3.14)]
    [InlineData("-5", -5)]
    [InlineData("0", 0)]
    public void TryGetDecimal_FromNumericString_Parses(string input, double expected)
    {
        var elem = JsonDocument.Parse($"\"{input}\"").RootElement;
        Assert.True(FieldValidationRules.TryGetDecimal(elem, out var value));
        Assert.Equal((decimal)expected, value);
    }

    [Fact]
    public void TryGetDecimal_FromNumber_Parses()
    {
        var elem = JsonDocument.Parse("3.14").RootElement;
        Assert.True(FieldValidationRules.TryGetDecimal(elem, out var value));
        Assert.Equal(3.14m, value);
    }

    [Fact]
    public void TryGetDecimal_FromInvalidString_ReturnsFalse()
    {
        var elem = JsonDocument.Parse("\"not-a-number\"").RootElement;
        Assert.False(FieldValidationRules.TryGetDecimal(elem, out _));
    }

    [Fact]
    public void TryGetDecimal_FromObject_ReturnsFalse()
    {
        var elem = JsonDocument.Parse("""{"x":1}""").RootElement;
        Assert.False(FieldValidationRules.TryGetDecimal(elem, out _));
    }

    [Fact]
    public void TryGetDecimal_FromNull_ReturnsFalse()
    {
        var elem = JsonDocument.Parse("null").RootElement;
        Assert.False(FieldValidationRules.TryGetDecimal(elem, out _));
    }
}
```

## 文件 5/6 TreeGraph.Blazor.Tests/StartupTests.cs

```csharp
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace TreeGraph.Blazor.Tests;

/// <summary>
/// 启动级 smoke 测试：在测试宿主里跑完整的 Program.cs pipeline，
/// 触发所有 <c>ValidateOnStart</c> 的 Options 校验。
///
/// 目的：防止配置错误（如 EavApiClient 的 Resilience 参数）潜伏到运行期。
/// 背景：曾因 <c>MaxRetryAttempts = 0</c> + <c>SamplingDuration</c> 不满足
/// 约束，Aspire 编排启动时抛 OptionsValidationException，
/// 但 dotnet build 通过、单元测试也通过。
///
/// 首次构造 <see cref="BlazorAppFactory"/> 时，工厂会调用
/// <c>IHost.StartAsync()</c>，此时所有 <c>ValidateOnStart</c> 的 Options 会被校验；
/// 任何非法配置都会在测试中直接抛异常。
/// </summary>
public class StartupTests : IClassFixture<BlazorAppFactory>
{
    private readonly BlazorAppFactory _factory;

    public StartupTests(BlazorAppFactory factory) => _factory = factory;

    /// <summary>
    /// 宿主启动不抛异常即通过。工厂构造时已触发校验。
    /// </summary>
    [Fact]
    public void Host_Starts_WithValidOptions()
    {
        Assert.NotNull(_factory.Server);
    }

    /// <summary>
    /// 打通 HTTP pipeline：请求 Aspire 默认端点的 /alive。
    /// 覆盖：路由注册 + 中间件顺序 + 无意外依赖解析失败。
    /// </summary>
    [Fact]
    public async Task Health_Endpoint_Is_Reachable()
    {
        using var client = _factory.CreateClient();

        var resp = await client.GetAsync("/alive");

        // MapDefaultEndpoints 在 Development 环境注册 /alive；
        // 若注册未生效（环境判断出错），会 404，我们把它视为异常。
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}

/// <summary>
/// 测试宿主工厂：覆盖外部依赖，让 Program.cs 完整 pipeline 能在无 Aspire、
/// 无真实 DB、无 OTEL collector 的环境下启动。
/// </summary>
public sealed class BlazorAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // 与生产启动保持一致的环境（影响 MapDefaultEndpoints 是否注册 /alive）
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // 假连接串：不会真连（Blazor 不注册 DbContext，测试也不触达 DB 路径），
                // 仅作为占位配置保留。
                ["ConnectionStrings:TreeGraphDb"] =
                    "Host=localhost;Port=1;Database=fake;Username=fake;Password=fake",

                // AddServiceDefaults 检测到此为空 → 不启用 OTLP exporter，
                // 避免测试环境尝试连接 collector。
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "",

                // 关闭 Aspire 服务发现探测（若存在），避免无谓的 DNS 查询。
                // EavApiClient 的 BaseAddress 只是被设置，测试不真实发请求。
                ["Aspire:ServiceDiscovery:Enabled"] = "false",
            });
        });
    }
}
```

## 文件 6/6 TreeGraph.Blazor.Tests/TestDoubles/TestHttpMessageHandler.cs

```csharp
using System.Net;
using System.Text;
using System.Text.Json;

namespace TreeGraph.Blazor.Tests.TestDoubles;

/// <summary>
/// 轻量 HTTP handler：按 URL 前缀匹配返回预置 JSON。
/// 用于 bUnit 测试中替代真实网络。
///
/// 用法：
///   var handler = new TestHttpMessageHandler()
///       .Map("GET api/eav/metadata/custom-tables/1", dto)
///       .Map("GET api/eav/Product/entities/1/tables/certs", value);
///
/// 未匹配的请求返回 404，便于测试时立即暴露遗漏。
/// </summary>
public sealed class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly List<(string Method, string UrlKey, string Json)> _routes = new();

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public TestHttpMessageHandler Map(string method, string urlKey, object payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        _routes.Add((method.ToUpperInvariant(), urlKey, json));
        return this;
    }

    public TestHttpMessageHandler MapGet(string urlKey, object payload)
        => Map("GET", urlKey, payload);

    public TestHttpMessageHandler MapPut(string urlKey, object payload)
        => Map("PUT", urlKey, payload);

    public TestHttpMessageHandler MapPost(string urlKey, object payload)
        => Map("POST", urlKey, payload);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var method = request.Method.Method.ToUpperInvariant();
        var url = request.RequestUri?.ToString() ?? "";

        foreach (var (m, key, json) in _routes)
        {
            if (m != method) continue;
            if (!url.Contains(key, StringComparison.OrdinalIgnoreCase)) continue;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent(
                $"No route for {method} {url}",
                Encoding.UTF8, "text/plain")
        });
    }
}
```

