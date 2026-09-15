using FileStorageApi.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FileStorageApi.Data;

public class FileStorageContext(DbContextOptions<FileStorageContext> options)
    : DbContext(options)
{
    public DbSet<DocumentMetadataEntity> DocumentMetadata { get; set; }
    public DbSet<DocumentAuditEntity>    DocumentAudits   { get; set; }
    public DbSet<IndexTaskEntity>        IndexTasks       { get; set; }
    public DbSet<UploadSessionEntity>    UploadSessions   { get; set; }
    public DbSet<UploadChunkEntity>      UploadChunks     { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset) ||
                    property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(
                        new ValueConverter<DateTimeOffset, DateTimeOffset>(
                            v => v.ToUniversalTime(), v => v));
                }
            }
        }

        modelBuilder.Entity<DocumentMetadataEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.DocId, e.Version, e.Tenant }).IsUnique();
            entity.HasIndex(e => new { e.Tenant, e.DocId });
            entity.HasIndex(e => new { e.Status, e.CreatedAt });    // ★ 补偿扫描用
            entity.Property(e => e.Status).HasDefaultValue("active").HasMaxLength(32);
            entity.Property(e => e.FileName).HasMaxLength(512);
            entity.Property(e => e.ContentType).HasMaxLength(256);
            entity.Property(e => e.ObjectKey).HasMaxLength(1024);
            entity.Property(e => e.Sha256).HasMaxLength(64);
            entity.Property(e => e.Tenant).HasMaxLength(128);
            entity.Property(e => e.DocId).HasMaxLength(128);
        });

        modelBuilder.Entity<DocumentAuditEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.DocId, e.Tenant, e.CreatedAt });
            entity.Property(e => e.Action).HasMaxLength(64);
            entity.Property(e => e.Actor).HasMaxLength(256);
            entity.Property(e => e.Tenant).HasMaxLength(128);
            entity.Property(e => e.DocId).HasMaxLength(128);
        });

        modelBuilder.Entity<IndexTaskEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Status, e.CreatedAt });
            entity.Property(e => e.Status).HasMaxLength(32);
            entity.Property(e => e.Tenant).HasMaxLength(128);
            entity.Property(e => e.DocId).HasMaxLength(128);
        });

        modelBuilder.Entity<UploadSessionEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Status, e.ExpiresAt });
            entity.HasIndex(e => new { e.Tenant, e.CreatedAt });

            // ★ P0-2：部分唯一索引——仅对"活跃"会话生效，让客户端用 Fingerprint 恢复上传
            entity.HasIndex(e => new { e.Tenant, e.Fingerprint })
                .IsUnique()
                .HasFilter("\"Fingerprint\" IS NOT NULL " +
                           "AND \"Status\" IN ('pending','uploading','merging')");

            entity.Property(e => e.Status).HasMaxLength(32);
            entity.Property(e => e.FileName).HasMaxLength(512);
            entity.Property(e => e.ContentType).HasMaxLength(256);
            entity.Property(e => e.ObjectKey).HasMaxLength(1024);
            entity.Property(e => e.Sha256).HasMaxLength(64);
            entity.Property(e => e.Tenant).HasMaxLength(128);
            entity.Property(e => e.DocId).HasMaxLength(128);
            entity.Property(e => e.Fingerprint).HasMaxLength(128);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2048);
        });

        modelBuilder.Entity<UploadChunkEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UploadId, e.ChunkIndex }).IsUnique();
            entity.Property(e => e.Sha256).HasMaxLength(64);

            entity.HasOne(e => e.Session)
                  .WithMany(s => s.Chunks)
                  .HasForeignKey(e => e.UploadId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// 幂等 upsert 分块：利用 (UploadId, ChunkIndex) 唯一约束，
    /// 并发上传同一分块不会 500，而是安全覆盖。
    /// </summary>
    public async Task UpsertChunkAsync(
        Guid uploadId, int chunkIndex, byte[] data, string sha256,
        CancellationToken ct)
    {
        const string sql = """
            INSERT INTO "UploadChunks"
                ("Id", "UploadId", "ChunkIndex", "Size", "Data", "Sha256", "CreatedAt")
            VALUES
                (@id, @uploadId, @chunkIndex, @size, @data, @sha256, @createdAt)
            ON CONFLICT ("UploadId", "ChunkIndex") DO UPDATE SET
                "Data"      = EXCLUDED."Data",
                "Size"      = EXCLUDED."Size",
                "Sha256"    = EXCLUDED."Sha256",
                "CreatedAt" = EXCLUDED."CreatedAt";
            """;

        await Database.ExecuteSqlRawAsync(sql,
            new Npgsql.NpgsqlParameter("id", Guid.NewGuid()),
            new Npgsql.NpgsqlParameter("uploadId", uploadId),
            new Npgsql.NpgsqlParameter("chunkIndex", chunkIndex),
            new Npgsql.NpgsqlParameter("size", data.Length),
            new Npgsql.NpgsqlParameter("data", data),
            new Npgsql.NpgsqlParameter("sha256", (object?)sha256 ?? DBNull.Value),
            new Npgsql.NpgsqlParameter("createdAt", DateTimeOffset.UtcNow));
    }
}