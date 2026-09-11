using APromisedLand.Api.MafRag.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace APromisedLand.Api.Data;

public class MafRagContext(DbContextOptions<MafRagContext> options) : DbContext(options)
{
    public DbSet<DocumentMetadataEntity> DocumentMetadata { get; set; }
    public DbSet<DocumentAuditEntity>    DocumentAudits   { get; set; }
    public DbSet<IndexTaskEntity>        IndexTasks       { get; set; }
    public DbSet<DomainEventEntity>      DomainEvents     { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ---------- UTC 转换 ----------
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset) ||
                    property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(
                        new ValueConverter<DateTimeOffset, DateTimeOffset>(
                            v => v.ToUniversalTime(),
                            v => v));
                }
            }
        }

        // ---------- DocumentMetadata ----------
        modelBuilder.Entity<DocumentMetadataEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            // 业务唯一键：同一租户下，同一 DocId 的同一版本只能有一条
            entity.HasIndex(e => new { e.DocId, e.Version, e.Tenant }).IsUnique();

            // 查询辅助：按租户 + DocId 查全部版本
            entity.HasIndex(e => new { e.Tenant, e.DocId });

            entity.Property(e => e.Status).HasDefaultValue("active");
        });

        // ---------- DocumentAudit ----------
        // 审计表刻意不做 FK：
        //   · DocId 不是 DocumentMetadataEntity 的唯一键（同 DocId 有多个 Version），
        //     用 HasPrincipalKey(d => d.DocId) 会在迁移时创建唯一约束并失败。
        //   · 审计语义是"独立追责"，不应随元数据删除而级联。
        //   · 如需导航，请为 DocumentAuditEntity 增加 Guid? DocumentMetadataId，
        //     再配置 HasOne(e => e.Document).WithMany().HasForeignKey(...)
        //           .OnDelete(DeleteBehavior.SetNull)。
        modelBuilder.Entity<DocumentAuditEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.DocId, e.Tenant, e.CreatedAt });
        });

        // ---------- IndexTask ----------
        modelBuilder.Entity<IndexTaskEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            // 全局扫描：清理超时任务 / 统计
            entity.HasIndex(e => new { e.Status, e.CreatedAt });

            // IndexTaskService.BeginAsync 的活跃任务查询
            entity.HasIndex(e => new { e.DocId, e.Tenant, e.Status });
        });

        // ---------- DomainEvent ----------
        modelBuilder.Entity<DomainEventEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            // 事件溯源的乐观并发键
            entity.HasIndex(e => new { e.StreamId, e.Version }).IsUnique();
        });

        base.OnModelCreating(modelBuilder);
    }
}