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

    public DocumentMetadataService(MafRagContext dbContext, ILogger<DocumentMetadataService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task InsertAsync(DocumentMetadata metadata, CancellationToken ct)
    {
        var entity = new DocumentMetadataEntity
        {
            DocId = metadata.DocId,
            Tenant = metadata.Tenant,
            Version = metadata.Version,
            FileName = metadata.FileName,
            FileSize = metadata.FileSize,
            MimeType = metadata.MimeType,
            BlobUri = metadata.BlobUri,
            BlobName = metadata.BlobName,
            ExtraMetadata = JsonSerializer.Serialize(metadata.ExtraMetadata ?? new Dictionary<string, object>())
        };
        await _dbContext.DocumentMetadata.AddAsync(entity, ct);
        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Inserted metadata for DocId {DocId}", metadata.DocId);
    }

    public async Task<DocumentMetadata?> GetLatestAsync(string docId, string tenant, CancellationToken ct)
    {
        var entity = await _dbContext.DocumentMetadata
            .Where(d => d.DocId == docId && d.Tenant == tenant)
            .OrderByDescending(d => d.Version)
            .FirstOrDefaultAsync(ct);
        if (entity == null) return null;
        return new DocumentMetadata
        {
            DocId = entity.DocId,
            Tenant = entity.Tenant,
            Version = entity.Version,
            FileName = entity.FileName,
            FileSize = entity.FileSize,
            MimeType = entity.MimeType,
            BlobUri = entity.BlobUri,
            BlobName = entity.BlobName,
            ExtraMetadata = JsonSerializer.Deserialize<Dictionary<string, object>>(entity.ExtraMetadata ?? "{}") ?? new()
        };
    }
}
