using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using TreeGraph.Shared.MafWfSky.Contracts;

namespace TreeGraph.Blazor.Shared.MafWfSky.Services;

/// <summary>
/// MafWfApi SSE 流读取。后端 SSE 不经信封包装，data 载荷即为原始 DTO。
/// </summary>
public class MafWfSseReader(HttpClient http, MafWfSkyOptions options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    /// <summary>Graph Agent 流式对话：逐块返回 StreamChunkDto。</summary>
    public async IAsyncEnumerable<StreamChunkDto> GraphAgentStreamAsync(
        string? conversationId, string message,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var url = $"{options.GraphAgentPath.TrimEnd('/')}/chat/stream";
        var body = new AgentChatRequest { ConversationId = conversationId, Message = message };

        await foreach (var chunk in ReadSseAsync<StreamChunkDto>(url, body, ct))
            yield return chunk;
    }

    /// <summary>
    /// 展开节点合成回答：复用 /chat/stream，通过 __expand_node__:{inodeId}:{hostType} 前缀触发。
    /// </summary>
    public IAsyncEnumerable<StreamChunkDto> ExpandNodeStreamAsync(
        string? conversationId, string inodeId, string hostType,
        CancellationToken ct = default)
        => GraphAgentStreamAsync(
            conversationId,
            $"__expand_node__:{inodeId}:{hostType}",
            ct);

    /// <summary>Writer-Critic 流式：逐事件返回 WorkflowStreamEventDto，遇 event:done 结束。</summary>
    public async IAsyncEnumerable<WorkflowStreamEventDto> WriterCriticStreamAsync(
        string topic,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var url = $"{options.WorkflowsPath.TrimEnd('/')}/writer-critic/stream";
        var body = new WorkflowRunRequest { Topic = topic };

        await foreach (var evt in ReadSseAsync<WorkflowStreamEventDto>(url, body, ct))
            yield return evt;
    }

    // ─── 通用 SSE 逐行解析 ──────────────────────────────────

    private async IAsyncEnumerable<T> ReadSseAsync<T>(
        string url, object body,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        req.Headers.Accept.ParseAdd("text/event-stream");

        using var resp = await http.SendAsync(
            req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) yield break;               // 流结束
            if (line.StartsWith("event: done", StringComparison.Ordinal)) yield break;
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;

            var payload = line["data:".Length..].Trim();
            if (payload.Length == 0 || payload == "{}") continue;

            T? item;
            try { item = JsonSerializer.Deserialize<T>(payload, JsonOptions); }
            catch (JsonException) { continue; }

            if (item is not null) yield return item;
        }
    }
}
