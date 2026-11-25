namespace TreeGraph.Shared.Identity.Admin;

/// <summary>管理事件。</summary>
public sealed record AdminEventEntry(
    string? Time,
    string? OperationType,
    string? ResourcePath,
    string? ResourceType,
    string? Error,
    string? IpAddress,
    string? ClientId,
    string? UserId);

/// <summary>登录事件。</summary>
public sealed record LoginEventEntry(
    string? Time,
    string? Type,
    string? ClientId,
    string? UserId,
    string? SessionId,
    string? IpAddress,
    string? Error,
    IReadOnlyDictionary<string, string> Details);

/// <summary>事件查询条件。</summary>
public sealed record AdminEventQuery(
    string? OperationType = null,
    string? ResourceType = null,
    string? UserId = null,
    string? ClientId = null,
    string? DateFrom = null,
    string? DateTo = null,
    int First = 0,
    int Max = 50);

/// <summary>登录事件查询条件。</summary>
public sealed record LoginEventQuery(
    string? Type = null,
    string? UserId = null,
    string? ClientId = null,
    string? DateFrom = null,
    string? DateTo = null,
    int First = 0,
    int Max = 50);

/// <summary>事件管理服务。</summary>
public interface IEventsAdminService
{
    Task<IReadOnlyList<AdminEventEntry>> QueryAdminEventsAsync(AdminEventQuery query, CancellationToken ct = default);

    Task<IReadOnlyList<LoginEventEntry>> QueryLoginEventsAsync(LoginEventQuery query, CancellationToken ct = default);
}
