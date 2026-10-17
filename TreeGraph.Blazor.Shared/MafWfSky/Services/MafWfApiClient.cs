using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TreeGraph.Shared.MafWfSky.Contracts;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Blazor.Shared.MafWfSky.Services;

/// <summary>
/// TreeGraph.MafWfApi 客户端。
/// 后端响应已统一包装为 ApiResponse&lt;T&gt; 信封（{ success, message, data }），
/// 本客户端负责解包并把业务错误转为异常。
/// </summary>
public class MafWfApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly MafWfSkyOptions _options;
    private readonly ILogger<MafWfApiClient> _logger;

    public MafWfApiClient(
        HttpClient http,
        MafWfSkyOptions options,
        ILogger<MafWfApiClient> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    private string ChatBase => _options.ChatPath.TrimEnd('/');
    private string GraphAgentBase => _options.GraphAgentPath.TrimEnd('/');
    private string AgentsBase => _options.AgentsPath.TrimEnd('/');
    private string WorkflowsBase => _options.WorkflowsPath.TrimEnd('/');
    private string NamingBase => _options.NamingPath.TrimEnd('/');

    // ══════════════════════════════════════════════════════
    // 无工具对话（/api/chat）
    // ══════════════════════════════════════════════════════

    public async Task<ChatResponseDto> ChatAsync(string message, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync(ChatBase,
            new ChatRequestDto { Message = message }, JsonOptions, ct);
        return (await UnwrapAsync<ChatResponseDto>(resp, ct))!;
    }

    // ══════════════════════════════════════════════════════
    // Graph Agent（/api/graph-agent）
    // ══════════════════════════════════════════════════════

    public async Task<AgentReplyDto> GraphAgentChatAsync(
        string? conversationId, string message, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync($"{GraphAgentBase}/chat",
            new AgentChatRequest { ConversationId = conversationId, Message = message },
            JsonOptions, ct);
        return (await UnwrapAsync<AgentReplyDto>(resp, ct))!;
    }

    /// <summary>展开节点合成回答（非流式一次性返回）。</summary>
    public async Task<NodeExpandResultDto> ExpandNodeAsync(
        string? conversationId, string inodeId, string hostType,
        int depth = 3,
        CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync($"{GraphAgentBase}/expand-node",
            new { conversationId, inodeId, hostType, depth }, JsonOptions, ct);
        return (await UnwrapAsync<NodeExpandResultDto>(resp, ct))!;
    }

    public async Task<IReadOnlyList<SessionSummaryDto>> GraphAgentListSessionsAsync(
        CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"{GraphAgentBase}/sessions", ct);
        var body = await UnwrapAsync<GraphAgentSessionsReplyDto>(resp, ct);
        return body?.Sessions ?? [];
    }

    public async Task<SessionMessagesReplyDto?> GraphAgentGetSessionMessagesAsync(
        string conversationId, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync(
            $"{GraphAgentBase}/sessions/{conversationId}/messages", ct);
        return resp.StatusCode == System.Net.HttpStatusCode.NotFound
            ? null
            : await UnwrapAsync<SessionMessagesReplyDto>(resp, ct);
    }

    public async Task<bool> GraphAgentResetAsync(
        string conversationId, CancellationToken ct = default)
    {
        var resp = await _http.PostAsync(
            $"{GraphAgentBase}/reset/{conversationId}", content: null, ct);
        return resp.StatusCode switch
        {
            System.Net.HttpStatusCode.NoContent => true,
            System.Net.HttpStatusCode.NotFound => false,
            _ => throw await ToExceptionAsync(resp, ct)
        };
    }

    public async Task GraphAgentRenameAsync(
        string conversationId, string? displayName, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync(
            $"{GraphAgentBase}/sessions/{conversationId}/rename",
            new RenameSessionRequest { DisplayName = displayName }, JsonOptions, ct);
        await EnsureSuccessAsync(resp, ct);
    }

    // ══════════════════════════════════════════════════════
    // 通用助手（/api/Agents）
    // ══════════════════════════════════════════════════════

    public async Task<AgentReplyDto> AgentsChatAsync(
        string? conversationId, string message, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync($"{AgentsBase}/chat",
            new AgentChatRequest { ConversationId = conversationId, Message = message },
            JsonOptions, ct);
        return (await UnwrapAsync<AgentReplyDto>(resp, ct))!;
    }

    public async Task<IReadOnlyList<string>> AgentsListSessionsAsync(
        CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"{AgentsBase}/sessions", ct);
        var body = await UnwrapAsync<SessionsReplyDto>(resp, ct);
        return body?.Sessions ?? [];
    }

    public async Task<SessionMessagesReplyDto?> AgentsGetSessionMessagesAsync(
        string conversationId, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync(
            $"{AgentsBase}/sessions/{conversationId}/messages", ct);
        return resp.StatusCode == System.Net.HttpStatusCode.NotFound
            ? null
            : await UnwrapAsync<SessionMessagesReplyDto>(resp, ct);
    }

    public async Task<bool> AgentsResetAsync(
        string conversationId, CancellationToken ct = default)
    {
        var resp = await _http.PostAsync(
            $"{AgentsBase}/reset/{conversationId}", content: null, ct);
        return resp.StatusCode switch
        {
            System.Net.HttpStatusCode.NoContent => true,
            System.Net.HttpStatusCode.NotFound => false,
            _ => throw await ToExceptionAsync(resp, ct)
        };
    }

    // ══════════════════════════════════════════════════════
    // 工作流（/api/Workflows）
    // ══════════════════════════════════════════════════════

    public async Task<WorkflowReplyDto> RunWriterCriticAsync(
        string topic, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync($"{WorkflowsBase}/writer-critic",
            new WorkflowRunRequest { Topic = topic }, JsonOptions, ct);
        return (await UnwrapAsync<WorkflowReplyDto>(resp, ct))!;
    }

    // ══════════════════════════════════════════════════════
    // 命名生成（/api/naming）
    // ══════════════════════════════════════════════════════

    /// <summary>中文描述 → snake_case / PascalCase 英文命名（LLM 翻译 + 代码侧规则）。</summary>
    public async Task<NamingResultDto> GenerateNamingAsync(string text, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync($"{NamingBase}/generate",
            new NamingRequestDto { Text = text }, JsonOptions, ct);
        return (await UnwrapAsync<NamingResultDto>(resp, ct))!;
    }

    // ══════════════════════════════════════════════════════
    // 信封解包
    // ══════════════════════════════════════════════════════

    private async Task<T?> UnwrapAsync<T>(HttpResponseMessage resp, CancellationToken ct)
    {
        if (!resp.IsSuccessStatusCode)
            throw await ToExceptionAsync(resp, ct);

        var envelope = await resp.Content.ReadFromJsonAsync<ApiResponse<T>>(JsonOptions, ct);
        if (envelope is null)
            throw new HttpRequestException("响应体为空或格式非法");

        if (!envelope.Success)
            throw new HttpRequestException(envelope.Message ?? "请求失败");

        return envelope.Data;
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        if (resp.IsSuccessStatusCode) return;
        throw await ToExceptionAsync(resp, ct);
    }

    /// <summary>错误响应也走信封：优先提取 message，失败则回退原始 body。</summary>
    private async Task<HttpRequestException> ToExceptionAsync(
        HttpResponseMessage resp, CancellationToken ct)
    {
        var body = await resp.Content.ReadAsStringAsync(ct);
        try
        {
            var envelope = JsonSerializer.Deserialize<ApiResponse<object>>(body, JsonOptions);
            if (envelope?.Message is { Length: > 0 } msg)
                return new HttpRequestException($"{msg}（{(int)resp.StatusCode}）");
        }
        catch { /* 非信封格式，回退 */ }

        _logger.LogWarning("MafWfApi 错误 ({Status}): {Body}", (int)resp.StatusCode, body);
        return new HttpRequestException(
            $"请求失败 ({(int)resp.StatusCode}): {body[..Math.Min(300, body.Length)]}");
    }
}

/// <summary>POST /api/graph-agent/expand-node 响应（非流式一次性返回）。</summary>
public sealed record NodeExpandResultDto(
    string ConversationId,
    string AgentName,
    string Reply,
    bool Found);
