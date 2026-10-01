using System.Text.Json;

namespace TreeGraph.Shared.Eav.Dtos;

/// <summary>动态实体（GET entities/{id} 及查询结果项的 JSON 形状）</summary>
public record DynamicEntityDto(
    long EntityId,
    string EntityType,
    Dictionary<string, JsonElement> Properties);

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
