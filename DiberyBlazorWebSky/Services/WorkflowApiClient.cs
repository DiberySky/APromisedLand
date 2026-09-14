using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DiberyBlazorWebSky.Models;

namespace DiberyBlazorWebSky.Services;

/// <summary>
///     通过 Aspire 服务发现调用 MAFWorkFlowApi 的工作流接口。
///     与后端 WorkflowsController 的对应关系：
///     POST /api/workflows/writer-critic          → RunWriterCriticAsync
///     POST /api/workflows/writer-critic/stream   → RunWriterCriticStreamAsync
/// </summary>
public class WorkflowApiClient(HttpClient http, ILogger<WorkflowApiClient> logger)
{
    private const string WriterCriticEndpoint = "/api/workflows/writer-critic";
    private const string WriterCriticStreamEndpoint = "/api/workflows/writer-critic/stream";

    private static readonly JsonSerializerOptions SseJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>非流式：一次性返回完整工作流结果。</summary>
    public async Task<WorkflowReply?> RunWriterCriticAsync(
        string topic,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Running writer-critic workflow, topic = {Topic}", topic);

        var response = await http.PostAsJsonAsync(
            WriterCriticEndpoint,
            new WorkflowRunRequest { Topic = topic },
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content
            .ReadFromJsonAsync<WorkflowReply>(cancellationToken);
    }

    /// <summary>
    ///     流式：逐块返回增量文本事件。
    ///     使用 HTTP 长连接 + SSE 格式（每行 "data: {json}"）。
    /// </summary>
    public async IAsyncEnumerable<WorkflowStreamEvent> RunWriterCriticStreamAsync(
        string topic,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Streaming writer-critic workflow, topic = {Topic}", topic);

        using var request = new HttpRequestMessage(
            HttpMethod.Post, WriterCriticStreamEndpoint)
        {
            Content = JsonContent.Create(new WorkflowRunRequest { Topic = topic })
        };

        using var response = await http.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        await using var stream = await response.Content
            .ReadAsStreamAsync(cancellationToken);

        using var reader = new StreamReader(stream);

        string? currentEventName = null;

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null) break;
            if (line.Length == 0)
            {
                // 空行表示一个事件块结束
                currentEventName = null;
                continue;
            }

            // 事件名：event: xxx
            if (line.StartsWith("event: ", StringComparison.Ordinal))
            {
                currentEventName = line["event: ".Length..].Trim();
                continue;
            }

            // 数据行：data: {json}
            if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                // "done" 事件无载荷，直接结束
                if (string.Equals(currentEventName, "done", StringComparison.OrdinalIgnoreCase))
                    yield break;

                var json = line["data: ".Length..];

                // "error" 事件抛出异常
                if (string.Equals(currentEventName, "error", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"工作流流式执行失败：{json}");

                WorkflowStreamEvent? evt;
                try
                {
                    evt = JsonSerializer.Deserialize<WorkflowStreamEvent>(
                        json, SseJsonOptions);
                }
                catch (JsonException ex)
                {
                    logger.LogWarning(ex,
                        "无法解析 SSE 事件：{Payload}", json);
                    continue;
                }

                if (evt is not null)
                    yield return evt;
            }
        }
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
            _ => $"API 返回 {(int)response.StatusCode} {response.ReasonPhrase}"
        };

        if (!string.IsNullOrWhiteSpace(body))
            message += $"：{body}";

        throw new HttpRequestException(message, null, response.StatusCode);
    }
}