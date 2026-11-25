using Microsoft.EntityFrameworkCore;

namespace TreeGraph.IdentityAdmin.Audit;

/// <summary>身份管理独立数据库上下文（审计）。</summary>
public sealed class IdentityAdminDbContext : DbContext
{
    public IdentityAdminDbContext(DbContextOptions<IdentityAdminDbContext> options)
        : base(options)
    {
    }

    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();

    public DbSet<RealmBackupEntity> RealmBackups => Set<RealmBackupEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var audit = modelBuilder.Entity<AdminAuditLog>();
        audit.ToTable("admin_audit_log");
        audit.HasKey(x => x.Id);
        audit.Property(x => x.Id).HasColumnName("id");
        audit.Property(x => x.OccurredAt).HasColumnName("occurred_at").IsRequired();
        audit.Property(x => x.ActorSub).HasColumnName("actor_sub").IsRequired();
        audit.Property(x => x.ActorName).HasColumnName("actor_name");
        audit.Property(x => x.ServiceClientId).HasColumnName("service_client_id");
        audit.Property(x => x.Action).HasColumnName("action").IsRequired();
        audit.Property(x => x.TargetType).HasColumnName("target_type");
        audit.Property(x => x.TargetId).HasColumnName("target_id");
        audit.Property(x => x.RequestJson).HasColumnName("request_json").HasColumnType("jsonb");
        audit.Property(x => x.Result).HasColumnName("result").IsRequired();
        audit.Property(x => x.KeycloakStatus).HasColumnName("keycloak_status");
        audit.Property(x => x.CorrelationId).HasColumnName("correlation_id");
        audit.Property(x => x.Error).HasColumnName("error");

        audit.HasIndex(x => x.OccurredAt).HasDatabaseName("ix_admin_audit_log_occurred_at");
        audit.HasIndex(x => x.ActorSub).HasDatabaseName("ix_admin_audit_log_actor_sub");
        audit.HasIndex(x => x.Action).HasDatabaseName("ix_admin_audit_log_action");
        audit.HasIndex(x => x.Result).HasDatabaseName("ix_admin_audit_log_result");

        var backup = modelBuilder.Entity<RealmBackupEntity>();
        backup.ToTable("realm_backup");
        backup.HasKey(x => x.Id);
        backup.Property(x => x.Id).HasColumnName("id");
        backup.Property(x => x.Realm).HasColumnName("realm").IsRequired();
        backup.Property(x => x.FileName).HasColumnName("file_name").IsRequired();
        backup.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        backup.Property(x => x.ActorSub).HasColumnName("actor_sub").IsRequired();
        backup.Property(x => x.Action).HasColumnName("action").IsRequired();
        backup.Property(x => x.PayloadProtected).HasColumnName("payload_protected").IsRequired();

        backup.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_realm_backup_created_at");
        backup.HasIndex(x => x.Realm).HasDatabaseName("ix_realm_backup_realm");
    }
}
