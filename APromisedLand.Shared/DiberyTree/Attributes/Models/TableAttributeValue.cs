using APromisedLand.Shared.DiberyTree.Attributes.Enums;

namespace APromisedLand.Shared.DiberyTree.Attributes.Models;

/// <summary>
/// 表类型属性的“行实例”值。
/// <para>
/// 当某属性定义为 <see cref="AttributeTypeEnum.表格"/> 时，其值不是单值，
/// 而是若干行实例（本类型）。每行实例又承载多列值：列值复用各 Typed AttributeValue 表，
/// 列值的 <c>NodeId</c> 指向本行实例 <c>Id</c>，<c>AttributeDefinitionId</c> 指向列定义。
/// </para>
/// <para>
/// 递归层级：顶级 Node → TableAttributeValue(行实例, DefId=表定义)
///           → Text/Decimal/… 列值(NodeId=行实例 Id, DefId=列定义)。
/// </para>
/// </summary>
public class TableAttributeValue : AttributeValueBase
{
    /// <summary>
    /// 行序号，用于表内多行排序（可选）。
    /// </summary>
    public int? RowNo { get; set; }
}
