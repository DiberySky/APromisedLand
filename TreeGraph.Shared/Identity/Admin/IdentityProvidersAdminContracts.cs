namespace TreeGraph.Shared.Identity.Admin;

/// <summary>身份提供方列表项。</summary>
public sealed record IdentityProviderSummary(
    string Alias,
    string ProviderId,
    string? DisplayName,
    bool Enabled);

/// <summary>身份提供方详情。</summary>
public sealed record IdentityProviderDetail(
    string Alias,
    string ProviderId,
    string? DisplayName,
    bool Enabled,
    bool TrustEmail,
    bool StoreToken,
    bool LinkOnly,
    IReadOnlyDictionary<string, string> Config);

/// <summary>身份提供方 mapper。</summary>
public sealed record IdentityProviderMapperInfo(
    string Id,
    string Name,
    string IdentityProviderAlias,
    string IdentityProviderMapper,
    IReadOnlyDictionary<string, string> Config);

/// <summary>保存身份提供方请求。Config 中敏感字段以占位符表示"保持不变"。</summary>
public sealed record SaveIdentityProviderRequest(
    string Alias,
    string ProviderId,
    string? DisplayName = null,
    bool Enabled = true,
    bool TrustEmail = false,
    bool StoreToken = false,
    bool LinkOnly = false,
    IReadOnlyDictionary<string, string>? Config = null);

/// <summary>身份提供方管理服务。</summary>
public interface IIdentityProvidersAdminService
{
    Task<IReadOnlyList<IdentityProviderSummary>> ListAsync(CancellationToken ct = default);

    Task<IdentityProviderDetail?> GetAsync(string alias, CancellationToken ct = default);

    Task<AdminResult> CreateAsync(AdminActor actor, SaveIdentityProviderRequest request, CancellationToken ct = default);

    Task<AdminResult> UpdateAsync(AdminActor actor, string alias, SaveIdentityProviderRequest request, CancellationToken ct = default);

    Task<AdminResult> DeleteAsync(AdminActor actor, string alias, CancellationToken ct = default);

    Task<IReadOnlyList<IdentityProviderMapperInfo>> ListMappersAsync(string alias, CancellationToken ct = default);

    Task<AdminResult<string>> CreateMapperAsync(AdminActor actor, string alias, IdentityProviderMapperInfo mapper, CancellationToken ct = default);

    Task<AdminResult> DeleteMapperAsync(AdminActor actor, string alias, string mapperId, CancellationToken ct = default);
}
