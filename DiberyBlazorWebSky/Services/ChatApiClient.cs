using System.Net;
using DiberyBlazorWebSky.Models;

namespace DiberyBlazorWebSky.Services;

/// <summary>
///     通过 Aspire 服务发现调用 MAFWorkFlowApi 的聊天接口。
///     与后端 AgentsController 的对应关系：
///     POST /api/agents/chat                      → SendAsync
///     GET  /api/agents/sessions                  → ListSessionsAsync
///     GET  /api/agents/sessions/{id}/messages    → GetSessionMessagesAsync
///     POST /api/agents/reset/{id}                → ResetAsync
/// </summary>
public class ChatApiClient(HttpClient http, ILogger<ChatApiClient> logger)
{
    private const string ChatEndpoint = "/api/agents/chat";
    private const string SessionsEndpoint = "/api/agents/sessions";
    private const string SessionMessagesPrefix = "/api/agents/sessions/";
    private const string ResetEndpointPrefix = "/api/agents/reset/";

    public async Task<ChatResponse?> SendAsync(
        string message,
        string? conversationId = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "Sending message to API (conversation: {ConversationId})",
            conversationId ?? "<new>");

        var response = await http.PostAsJsonAsync(
            ChatEndpoint,
            new ChatRequest { Message = message, ConversationId = conversationId },
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListSessionsAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync(SessionsEndpoint, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var result = await response.Content
            .ReadFromJsonAsync<SessionsResponse>(cancellationToken);

        return result?.Sessions ?? [];
    }

    /// <summary>获取指定会话的完整消息历史。</summary>
    public async Task<IReadOnlyList<SessionMessage>> GetSessionMessagesAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        var url = $"{SessionMessagesPrefix}{Uri.EscapeDataString(conversationId)}/messages";
        var response = await http.GetAsync(url, cancellationToken);

        // 404 表示会话不存在或没有消息，视为空列表
        if (response.StatusCode == HttpStatusCode.NotFound)
            return [];

        await EnsureSuccessAsync(response, cancellationToken);

        var result = await response.Content
            .ReadFromJsonAsync<SessionMessagesReply>(cancellationToken);

        return result?.Messages ?? [];
    }

    public async Task ResetAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Resetting conversation {ConversationId}", conversationId);

        var response = await http.PostAsync(
            $"{ResetEndpointPrefix}{Uri.EscapeDataString(conversationId)}",
            null,
            cancellationToken);

        // 后端删除不存在会话时返回 404，前端视为"已不存在"
        if (response.StatusCode == HttpStatusCode.NotFound)
            return;

        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        var message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "API 返回 401（未授权）",
            HttpStatusCode.Forbidden => "API 返回 403（禁止访问）",
            HttpStatusCode.NotFound => "API 返回 404（端点不存在，请检查路由）",
            HttpStatusCode.Conflict => "会话忙，请稍后重试",
            _ => $"API 返回 {(int)response.StatusCode} {response.ReasonPhrase}"
        };

        if (!string.IsNullOrWhiteSpace(body))
            message += $"：{body}";

        throw new HttpRequestException(message, null, response.StatusCode);
    }
}