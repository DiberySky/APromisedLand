namespace TreeGraph.Shared.Identity.Admin;

/// <summary>用户联邦提供方列表项。</summary>
public sealed record UserFederationProviderSummary(
    string Id,
    string Name,
    string ProviderName,
    bool Enabled);

/// <summary>用户联邦提供方详情（LDAP / AD）。</summary>
public sealed record UserFederationProviderDetail(
    string Id,
    string Name,
    string ProviderName,
    bool Enabled,
    bool ImportEnabled,
    bool EditMode,
    bool SyncRegistrations,
    string? Vendor,
    string? ConnectionUrl,
    string? UsersDn,
    string? BindDn,
    string? UsernameLdapAttribute,
    string? RdnLdapAttribute,
    string? UuidLdapAttribute,
    string? UserObjectClasses,
    string? CustomUserSearchFilter);

/// <summary>保存 LDAP/AD 联邦请求。BindCredential 为空表示不修改。</summary>
public sealed record SaveLdapFederationRequest(
    string Name,
    bool Enabled,
    bool ImportEnabled,
    bool EditMode,
    bool SyncRegistrations,
    string? Vendor,
    string ConnectionUrl,
    string UsersDn,
    string? BindDn,
    string? BindCredential,
    string? UsernameLdapAttribute,
    string? RdnLdapAttribute,
    string? UuidLdapAttribute,
    string? UserObjectClasses,
    string? CustomUserSearchFilter);

/// <summary>联邦连接测试结果。</summary>
public sealed record FederationTestResult(bool Success, string? Message, int? StatusCode);

/// <summary>同步结果。</summary>
public sealed record FederationSyncResult(int Added, int Updated, int Removed, int Failed);

/// <summary>用户联邦管理服务。</summary>
public interface IFederationAdminService
{
    Task<IReadOnlyList<UserFederationProviderSummary>> ListAsync(CancellationToken ct = default);

    Task<UserFederationProviderDetail?> GetAsync(string componentId, CancellationToken ct = default);

    Task<AdminResult<string>> CreateAsync(AdminActor actor, SaveLdapFederationRequest request, CancellationToken ct = default);

    Task<AdminResult> UpdateAsync(AdminActor actor, string componentId, SaveLdapFederationRequest request, CancellationToken ct = default);

    Task<AdminResult> DeleteAsync(AdminActor actor, string componentId, CancellationToken ct = default);

    /// <summary>测试连接（不落库变更）。</summary>
    Task<FederationTestResult> TestConnectionAsync(SaveLdapFederationRequest request, CancellationToken ct = default);

    /// <summary>触发用户同步。</summary>
    Task<AdminResult<FederationSyncResult>> SyncAsync(AdminActor actor, string componentId, CancellationToken ct = default);
}
