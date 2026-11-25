namespace TreeGraph.Shared.Identity.Admin;

/// <summary>
/// 客户端登出传播配置。back-channel 由 Keycloak 直接回调客户端，
/// front-channel 通过浏览器 iframe 传播；两者均可在会话失效时联动注销。
/// </summary>
public sealed record ClientLogoutSettings(
    string ClientId,
    bool BackchannelLogoutSessionRequired,
    bool BackchannelLogoutRevokeOfflineTokens,
    bool FrontchannelLogoutSessionRequired,
    string? FrontchannelLogoutUrl);

/// <summary>更新登出传播配置请求。null 表示不修改。</summary>
public sealed record UpdateClientLogoutSettingsRequest(
    bool? BackchannelLogoutSessionRequired = null,
    bool? BackchannelLogoutRevokeOfflineTokens = null,
    bool? FrontchannelLogoutSessionRequired = null,
    string? FrontchannelLogoutUrl = null);

/// <summary>客户端登出传播配置管理服务。</summary>
public interface IClientLogoutAdminService
{
    Task<ClientLogoutSettings?> GetAsync(string clientId, CancellationToken ct = default);

    Task<AdminResult> UpdateAsync(
        AdminActor actor,
        string clientId,
        UpdateClientLogoutSettingsRequest request,
        CancellationToken ct = default);
}
