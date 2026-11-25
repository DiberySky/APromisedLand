namespace TreeGraph.Shared.Identity.Admin;

/// <summary>管理审计记录（读取视图）。</summary>
public sealed record AdminAuditEntry(
    Guid Id,
    DateTimeOffset OccurredAt,
    string ActorSub,
    string? ActorName,
    string? ServiceClientId,
    string Action,
    string? TargetType,
    string? TargetId,
    string Result,
    int? KeycloakStatus,
    string? CorrelationId,
    string? Error);

/// <summary>审计查询条件。</summary>
public sealed record AdminAuditQuery(
    string? ActorSub = null,
    string? Action = null,
    string? TargetType = null,
    string? Result = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 50);

/// <summary>管理审计读取入口，供 /admin/audit 页面使用。</summary>
public interface IAdminAuditQueryService
{
    Task<PagedResult<AdminAuditEntry>> QueryAsync(AdminAuditQuery query, CancellationToken ct = default);
}

/// <summary>审计写入阶段。</summary>
public enum AuditPhase
{
    /// <summary>调用 Keycloak 之前写入的意图记录。</summary>
    Pending,

    /// <summary>调用完成后的结果记录。</summary>
    Completed
}

/// <summary>审计写入请求（实现层使用）。</summary>
public sealed record AdminAuditWrite(
    Guid? Id,
    AuditPhase Phase,
    AdminActor? Actor,
    string? ServiceClientId,
    string Action,
    string? TargetType,
    string? TargetId,
    string Result,
    int? KeycloakStatus,
    string? CorrelationId,
    string? Error,
    string? RequestJson);
