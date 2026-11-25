using Microsoft.EntityFrameworkCore;
using TreeGraph.IdentityAdmin.Audit;

namespace TreeGraph.IdentityAdmin.Backup;

/// <summary>基于 IdentityAdminDb 的备份存储，负载经 IBackupProtector 加密。</summary>
public sealed class EfRealmBackupStore : IRealmBackupStore
{
    private readonly IDbContextFactory<IdentityAdminDbContext> _factory;
    private readonly IBackupProtector _protector;
    private readonly TimeProvider _timeProvider;

    public EfRealmBackupStore(
        IDbContextFactory<IdentityAdminDbContext> factory,
        IBackupProtector protector,
        TimeProvider timeProvider)
    {
        _factory = factory;
        _protector = protector;
        _timeProvider = timeProvider;
    }

    public async Task<Guid> SaveAsync(
        string realm,
        string fileName,
        string payloadJson,
        string actorSub,
        string action,
        CancellationToken ct = default)
    {
        var entity = new RealmBackupEntity
        {
            Id = Guid.NewGuid(),
            Realm = realm,
            FileName = fileName,
            CreatedAt = _timeProvider.GetUtcNow(),
            ActorSub = actorSub,
            Action = action,
            PayloadProtected = _protector.Protect(payloadJson)
        };

        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.RealmBackups.Add(entity);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return entity.Id;
    }

    public async Task<IReadOnlyList<RealmBackupRecord>> ListAsync(
        int take = 20,
        CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);

        return await db.RealmBackups
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(take, 1, 200))
            .Select(x => new RealmBackupRecord(x.Id, x.Realm, x.FileName, x.CreatedAt, x.ActorSub, x.Action))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<(RealmBackupRecord Record, string PayloadJson)?> GetLatestAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var entity = await db.RealmBackups
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (entity is null)
            return null;

        var record = new RealmBackupRecord(
            entity.Id, entity.Realm, entity.FileName, entity.CreatedAt, entity.ActorSub, entity.Action);

        return (record, _protector.Unprotect(entity.PayloadProtected));
    }
}
