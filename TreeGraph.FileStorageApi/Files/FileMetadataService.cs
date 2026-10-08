using System.Diagnostics;                    // ★ 新增
using System.Text.Json;
using TreeGraph.FileStorageApi.Data;
using TreeGraph.FileStorageApi.Entities;
using TreeGraph.FileStorageApi.Security;
using TreeGraph.FileStorageApi.Storage;
using TreeGraph.Shared.FileStorageSky.Contracts;   // ★ DTO 已迁移到 Shared
using Microsoft.EntityFrameworkCore;

namespace TreeGraph.FileStorageApi.Files;

public sealed class FileMetadataService : IFileMetadataService
{
    private readonly FileStorageContext _db;
    private readonly IObjectStorage _storage;
    private readonly AuditQueue _auditQueue;
    private readonly ICallerContext _caller;
    private readonly ILogger<FileMetadataService> _logger;

    public FileMetadataService(
        FileStorageContext db,
        IObjectStorage storage,
        AuditQueue auditQueue,
        ICallerContext caller,
        ILogger<FileMetadataService> logger)
    {
        _db         = db;
        _storage    = storage;
        _auditQueue = auditQueue;
        _caller     = caller;
        _logger     = logger;
    }

    public async Task<IReadOnlyList<FileMetadataDto>> ListAsync(
        int skip, int take, CancellationToken ct)
    {
        // 在 LINQ 外求值 Tenant：HttpCallerContext.Tenant 超长时抛
        // InvalidTenantException，若放在 Where lambda 内会被 EF 的
        // ExpressionTreeFuncletizer 包装成 InvalidOperationException，
        // 绕过 Program.cs 的全局中间件（→400），最终被吞成 500。
        var tenant = _caller.Tenant;
        var items = await _db.DocumentMetadata
            .AsNoTracking()
            .Where(m => m.Tenant == tenant && m.Status != "deleted")
            .OrderByDescending(m => m.CreatedAt)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(ct);

        return items.Select(ToDto).ToList();
    }

    public async Task<FileMetadataDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var tenant = _caller.Tenant;
        var m = await _db.DocumentMetadata
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.Tenant == tenant, ct);
        return m is null ? null : ToDto(m);
    }

    public async Task<FileMetadataDto?> GetByDocIdAsync(
        string docId, int? version, CancellationToken ct)
    {
        var tenant = _caller.Tenant;
        var query = _db.DocumentMetadata
            .AsNoTracking()
            .Where(m => m.DocId == docId
                     && m.Tenant == tenant
                     && m.Status != "deleted"
                     && m.Status != "delete_pending");

        if (version.HasValue)
            query = query.Where(m => m.Version == version.Value);
        else
            query = query.OrderByDescending(m => m.Version);

        var m = await query.FirstOrDefaultAsync(ct);
        return m is null ? null : ToDto(m);
    }

    public async Task<DownloadResult?> DownloadAsync(
        Guid id, long? rangeStart, long? rangeEnd, CancellationToken ct)
    {
        var tenant = _caller.Tenant;
        var m = await _db.DocumentMetadata
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.Tenant == tenant, ct);

        if (m is null || m.Status != "active") return null;

        ObjectStorageGetResult get;
        try
        {
            get = await _storage.GetAsync(m.ObjectKey, rangeStart, rangeEnd, ct);
        }
        catch (ObjectNotFoundException)
        {
            _logger.LogWarning(
                "元数据 active 但 S3 对象不存在：id={Id}, key={Key}", id, m.ObjectKey);
            return null;
        }

        _auditQueue.TryEnqueue(new AuditEntry(
            DocId:       m.DocId,
            Tenant:      m.Tenant,
            Action:      "downloaded",
            Actor:       _caller.Actor,
            DetailsJson: JsonSerializer.Serialize(new { objectKey = m.ObjectKey })));

        return new DownloadResult(
            get.Content,
            ToDto(m),
            get.ContentLength,
            get.TotalLength,
            get.ContentRange);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var tenant = _caller.Tenant;
        var m = await _db.DocumentMetadata
            .FirstOrDefaultAsync(x => x.Id == id && x.Tenant == tenant, ct);
        if (m is null) return false;

        m.Status = "delete_pending";
        m.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var s3Ok = false;
        try
        {
            s3Ok = await _storage.DeleteAsync(m.ObjectKey, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "删除 S3 对象失败：{Key}", m.ObjectKey);
        }

        m.Status = s3Ok ? "deleted" : "delete_pending";
        m.UpdatedAt = DateTimeOffset.UtcNow;

        _db.DocumentAudits.Add(new DocumentAuditEntity
        {
            DocId       = m.DocId,
            Tenant      = m.Tenant,
            Action      = "deleted",
            Actor       = _caller.Actor,
            DetailsJson = JsonSerializer.Serialize(new { objectKey = m.ObjectKey, s3Ok }),
        });

        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static FileMetadataDto ToDto(DocumentMetadataEntity m) => new()
    {
        Id = m.Id, DocId = m.DocId, Version = m.Version, Tenant = m.Tenant,
        FileName = m.FileName, ContentType = m.ContentType, Size = m.Size,
        ObjectKey = m.ObjectKey, Sha256 = m.Sha256, Status = m.Status,
        CreatedAt = m.CreatedAt, UpdatedAt = m.UpdatedAt,
    };
}