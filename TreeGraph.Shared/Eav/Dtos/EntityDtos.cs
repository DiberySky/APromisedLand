using System.Text.Json;

namespace TreeGraph.Shared.Eav.Dtos;

/// <summary>
/// 动态实体（GET entities/{id} 及查询结果项的 JSON 形状）。
///
/// ★ #3 保存冲突检测新增 UpdatedAt：
///   - 值为该实体所有 AttributeValue 行的最大 UpdatedAt
///   - 实体无属性值时（新建），为 null
///   - 客户端保存时把它回传（X-Expected-Updated-At header），
///     服务端比对 DB 中最新值，不符则返回 409 Conflict
/// </summary>
public record DynamicEntityDto(
    long EntityId,
    string EntityType,
    Dictionary<string, JsonElement> Properties,
    DateTimeOffset? UpdatedAt = null);

/// <summary>审计历史项（GET entities/{id}/history）</summary>
public record EntityHistoryDto(
    long AuditId,
    long EntityId,
    string EntityType,
    long AttributeId,
    string AttributeName,
    string? OldValue,
    string? NewValue,
    string ChangeType,
    string ChangedBy,
    DateTimeOffset ChangedAt,
    string? CorrelationId,
    string? ClientIp);

/// <summary>按自定义表行内数据查询父实体（POST entities/query-by-table）</summary>
public class QueryByTableRequest
{
    public string AttributeName { get; set; } = "";
    public List<Dictionary<string, object?>> RowConditions { get; set; } = new();
}

/// <summary>
/// 批量删除请求（POST api/eav/{entityType}/entities/batch-delete）。
/// </summary>
public class BatchDeleteRequest
{
    /// <summary>待删除的实体 ID 列表。重复 ID 会被去重。</summary>
    public List<long> EntityIds { get; set; } = new();
}

/// <summary>
/// 批量删除结果。
///
/// - Deleted    实际删除的实体 ID（存在且删除成功）
/// - NotFound   请求中不存在于该实体类型下的 ID
/// - TotalAttributesDeleted  所有被删除实体累计删除的属性值行数（含审计计数）
/// </summary>
public record BatchDeleteResultDto(
    IReadOnlyList<long> Deleted,
    IReadOnlyList<long> NotFound,
    int TotalAttributesDeleted);
