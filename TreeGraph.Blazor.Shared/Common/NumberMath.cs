using System.Globalization;

namespace TreeGraph.Blazor.Shared.Common;

/// <summary>
/// 数字控件内部换算：支持 int/long/decimal/double 及其可空形式，统一以 decimal 计算。
/// 注意：double 超出 decimal 精度范围的极端值不适用（UI 数字字段不涉及）。
/// </summary>
internal static class NumberMath
{
    public static Type Underlying(Type t) => Nullable.GetUnderlyingType(t) ?? t;

    public static decimal ToDecimal(object? value, decimal fallback = 0m)
        => value switch
        {
            null => fallback,
            int i => i,
            long l => l,
            decimal m => m,
            double d => (decimal)d,
            _ => fallback
        };

    public static object FromDecimal(decimal v, Type target)
    {
        target = Underlying(target);
        if (target == typeof(int)) return (int)v;
        if (target == typeof(long)) return (long)v;
        if (target == typeof(double)) return (double)v;
        return v;
    }

    /// <summary>按目标类型解析输入文本；int/long 拒绝小数。</summary>
    public static bool TryParse(string text, Type target, out decimal v)
    {
        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out v)
            && !decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out v))
        {
            v = 0m;
            return false;
        }

        target = Underlying(target);
        if (target == typeof(int) || target == typeof(long))
            return decimal.Truncate(v) == v;
        return true;
    }

    public static string Format(decimal v, Type target)
    {
        target = Underlying(target);
        return target == typeof(double)
            ? ((double)v).ToString("G15", CultureInfo.InvariantCulture)
            : v.ToString(CultureInfo.InvariantCulture);
    }
}
