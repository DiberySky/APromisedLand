namespace TreeGraph.Api.NodeEavSky.Entities;

/// <summary>属性变更审计日志（与值变更同事务提交）</summary>
public class AttributeAuditLog
{
    public string AuditId { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string AttributeId { get; set; } = "";
    public string AttributeName { get; set; } = "";
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }

    /// <summary>Insert / Update / Delete</summary>
    public string ChangeType { get; set; } = "";

    public string ChangedBy { get; set; } = "";
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? CorrelationId { get; set; }
    public string? ClientIp { get; set; }
}
