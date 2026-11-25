namespace TreeGraph.IdentityAdmin.Backup;

/// <summary>已持久化的 realm 备份元数据（不含明文负载）。</summary>
public sealed record RealmBackupRecord(
    Guid Id,
    string Realm,
    string FileName,
    DateTimeOffset CreatedAt,
    string ActorSub,
    string Action);

/// <summary>备份负载保护器。由 Web 层用 DataProtection 实现。</summary>
public interface IBackupProtector
{
    string Protect(string plaintext);

    string Unprotect(string protectedText);
}

/// <summary>realm 结构备份存储。仅保存 partial export，不保存密钥。</summary>
public interface IRealmBackupStore
{
    /// <summary>加密并保存备份，返回记录 ID。</summary>
    Task<Guid> SaveAsync(
        string realm,
        string fileName,
        string payloadJson,
        string actorSub,
        string action,
        CancellationToken ct = default);

    /// <summary>按创建时间倒序列出备份元数据。</summary>
    Task<IReadOnlyList<RealmBackupRecord>> ListAsync(int take = 20, CancellationToken ct = default);

    /// <summary>读取最近一次备份的明文负载，用于恢复或比对。</summary>
    Task<(RealmBackupRecord Record, string PayloadJson)?> GetLatestAsync(CancellationToken ct = default);
}
