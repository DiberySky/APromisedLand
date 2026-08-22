using APromisedLand.Shared.DiberyTree.Attributes.Enums;
using APromisedLand.Shared.DiberyTree.Attributes.Models;

namespace APromisedLand.Shared.DiberyTree.Attributes.Validation;

/// <summary>
/// <see cref="AttributeDefinition"/> 的校验器，负责表/列定义的结构性约束。
/// </summary>
public static class DefinitionValidator
{
    /// <summary>
    /// 校验单个 <see cref="AttributeDefinition"/>：
    /// <para>1) 表定义（<c>ParentId</c> 为空）的类型必须为「表」；</para>
    /// <para>2) 列定义（<c>ParentId</c> 非空）的类型不可以是「表」（不支持递归嵌套子表）。</para>
    /// <para>调用时机：创建 / 更新属性定义时。传入的 def 须已加载 <c>AttributeType</c> 导航属性。</para>
    /// </summary>
    /// <returns>(是否合法, 错误信息)</returns>
    public static (bool IsValid, string? ErrorMessage) Validate(AttributeDefinition def)
    {
        var systemType = def.AttributeTypeId.ToAttributeTypeEnum();

        // ---------- 表定义：ParentId 为空 → 必须是「表」类型 ----------
        if (string.IsNullOrEmpty(def.ParentId))
        {
            if (systemType != AttributeTypeEnum.表格)
                return (false, $"表定义「{def.Name}」的类型必须为「表」");
            return (true, null);
        }

        // ---------- 列定义：ParentId 非空 → 不可以是「表」类型 ----------
        if (systemType == AttributeTypeEnum.表格)
            return (false, $"列「{def.Name}」的类型不可以是「表」（不支持递归嵌套子表）");

        return (true, null);
    }

    /// <summary>
    /// 便捷入口：仅校验列定义不可为「表」类型。等价于 <see cref="Validate"/> 在列定义分支的判定。
    /// </summary>
    public static (bool IsValid, string? ErrorMessage) ValidateColumn(AttributeDefinition columnDefinition)
    {
        if (string.IsNullOrEmpty(columnDefinition.ParentId))
            return (true, null); // 非列定义，不在本方法范围

        if (columnDefinition.AttributeTypeId.ToAttributeTypeEnum() == AttributeTypeEnum.表格)
            return (false, $"列「{columnDefinition.Name}」的类型不可以是「表」（不支持递归嵌套子表）");

        return (true, null);
    }
}
