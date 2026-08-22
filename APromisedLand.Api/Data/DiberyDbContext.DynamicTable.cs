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
    /// <para>1) <see cref="TableAttributeValue"/> 行实例表（TPC 派生类型）；</para>
    /// <para>2) <see cref="AttributeDefinition"/> 的 <c>ParentId</c> 自引用关系（表定义 ↔ 列定义）。</para>
    /// </summary>
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // ------ TableAttributeValue：表类型属性的“行实例”值 ------
        // 根类型 AttributeValueBase 已在主文件配置为 TPC，此处仅声明派生表名与额外字段。
        modelBuilder.Entity<TableAttributeValue>(entity =>
        {
            entity.ToTable("TableAttributeValues");
            entity.Property(e => e.RowNo);
        });

        // ------ AttributeDefinition 自引用（表定义 ↔ 列定义）------
        // 列定义的 ParentId 指向所属表定义；表定义自身 ParentId 为 null。
        // 一张表可由多种 AttributeType 的列混合组成（文本/整数/小数/日期/时间/日期时间/文件/定位）。
        // 约束：列本身不可以是「表」类型（不支持递归嵌套子表）；
        //       由服务层在创建/更新列定义时校验：列定义(ParentId 非空)的 AttributeTypeId 不可为表类型。
        modelBuilder.Entity<AttributeDefinition>(entity =>
        {
            entity.HasOne(d => d.Parent)
                  .WithMany()
                  .HasForeignKey(d => d.ParentId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(d => d.ParentId);
            entity.Property(d => d.ParentId).HasMaxLength(36);
            entity.Property(d => d.Order);
            entity.Property(d => d.IsRequired);
            entity.Property(d => d.DefaultValue).HasMaxLength(2048);
        });
    }
}
