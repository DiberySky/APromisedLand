namespace TreeGraph.Shared.Identity.Admin;

/// <summary>仪表盘聚合数据。</summary>
public sealed record DashboardSummary(
    int UserCount,
    int EnabledUserCount,
    int ActiveSessionCount,
    int ClientCount,
    int GroupCount,
    int RoleCount,
    string? KeycloakVersion,
    bool KeycloakHealthy,
    IReadOnlyList<AdminAuditEntry> RecentAudit);

/// <summary>仪表盘服务。</summary>
public interface IDashboardAdminService
{
    Task<DashboardSummary> GetSummaryAsync(CancellationToken ct = default);

    /// <summary>读取 Keycloak 实际版本并校验是否等于期望版本。</summary>
    Task<(string? Version, bool MatchesExpected)> GetVersionAsync(CancellationToken ct = default);
}
