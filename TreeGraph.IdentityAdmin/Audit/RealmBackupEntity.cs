namespace TreeGraph.IdentityAdmin.Audit;

/// <summary>realm 结构备份记录（负载加密存储）。</summary>
public sealed class RealmBackupEntity
{
    public Guid Id { get; set; }
    public string Realm { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public string ActorSub { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string PayloadProtected { get; set; } = string.Empty;
}
