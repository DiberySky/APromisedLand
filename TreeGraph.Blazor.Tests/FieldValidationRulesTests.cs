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
