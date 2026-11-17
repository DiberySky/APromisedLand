using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TreeGraph.Persistence.LineSky.Entities;
using TreeGraph.Shared.LineSky.Entities;
using TreeGraph.Shared.NodeSky.Entities;

namespace TreeGraph.Persistence.Data.Configurations.LineSky;

public class LineTerminalConfiguration : IEntityTypeConfiguration<LineTerminal>
{
    public void Configure(EntityTypeBuilder<LineTerminal> e)
    {
        e.ToTable("line_terminals");
        e.HasKey(x => new { x.LineId, x.NodeId });
        e.Property(x => x.LineId).HasColumnName("line_id").HasMaxLength(36).IsRequired();
        e.Property(x => x.NodeId).HasColumnName("node_id").HasMaxLength(36).IsRequired();
        e.Property(x => x.Role)
            .HasColumnName("role")
            .HasConversion<string>().HasMaxLength(20).IsRequired();
        e.Property(x => x.OperatingState)
            .HasColumnName("operating_state")
            .HasConversion<string>().HasMaxLength(20).IsRequired();
        e.Property(x => x.ExternalNodeId).HasColumnName("external_node_id").HasMaxLength(36);

        // 端子挂线路（删除线路由服务层守卫拦截，Restrict 兜底）
        e.HasOne<Line>()
            .WithMany()
            .HasForeignKey(x => x.LineId)
            .OnDelete(DeleteBehavior.Restrict);

        // R7：端子节点直指 nodes(id)（LineNode = nodes 注册行）；同线归属由服务层校验
        e.HasOne<Node>()
            .WithMany()
            .HasForeignKey(x => x.NodeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_lt_node");

        e.HasIndex(x => x.ExternalNodeId).HasDatabaseName("ix_line_terminals_external_node");
    }
}
