using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DiberyBlazorWebSky.Services;

public class GraphAgentRequest
{
    [JsonPropertyName("conversationId")]
    public string? ConversationId { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}

public class GraphAgentToolCallDetail
{
    [JsonPropertyName("toolName")]
    public string ToolName { get; set; } = "";

    [JsonPropertyName("arguments")]
    public string Arguments { get; set; } = "";

    [JsonPropertyName("result")]
    public string? Result { get; set; }

    [JsonPropertyName("elapsedMs")]
    public long ElapsedMs { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("fromCache")]
    public bool FromCache { get; set; }   // ★ 新增
}

public class GraphAgentReply
{
    [JsonPropertyName("conversationId")]
    public string ConversationId { get; set; } = "";

    [JsonPropertyName("agentName")]
    public string AgentName { get; set; } = "";

    [JsonPropertyName("reply")]
    public string Reply { get; set; } = "";

    [JsonPropertyName("messageCount")]
    public int MessageCount { get; set; }

    [JsonPropertyName("toolsInvoked")]
    public List<string> ToolsInvoked { get; set; } = new();

    [JsonPropertyName("toolCallDetails")]
    public List<GraphAgentToolCallDetail> ToolCallDetails { get; set; } = new();
}

public class GraphAgentSessionsReply
{
    [JsonPropertyName("sessions")]
    public List<string> Sessions { get; set; } = new();
}

public class GraphAgentSessionMessagesReply
{
    [JsonPropertyName("conversationId")]
    public string ConversationId { get; set; } = "";

    [JsonPropertyName("messages")]
    public List<GraphAgentSessionMessage> Messages { get; set; } = new();
}

public class GraphAgentSessionMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [JsonPropertyName("authorName")]
    public string? AuthorName { get; set; }
}

public class GraphAgentApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<GraphAgentApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public GraphAgentApiClient(HttpClient http, ILogger<GraphAgentApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<GraphAgentReply> SendAsync(
        string message,
        string? conversationId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GraphAgentRequest { Message = message, ConversationId = conversationId };
            var response = await _http.PostAsJsonAsync(
                "/api/graph-agent/chat", request, JsonOpts, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("GraphAgent 返回 {Status}: {Body}", response.StatusCode, body);
                return new GraphAgentReply { Reply = "服务返回异常，请稍后重试。" };
            }

            var result = await response.Content.ReadFromJsonAsync<GraphAgentReply>(
                JsonOpts, cancellationToken);
            return result ?? new GraphAgentReply { Reply = "（服务返回空响应）" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "调用 GraphAgent 失败");
            return new GraphAgentReply { Reply = $"请求失败：{ex.Message}" };
        }
    }

    public async Task<List<string>> ListSessionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _http.GetFromJsonAsync<GraphAgentSessionsReply>(
                "/api/graph-agent/sessions", JsonOpts, cancellationToken);
            return result?.Sessions ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "加载会话列表失败");
            return new List<string>();
        }
    }

    public async Task<List<GraphAgentSessionMessage>> GetMessagesAsync(
        string conversationId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _http.GetFromJsonAsync<GraphAgentSessionMessagesReply>(
                $"/api/graph-agent/sessions/{Uri.EscapeDataString(conversationId)}/messages",
                JsonOpts, cancellationToken);
            return result?.Messages ?? new List<GraphAgentSessionMessage>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "加载会话历史失败：{ConvId}", conversationId);
            return new List<GraphAgentSessionMessage>();
        }
    }

    public async Task<bool> ResetSessionAsync(
        string conversationId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _http.PostAsync(
                $"/api/graph-agent/reset/{Uri.EscapeDataString(conversationId)}",
                content: null, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "删除会话失败：{ConvId}", conversationId);
            return false;
        }
    }
}