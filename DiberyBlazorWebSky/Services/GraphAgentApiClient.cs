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
}

/// <summary>GET /api/graph-agent/sessions 响应。</summary>
public class GraphAgentSessionsReply
{
    [JsonPropertyName("sessions")]
    public List<string> Sessions { get; set; } = new();
}

/// <summary>GET /api/graph-agent/sessions/{id}/messages 响应。</summary>
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

/// <summary>调用 MAFWorkFlowApi 的 GraphAgent 端点。</summary>
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

    // ─── 发消息 ───────────────────────────────────────

    public async Task<GraphAgentReply> SendAsync(
        string message,
        string? conversationId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GraphAgentRequest
            {
                Message = message,
                ConversationId = conversationId
            };

            var response = await _http.PostAsJsonAsync(
                "/api/graph-agent/chat",
                request,
                JsonOpts,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "GraphAgent 返回 {Status}: {Body}", response.StatusCode, body);

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

    // ─── 会话列表 ─────────────────────────────────────

    public async Task<List<string>> ListSessionsAsync(
        CancellationToken cancellationToken = default)
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

    // ─── 会话消息历史 ────────────────────────────────

    public async Task<List<GraphAgentSessionMessage>> GetMessagesAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _http.GetFromJsonAsync<GraphAgentSessionMessagesReply>(
                $"/api/graph-agent/sessions/{Uri.EscapeDataString(conversationId)}/messages",
                JsonOpts,
                cancellationToken);

            return result?.Messages ?? new List<GraphAgentSessionMessage>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "加载会话历史失败：{ConvId}", conversationId);
            return new List<GraphAgentSessionMessage>();
        }
    }

    // ─── 删除会话 ─────────────────────────────────────

    public async Task<bool> ResetSessionAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _http.PostAsync(
                $"/api/graph-agent/reset/{Uri.EscapeDataString(conversationId)}",
                content: null,
                cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "删除会话失败：{ConvId}", conversationId);
            return false;
        }
    }
}