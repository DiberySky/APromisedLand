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
    public List<GraphAgentSessionSummary> Sessions { get; set; } = new();
}

public class GraphAgentSessionSummary
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }
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
    
        /// <summary>
    /// 流式对话：通过回调逐块接收增量文本和最终事件。
    /// onDelta：收到增量文本时触发
    /// onDone：收到最终事件时触发（含工具调用详情）
    /// </summary>
    public async Task SendStreamAsync(
        string message,
        string? conversationId,
        Func<string, Task> onDelta,
        Func<GraphAgentReply, Task> onDone,
        CancellationToken cancellationToken = default)
    {
        var request = new GraphAgentRequest { Message = message, ConversationId = conversationId };

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post, "/api/graph-agent/chat/stream")
        {
            Content = JsonContent.Create(request, options: JsonOpts)
        };

        using var response = await _http.SendAsync(
            httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("流式失败 {Status}: {Body}", response.StatusCode, body);
            await onDone(new GraphAgentReply { Reply = "服务异常，请稍后重试。" });
            return;
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var accumulated = new System.Text.StringBuilder();
        GraphAgentReply? finalReply = null;

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            if (!line.StartsWith("data: ")) continue;
            var json = line[6..].Trim();
            if (string.IsNullOrEmpty(json)) continue;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // 兼容字符串 / 数字两种 type 格式
                string? type = null;
                if (root.TryGetProperty("type", out var typeEl))
                {
                    if (typeEl.ValueKind == JsonValueKind.String)
                        type = typeEl.GetString();
                    else if (typeEl.ValueKind == JsonValueKind.Number)
                        type = typeEl.GetInt32() switch
                        {
                            0 => "start", 1 => "delta", 2 => "done", _ => null
                        };
                }

                if (string.Equals(type, "delta", StringComparison.OrdinalIgnoreCase))
                {
                    var text = root.TryGetProperty("text", out var txt) && txt.ValueKind == JsonValueKind.String
                        ? txt.GetString() : null;
                    if (!string.IsNullOrEmpty(text))
                    {
                        accumulated.Append(text);
                        await onDelta(text);
                    }
                }
                else if (string.Equals(type, "done", StringComparison.OrdinalIgnoreCase))
                {
                    var replyText = root.TryGetProperty("text", out var txt) && txt.ValueKind == JsonValueKind.String
                        ? txt.GetString() : null;
                    var convId = root.TryGetProperty("conversationId", out var cid) && cid.ValueKind == JsonValueKind.String
                        ? cid.GetString() : null;

                    // ★ 新增：解析 AgentName
                    var agentName = root.TryGetProperty("agentName", out var an) && an.ValueKind == JsonValueKind.String
                        ? an.GetString() : null;

                    var tools = new List<string>();
                    if (root.TryGetProperty("toolsInvoked", out var ti) && ti.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var x in ti.EnumerateArray())
                            if (x.ValueKind == JsonValueKind.String)
                                tools.Add(x.GetString() ?? "");
                    }

                    var details = new List<GraphAgentToolCallDetail>();
                    if (root.TryGetProperty("toolCallDetails", out var tcd) &&
                        tcd.ValueKind == JsonValueKind.Array)
                    {
                        details = tcd.Deserialize<List<GraphAgentToolCallDetail>>(JsonOpts)
                                  ?? new List<GraphAgentToolCallDetail>();
                    }

                    finalReply = new GraphAgentReply
                    {
                        ConversationId = convId ?? "",
                        Reply = replyText ?? accumulated.ToString(),
                        ToolsInvoked = tools,
                        ToolCallDetails = details,
                        AgentName = agentName ?? ""   // ★ 新增
                    };
                }
                else if (string.Equals(type, "error", StringComparison.OrdinalIgnoreCase))
                {
                    var msg = root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String
                        ? m.GetString() : "未知错误";
                    finalReply = new GraphAgentReply { Reply = $"流式错误：{msg}" };
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "解析 SSE 事件失败：{Line}", line);
            }
        }

        if (finalReply is not null)
            await onDone(finalReply);
    }

    public async Task<List<GraphAgentSessionSummary>> ListSessionsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _http.GetFromJsonAsync<GraphAgentSessionsReply>(
                "/api/graph-agent/sessions", JsonOpts, cancellationToken);
            return result?.Sessions ?? new List<GraphAgentSessionSummary>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "加载会话列表失败");
            return new List<GraphAgentSessionSummary>();
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
    
    /// <summary>为会话设置显示名（空字符串表示恢复默认）。</summary>
    public async Task<bool> RenameSessionAsync(
        string conversationId,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(
                $"/api/graph-agent/sessions/{Uri.EscapeDataString(conversationId)}/rename",
                new { DisplayName = displayName ?? "" },
                JsonOpts,
                cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "重命名会话失败：{ConvId}", conversationId);
            return false;
        }
    }
}