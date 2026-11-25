using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace TreeGraph.IdentityAdmin.Audit;

/// <summary>基于 <see cref="IdentityAdminDbContext"/> 的审计存储。</summary>
public sealed class AdminAuditStore : IAdminAuditStore
{
    private readonly IDbContextFactory<IdentityAdminDbContext> _factory;
    private readonly ILogger<AdminAuditStore> _logger;

    public AdminAuditStore(
        IDbContextFactory<IdentityAdminDbContext> factory,
        ILogger<AdminAuditStore> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task<Guid> BeginAsync(
        string actorSub,
        string? actorName,
        string? serviceClientId,
        string action,
        string? targetType,
        string? targetId,
        string? requestJson,
        string correlationId,
        CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var entity = new AdminAuditLog
        {
            Id = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            ActorSub = actorSub,
            ActorName = actorName,
            ServiceClientId = serviceClientId,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            RequestJson = requestJson,
            Result = "pending",
            CorrelationId = correlationId
        };

        db.AdminAuditLogs.Add(entity);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return entity.Id;
    }

    public async Task CompleteAsync(
        Guid id,
        string result,
        int? keycloakStatus,
        string? error,
        CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var entity = await db.AdminAuditLogs.FindAsync([id], ct).ConfigureAwait(false);
        if (entity is null)
        {
            _logger.LogWarning("审计记录不存在，无法更新结果：{AuditId}", id);
            return;
        }

        entity.Result = result;
        entity.KeycloakStatus = keycloakStatus;
        entity.Error = error;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task WriteFallbackAsync(string line, CancellationToken ct = default)
    {
        try
        {
            var directory = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(directory);
            var file = Path.Combine(directory, "audit-fallback.log");
            await File.AppendAllTextAsync(
                    file,
                    string.Concat(DateTimeOffset.UtcNow.ToString("O"), " ", line, Environment.NewLine),
                    ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "审计兜底文件写入失败：{Line}", line);
        }
    }
}
