using APromisedLand.Shared.DiberyTree.Attributes.Models;
using Microsoft.EntityFrameworkCore;

namespace APromisedLand.Api.Data;

/// <summary>
/// <see cref="DiberyDbContext"/> 的 partial：承载“动态表（AttributeType=表）”相关的 EF Core 配置。
/// 主文件零逻辑改动，仅通过 <see cref="OnModelCreatingPartial"/> 挂载。
/// </summary>
public partial class DiberyDbContext
{
    /// <summary>
    /// 注册动态表相关的实体配置：
    /// <para>1) <see cref="APromisedLand.Shared.DiberyTree.Attributes.Models.TableRowAttributeValue"/> 行实例表（TPC 派生类型）；</para>
    /// </summary>
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // ------ TableRowAttributeValue：表类型属性的“行实例”值 ------
        // 根类型 AttributeValueBase 已在主文件配置为 TPC，此处仅声明派生表名与额外字段。
        modelBuilder.Entity<TableRowAttributeValue>(entity =>
        {
            entity.ToTable("TableRowAttributeValues");
            entity.Property(e => e.TableId).HasMaxLength(36);
            entity.Property(e => e.RowNo);
        });

        // 注意：已移除 AttributeDefinition 的自引用导航关系配置（HasOne/WithMany/ForeignKey/OnDelete）。
        // 现在 ParentId 仅作为普通属性列存在，无数据库外键约束。
        // 请在业务层自行校验 ParentId 的有效性，并处理删除时的子项检查。
    }
}