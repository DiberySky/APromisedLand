using System.Text.Json;
using TreeGraph.Blazor.Shared.NodeEavSky.Services;
using TreeGraph.Shared.NodeEavSky.Dtos;
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
