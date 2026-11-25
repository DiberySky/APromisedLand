namespace TreeGraph.Shared.Identity.Admin;

/// <summary>realm 设置读取视图。</summary>
public sealed record RealmSettings(
    string Realm,
    string? DisplayName,
    bool RegistrationAllowed,
    bool BruteForceProtected,
    string? PasswordPolicy,
    int AccessTokenLifespan,
    int SsoSessionIdleTimeout,
    int SsoSessionMaxLifespan,
    int FailedLoginAttempts,
    int WaitIncrementSeconds,
    int LockoutDurationSeconds,
    string? LoginTheme,
    string? AccountTheme,
    string? AdminTheme,
    string? EmailTheme,
    IReadOnlyList<string> DefaultGroups,
    bool SmtpConfigured,
    string? SmtpHost,
    string? SmtpFrom);

/// <summary>更新 realm 设置请求。null 表示不修改。</summary>
public sealed record UpdateRealmSettingsRequest(
    string? DisplayName = null,
    bool? RegistrationAllowed = null,
    bool? BruteForceProtected = null,
    string? PasswordPolicy = null,
    int? AccessTokenLifespan = null,
    int? SsoSessionIdleTimeout = null,
    int? SsoSessionMaxLifespan = null,
    int? FailedLoginAttempts = null,
    int? WaitIncrementSeconds = null,
    int? LockoutDurationSeconds = null,
    string? LoginTheme = null,
    string? AccountTheme = null,
    string? AdminTheme = null,
    string? EmailTheme = null,
    IReadOnlyList<string>? DefaultGroups = null);

/// <summary>SMTP 设置。Password 为空表示不修改。</summary>
public sealed record SmtpSettingsRequest(
    string Host,
    int Port,
    string? From = null,
    string? FromDisplayName = null,
    bool StartTls = true,
    bool Ssl = false,
    string? User = null,
    string? Password = null);

/// <summary>已部署主题列表。</summary>
public sealed record AvailableThemes(
    IReadOnlyList<string> Login,
    IReadOnlyList<string> Account,
    IReadOnlyList<string> Admin,
    IReadOnlyList<string> Email);

/// <summary>realm 设置管理服务。</summary>
public interface IRealmSettingsAdminService
{
    Task<RealmSettings?> GetAsync(CancellationToken ct = default);

    Task<AdminResult> UpdateAsync(AdminActor actor, UpdateRealmSettingsRequest request, CancellationToken ct = default);

    Task<AdminResult> UpdateSmtpAsync(AdminActor actor, SmtpSettingsRequest request, CancellationToken ct = default);

    Task<AvailableThemes> GetAvailableThemesAsync(CancellationToken ct = default);
}
