namespace TreeGraph.IdentityAdmin.Audit;

/// <summary>管理审计日志实体，对应表 <c>admin_audit_log</c>。</summary>
public sealed class AdminAuditLog
{
    public Guid Id { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public string ActorSub { get; set; } = string.Empty;

    public string? ActorName { get; set; }

    public string? ServiceClientId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string? TargetType { get; set; }

    public string? TargetId { get; set; }

    public string? RequestJson { get; set; }

    public string Result { get; set; } = "pending";

    public int? KeycloakStatus { get; set; }

    public string? CorrelationId { get; set; }

    public string? Error { get; set; }
}
