using APromisedLand.Api.Data;
using APromisedLand.Api.MafRag.Entities;
using Microsoft.EntityFrameworkCore;

namespace MAFRagService.Services;

public sealed class DocumentAuditService
{
    private readonly MafRagContext _db;

    public DocumentAuditService(MafRagContext db) => _db = db;

    public async Task RecordAsync(
        string docId, string tenant, string action,
        string? oldVersion, string? newVersion, string? @operator,
        CancellationToken ct)
    {
        _db.DocumentAudits.Add(new DocumentAuditEntity
        {
            DocId      = docId,
            Tenant     = tenant,
            Action     = action,
            OldVersion = oldVersion,
            NewVersion = newVersion,
            Operator   = @operator,
            CreatedAt  = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<DocumentAuditEntity>> ListAsync(
        string docId, string tenant, CancellationToken ct)
        => await _db.DocumentAudits
            .Where(a => a.DocId == docId && a.Tenant == tenant)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
}