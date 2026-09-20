using System.ComponentModel.DataAnnotations;

namespace MAFWorkFlowApi.Models;

public sealed class GraphAgentRequest
{
    public string? ConversationId { get; set; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(4000, MinimumLength = 1)]
    public string Message { get; set; } = string.Empty;
}

public sealed record GraphAgentReply(
    string ConversationId,
    string AgentName,
    string Reply,
    int MessageCount,
    IReadOnlyList<string> ToolsInvoked,
    IReadOnlyList<ToolCallDetailDto> ToolCallDetails);