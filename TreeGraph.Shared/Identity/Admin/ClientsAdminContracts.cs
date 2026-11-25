namespace TreeGraph.Shared.Identity.Admin;

/// <summary>客户端列表项。</summary>
public sealed record IdentityClientSummary(
    string Id,
    string ClientId,
    string? Name,
    string? Description,
    bool Enabled,
    bool PublicClient,
    string? Protocol);

/// <summary>客户端详情。</summary>
public sealed record IdentityClientDetail(
    string Id,
    string ClientId,
    string? Name,
    string? Description,
    bool Enabled,
    bool PublicClient,
    string? Protocol,
    string ClientAuthenticatorType,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> WebOrigins,
    bool StandardFlowEnabled,
    bool ImplicitFlowEnabled,
    bool DirectAccessGrantsEnabled,
    bool ServiceAccountsEnabled,
    string? RootUrl,
    string? BaseUrl,
    bool FullScopeAllowed,
    bool BearerOnly,
    IReadOnlyList<string> DefaultClientScopes,
    IReadOnlyList<string> OptionalClientScopes);

/// <summary>创建客户端请求。</summary>
public sealed record CreateClientRequest(
    string ClientId,
    string? Name = null,
    string? Description = null,
    bool PublicClient = true,
    string? RootUrl = null,
    string? BaseUrl = null,
    IReadOnlyList<string>? RedirectUris = null,
    IReadOnlyList<string>? WebOrigins = null,
    bool StandardFlowEnabled = true,
    bool DirectAccessGrantsEnabled = false,
    bool ServiceAccountsEnabled = false);

/// <summary>更新客户端请求。null 表示不修改。</summary>
public sealed record UpdateClientRequest(
    string? Name = null,
    string? Description = null,
    bool? Enabled = null,
    IReadOnlyList<string>? RedirectUris = null,
    IReadOnlyList<string>? WebOrigins = null,
    bool? StandardFlowEnabled = null,
    bool? ImplicitFlowEnabled = null,
    bool? DirectAccessGrantsEnabled = null,
    bool? ServiceAccountsEnabled = null,
    string? RootUrl = null,
    string? BaseUrl = null,
    bool? FullScopeAllowed = null);

/// <summary>客户端密钥（仅创建/轮换时返回一次）。</summary>
public sealed record ClientSecretResult(string ClientId, string Secret);

/// <summary>service account 已授予的 realm 角色名。</summary>
public sealed record ClientServiceAccountRoles(string ClientId, IReadOnlyList<string> RealmRoleNames);

/// <summary>客户端管理服务。</summary>
public interface IClientsAdminService
{
    Task<PagedResult<IdentityClientSummary>> SearchAsync(AdminPageRequest request, CancellationToken ct = default);

    Task<IdentityClientDetail?> GetAsync(string clientId, CancellationToken ct = default);

    Task<AdminResult<string>> CreateAsync(AdminActor actor, CreateClientRequest request, CancellationToken ct = default);

    Task<AdminResult> UpdateAsync(AdminActor actor, string clientId, UpdateClientRequest request, CancellationToken ct = default);

    Task<AdminResult> DeleteAsync(AdminActor actor, string clientId, CancellationToken ct = default);

    /// <summary>读取 client secret（仅 confidential 客户端）。</summary>
    Task<AdminResult<ClientSecretResult>> GetSecretAsync(string clientId, CancellationToken ct = default);

    /// <summary>轮换 client secret。</summary>
    Task<AdminResult<ClientSecretResult>> RegenerateSecretAsync(AdminActor actor, string clientId, CancellationToken ct = default);

    Task<AdminResult<ClientServiceAccountRoles>> GetServiceAccountRolesAsync(string clientId, CancellationToken ct = default);

    Task<AdminResult> AddServiceAccountRoleAsync(AdminActor actor, string clientId, string roleName, CancellationToken ct = default);

    Task<AdminResult> RemoveServiceAccountRoleAsync(AdminActor actor, string clientId, string roleName, CancellationToken ct = default);
}
