namespace TreeGraph.Shared.Identity.Admin;

/// <summary>
/// realm 签名/加密密钥视图。ProviderId 即 Keycloak 中对应 KeyProvider 组件的 id。
/// </summary>
public sealed record RealmSigningKey(
    string ProviderId,
    string? Kid,
    string? Type,
    string? Algorithm,
    string? Status,
    int? ProviderPriority,
    string? PublicKey,
    string? Certificate,
    string? ValidTo)
{
    /// <summary>是否为当前用于签发的活动密钥（Keycloak 状态 ACTIVE）。</summary>
    public bool IsActive => string.Equals(Status, "ACTIVE", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// 生成新签名密钥请求。默认新增一组 RSA/RS256 密钥，priority 高于现有密钥后由 Keycloak 提升为 ACTIVE。
/// </summary>
public sealed record GenerateRealmKeyRequest(
    string ProviderId = "rsa-generated",
    string Algorithm = "RS256",
    int KeySize = 2048,
    int Priority = 100);

/// <summary>
/// realm 密钥管理。写操作属高危（可导致全站令牌校验失败），需 step-up 并自动备份 realm。
/// </summary>
public interface IRealmKeysAdminService
{
    /// <summary>列出当前 realm 的签名与加密密钥。</summary>
    Task<IReadOnlyList<RealmSigningKey>> ListAsync(CancellationToken ct = default);

    /// <summary>生成一组新密钥。旧密钥不会被自动删除，保持 PASSIVE 以便平滑过渡。</summary>
    Task<AdminResult<RealmSigningKey>> GenerateAsync(
        AdminActor actor,
        GenerateRealmKeyRequest request,
        CancellationToken ct = default);

    /// <summary>删除指定密钥。拒绝删除 ACTIVE 密钥。</summary>
    Task<AdminResult> DeleteAsync(AdminActor actor, string providerId, CancellationToken ct = default);
}
