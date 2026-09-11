using System.Text.Json;
using APromisedLand.Api.Data;
using APromisedLand.Api.MafRag.Entities;
using MAFRagService.Models;
using Microsoft.EntityFrameworkCore;

namespace MAFRagService.Services;

public class DocumentMetadataService
{
    private readonly MafRagContext _dbContext;
    private readonly ILogger<DocumentMetadataService> _logger;

    public DocumentMetadataService(
        MafRagContext dbContext,
        ILogger<DocumentMetadataService> logger)
    {
        _dbContext = dbContext;
        _logger    = logger;
    }

    public async Task InsertAsync(DocumentMetadata metadata, CancellationToken ct)
    {
        var entity = new DocumentMetadataEntity
        {
            DocId    = metadata.DocId,
            Tenant   = metadata.Tenant,
            Version  = metadata.Version,
            FileName = metadata.FileName,
            FileSize = metadata.FileSize,
            MimeType = metadata.MimeType,
            BlobUri  = metadata.BlobUri,
            BlobName = metadata.BlobName,
            ExtraMetadata = JsonSerializer.Serialize(
                metadata.ExtraMetadata ?? new Dictionary<string, object>())
        };
        await _dbContext.DocumentMetadata.AddAsync(entity, ct);
        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Inserted metadata for DocId {DocId}", metadata.DocId);
    }

    public async Task<DocumentMetadata?> GetLatestAsync(
        string docId, string tenant, CancellationToken ct)
    {
        var entity = await _dbContext.DocumentMetadata
            .Where(d => d.DocId == docId && d.Tenant == tenant)
            .OrderByDescending(d => d.Version)
            .FirstOrDefaultAsync(ct);

        return entity is null ? null : ToModel(entity);
    }

    // ============================================================
    // ★ 新增：按 (DocId, Version, Tenant) 精确查询
    // ============================================================
    public async Task<DocumentMetadata?> GetAsync(
        string docId, string version, string tenant, CancellationToken ct)
    {
        var entity = await _dbContext.DocumentMetadata
            .FirstOrDefaultAsync(d =>
                d.DocId == docId && d.Version == version && d.Tenant == tenant, ct);

        return entity is null ? null : ToModel(entity);
    }

    // ============================================================
    // ★ 新增：文档生命周期状态（active / inactive / archived）
    //   ⚠️ 与索引状态（IndexTaskEntity）职责分离，不要写入 Indexing/Completed。
    // ============================================================
    public async Task SetLifecycleStatusAsync(
        string docId, string version, string tenant,
        string status, CancellationToken ct)
    {
        var entity = await _dbContext.DocumentMetadata
            .FirstOrDefaultAsync(d =>
                d.DocId == docId && d.Version == version && d.Tenant == tenant, ct);
        if (entity is null) return;

        entity.Status    = status;
        entity.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Document lifecycle updated: DocId={DocId}, Version={Version}, Status={Status}",
            docId, version, status);
    }

    private static DocumentMetadata ToModel(DocumentMetadataEntity entity) => new()
    {
        DocId    = entity.DocId,
        Tenant   = entity.Tenant,
        Version  = entity.Version,
        FileName = entity.FileName,
        FileSize = entity.FileSize,
        MimeType = entity.MimeType,
        BlobUri  = entity.BlobUri,
        BlobName = entity.BlobName,
        ExtraMetadata = string.IsNullOrWhiteSpace(entity.ExtraMetadata)
            ? new()
            : JsonSerializer.Deserialize<Dictionary<string, object>>(entity.ExtraMetadata) ?? new()
    };
}