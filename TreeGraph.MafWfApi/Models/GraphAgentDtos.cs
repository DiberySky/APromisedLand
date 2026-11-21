using System.ComponentModel.DataAnnotations;

namespace TreeGraph.MafWfApi.Models;

/// <summary>POST /api/graph-agent/chat 请求体。</summary>
public sealed class GraphAgentRequest
{
    public string? ConversationId { get; set; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(4000, MinimumLength = 1)]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 跳过意图路由，强制走图检索（/search 语义搜索页使用：
    /// 输入即检索意图，短名词不应被判为 assistant 的闲聊）。
    /// </summary>
    public bool ForceGraph { get; set; }
}

/// <summary>POST /api/graph-agent/chat 响应体。</summary>
public sealed record GraphAgentReply(
    string ConversationId,
    string AgentName,
    string Reply,
    int MessageCount,
    IReadOnlyList<string> ToolsInvoked,
    IReadOnlyList<ToolCallDetailDto> ToolCallDetails);

// ★ 新增：会话列表带显示名
public sealed record SessionSummary(string Id, string? DisplayName);

public sealed record GraphAgentSessionsReply(IReadOnlyList<SessionSummary> Sessions);

/// <summary>POST /api/graph-agent/sessions/{id}/rename 请求体。</summary>
public sealed class RenameSessionRequest
{
    [StringLength(200)]
    public string? DisplayName { get; set; }
}

/// <summary>POST /api/graph-agent/expand-node 请求体。</summary>
public sealed class ExpandNodeRequest
{
    public string? ConversationId { get; set; }

    [Required(AllowEmptyStrings = false)]
    public string InodeId { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string HostType { get; set; } = string.Empty;

    /// <summary>子树展开层数（0 = 仅节点自身，默认 3 层）。</summary>
    [Range(0, 10)]
    public int Depth { get; set; } = 3;
}

/// <summary>POST /api/graph-agent/expand-node 响应体（非流式一次性返回）。</summary>
public sealed record NodeExpandResult(
    string ConversationId,
    string AgentName,
    string Reply,
    bool Found,
    /// <summary>子树是否因节点数上限（50）被截断。</summary>
    bool Truncated = false,
    /// <summary>截断前实际加载的节点数。</summary>
    int TotalNodes = 0);