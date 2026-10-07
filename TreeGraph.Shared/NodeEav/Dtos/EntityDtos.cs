using System.Text.Json;

namespace TreeGraph.Shared.NodeEav.Dtos;

/// <summary>
/// 动态实体（GET entities/{id} 及查询结果项）。
///
/// EntityId 是 GUID 字符串（36 字符）。
/// UpdatedAt 用于乐观锁（GET 拿、PUT/PATCH 回传）。
/// </summary>
public record DynamicEntityDto(
    string EntityId,
    string EntityType,
    Dictionary<string, JsonElement> Properties,
    DateTimeOffset? UpdatedAt = null);

/// <summary>审计历史项（GET entities/{id}/history）。</summary>
public record EntityHistoryDto(
    string AuditId,
    string EntityId,
    string EntityType,
    string AttributeId,
    string AttributeName,
    string? OldValue,
    string? NewValue,
    string ChangeType,
    string ChangedBy,
    DateTimeOffset ChangedAt,
    string? CorrelationId,
    string? ClientIp);

/// <summary>按自定义表行内数据查询父实体（POST entities/query-by-table）。</summary>
public class QueryByTableRequest
{
    public string AttributeName { get; set; } = "";
    public List<Dictionary<string, object?>> RowConditions { get; set; } = new();
}

/// <summary>批量删除请求（POST entities/batch-delete）。</summary>
public class BatchDeleteRequest
{
    /// <summary>待删除的实体 ID 列表（GUID 字符串）。重复会被去重。</summary>
    public List<string> EntityIds { get; set; } = new();
}

/// <summary>批量删除结果。</summary>
public record BatchDeleteResultDto(
    IReadOnlyList<string> Deleted,
    IReadOnlyList<string> NotFound,
    int TotalAttributesDeleted);
