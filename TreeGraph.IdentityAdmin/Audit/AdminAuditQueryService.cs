using Microsoft.EntityFrameworkCore;
using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.IdentityAdmin.Audit;

/// <summary>审计查询实现，供 /admin/audit 页面读取。</summary>
public sealed class AdminAuditQueryService : IAdminAuditQueryService
{
    private readonly IDbContextFactory<IdentityAdminDbContext> _factory;

    public AdminAuditQueryService(IDbContextFactory<IdentityAdminDbContext> factory)
        => _factory = factory;

    public async Task<PagedResult<AdminAuditEntry>> QueryAsync(AdminAuditQuery query, CancellationToken ct = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 500 ? 50 : query.PageSize;

        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var source = db.AdminAuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.ActorSub))
            source = source.Where(x => x.ActorSub == query.ActorSub);

        if (!string.IsNullOrWhiteSpace(query.Action))
            source = source.Where(x => x.Action == query.Action);

        if (!string.IsNullOrWhiteSpace(query.TargetType))
            source = source.Where(x => x.TargetType == query.TargetType);

        if (!string.IsNullOrWhiteSpace(query.Result))
            source = source.Where(x => x.Result == query.Result);

        if (query.From is { } from)
            source = source.Where(x => x.OccurredAt >= from);

        if (query.To is { } to)
            source = source.Where(x => x.OccurredAt <= to);

        var total = await source.CountAsync(ct).ConfigureAwait(false);
        var items = await source
            .OrderByDescending(x => x.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AdminAuditEntry(
                x.Id,
                x.OccurredAt,
                x.ActorSub,
                x.ActorName,
                x.ServiceClientId,
                x.Action,
                x.TargetType,
                x.TargetId,
                x.Result,
                x.KeycloakStatus,
                x.CorrelationId,
                x.Error))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new PagedResult<AdminAuditEntry>(items, total, page, pageSize);
    }
}
