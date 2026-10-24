using System.Text.RegularExpressions;

namespace TreeGraph.Api.NodeEavSky.Services;

/// <summary>
/// 关系属性的图边类型（relation_type）解析与校验。
///
/// 解析优先级：
///   ① 显式提供 → 采用
///   ② 留空 + 用户指定英文属性名 → 驼峰拆词转 snake_case（UserDept → user_dept）
///   ③ 留空 + 自动生成的 attr_ 名 → 无法推导，报错
///
/// 校验规则：存前转小写、^[a-z][a-z0-9_]*$、≤100、拒绝内置图边类型。
/// </summary>
public static class RelationNaming
{
    /// <summary>无法推导时对外的统一错误文案。</summary>
    public const string CannotDeriveMessage =
        "关系属性必须填写 relation_type，或指定英文属性名";

    private static readonly Regex ValidPattern =
        new(@"^[a-z][a-z0-9_]*$", RegexOptions.Compiled);

    // 驼峰边界：小写/数字 → 大写；连续大写后接小写（HTTPStatus → HTTP|Status）
    private static readonly Regex LowerUpperBoundary =
        new(@"(?<=[a-z0-9])(?=[A-Z])", RegexOptions.Compiled);
    private static readonly Regex AcronymBoundary =
        new(@"(?<=[A-Z])(?=[A-Z][a-z])", RegexOptions.Compiled);

    /// <summary>
    /// 系统内置图边类型，关系属性不可占用。
    /// R7：线路归属概念废弃（Line→LineNode 走 parent_of），原归属边类型名从保留名移除；
    /// line_segment 保留（由 seed 属性 next 占用，用户不可再占用）。
    /// </summary>
    private static readonly HashSet<string> ReservedTypes = new(StringComparer.Ordinal)
    {
        "parent_of", "connects_to", "line_segment"
    };

    /// <summary>
    /// 解析创建关系属性时的 relation_type。
    /// </summary>
    /// <param name="relationType">请求显式提供的值（可为空）。</param>
    /// <param name="attributeName">最终属性名（用户指定或自动生成）。</param>
    /// <param name="userSpecifiedName">属性名是否由用户显式指定。</param>
    /// <returns>成功返回 (规范化后的值, null)；失败返回 (null, 错误文案)。</returns>
    public static (string? Value, string? Error) Resolve(
        string? relationType, string? attributeName, bool userSpecifiedName)
    {
        string candidate;
        if (!string.IsNullOrWhiteSpace(relationType))
        {
            candidate = relationType.Trim();
        }
        else if (userSpecifiedName && !string.IsNullOrWhiteSpace(attributeName))
        {
            candidate = CamelToSnake(attributeName!.Trim());
        }
        else
        {
            return (null, CannotDeriveMessage);
        }

        var error = Validate(candidate);
        return error is null ? (candidate.ToLowerInvariant(), null) : (null, error);
    }

    /// <summary>校验（已按规则先转小写）。返回 null 表示通过。</summary>
    public static string? Validate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return CannotDeriveMessage;

        var v = value.Trim().ToLowerInvariant();

        if (v.Length > 100)
            return "relation_type 过长（最大 100 字符）";

        if (!ValidPattern.IsMatch(v))
            return "relation_type 必须以字母开头，只含字母、数字、下划线（不能以 _ 开头）";

        if (ReservedTypes.Contains(v))
            return $"relation_type 为系统保留类型，不可使用：{v}";

        return null;
    }

    private static string CamelToSnake(string name)
    {
        var s = LowerUpperBoundary.Replace(name, "_");
        s = AcronymBoundary.Replace(s, "_");
        return s.ToLowerInvariant();
    }
}
