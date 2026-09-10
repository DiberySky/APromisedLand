using APromisedLand.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace MAFRagService.Services;

public class VersionManager 
{
    private readonly MafRagContext _dbContext;
    private readonly ILogger<VersionManager> _logger;

    public VersionManager(MafRagContext dbContext, ILogger<VersionManager> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<string> GetLatestVersionAsync(string docId, string tenant, CancellationToken ct)
    {
        var entity = await _dbContext.DocumentMetadata
            .Where(d => d.DocId == docId && d.Tenant == tenant)
            .OrderByDescending(d => d.UploadedAt)
            .FirstOrDefaultAsync(ct);
        return entity?.Version ?? "latest";
    }

    public async Task<List<string>> GetAllVersionsAsync(string docId, string tenant, CancellationToken ct)
    {
        return await _dbContext.DocumentMetadata
            .Where(d => d.DocId == docId && d.Tenant == tenant)
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => d.Version)
            .ToListAsync(ct);
    }

    public async Task DeactivateVersionAsync(string docId, string version, string tenant, CancellationToken ct)
    {
        var entity = await _dbContext.DocumentMetadata
            .FirstOrDefaultAsync(d => d.DocId == docId && d.Version == version && d.Tenant == tenant, ct);
        if (entity != null)
        {
            entity.Status = "inactive";
            entity.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogInformation("Deactivated version {Version} for DocId {DocId}", version, docId);
        }
    }
}
