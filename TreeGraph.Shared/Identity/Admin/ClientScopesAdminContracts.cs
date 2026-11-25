namespace TreeGraph.Shared.Identity.Admin;

/// <summary>client scope。</summary>
public sealed record IdentityClientScope(
    string Id,
    string Name,
    string? Description,
    string? Protocol,
    bool IsDefault);

/// <summary>protocol mapper。</summary>
public sealed record IdentityProtocolMapper(
    string Id,
    string Name,
    string Protocol,
    string ProtocolMapper,
    IReadOnlyDictionary<string, string> Config);

/// <summary>创建/更新 client scope 请求。</summary>
public sealed record SaveClientScopeRequest(
    string Name,
    string? Description = null,
    string? Protocol = "openid-connect");

/// <summary>创建/更新 protocol mapper 请求。</summary>
public sealed record SaveProtocolMapperRequest(
    string Name,
    string ProtocolMapper,
    string Protocol = "openid-connect",
    IReadOnlyDictionary<string, string>? Config = null);

/// <summary>client scope 管理服务。</summary>
public interface IClientScopesAdminService
{
    Task<IReadOnlyList<IdentityClientScope>> ListAsync(CancellationToken ct = default);

    Task<IdentityClientScope?> GetAsync(string scopeId, CancellationToken ct = default);

    Task<AdminResult<string>> CreateAsync(AdminActor actor, SaveClientScopeRequest request, CancellationToken ct = default);

    Task<AdminResult> UpdateAsync(AdminActor actor, string scopeId, SaveClientScopeRequest request, CancellationToken ct = default);

    Task<AdminResult> DeleteAsync(AdminActor actor, string scopeId, CancellationToken ct = default);

    Task<IReadOnlyList<IdentityProtocolMapper>> ListMappersAsync(string scopeId, CancellationToken ct = default);

    Task<AdminResult<string>> CreateMapperAsync(AdminActor actor, string scopeId, SaveProtocolMapperRequest request, CancellationToken ct = default);

    Task<AdminResult> UpdateMapperAsync(AdminActor actor, string scopeId, string mapperId, SaveProtocolMapperRequest request, CancellationToken ct = default);

    Task<AdminResult> DeleteMapperAsync(AdminActor actor, string scopeId, string mapperId, CancellationToken ct = default);

    /// <summary>把 scope 设为客户端的 default scope。</summary>
    Task<AdminResult> AddDefaultScopeToClientAsync(AdminActor actor, string clientId, string scopeId, CancellationToken ct = default);

    /// <summary>把 scope 设为客户端的 optional scope。</summary>
    Task<AdminResult> AddOptionalScopeToClientAsync(AdminActor actor, string clientId, string scopeId, CancellationToken ct = default);

    /// <summary>移除客户端的 default scope。</summary>
    Task<AdminResult> RemoveDefaultScopeFromClientAsync(AdminActor actor, string clientId, string scopeId, CancellationToken ct = default);

    /// <summary>移除客户端的 optional scope。</summary>
    Task<AdminResult> RemoveOptionalScopeFromClientAsync(AdminActor actor, string clientId, string scopeId, CancellationToken ct = default);
}
