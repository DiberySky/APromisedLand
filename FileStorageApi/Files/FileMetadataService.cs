using System.Diagnostics;                    // ★ 新增
using System.Text.Json;
using FileStorageApi.Data;
using FileStorageApi.Entities;
using FileStorageApi.Models;
using FileStorageApi.Security;
using FileStorageApi.Storage;
using Microsoft.EntityFrameworkCore;

namespace FileStorageApi.Files;

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
        var items = await _db.DocumentMetadata
            .AsNoTracking()
            .Where(m => m.Tenant == _caller.Tenant && m.Status != "deleted")
            .OrderByDescending(m => m.CreatedAt)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(ct);

        return items.Select(ToDto).ToList();
    }

    public async Task<FileMetadataDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var m = await _db.DocumentMetadata
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.Tenant == _caller.Tenant, ct);
        return m is null ? null : ToDto(m);
    }

    public async Task<FileMetadataDto?> GetByDocIdAsync(
        string docId, int? version, CancellationToken ct)
    {
        var query = _db.DocumentMetadata
            .AsNoTracking()
            .Where(m => m.DocId == docId
                     && m.Tenant == _caller.Tenant
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
        var m = await _db.DocumentMetadata
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.Tenant == _caller.Tenant, ct);

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
        var m = await _db.DocumentMetadata
            .FirstOrDefaultAsync(x => x.Id == id && x.Tenant == _caller.Tenant, ct);
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