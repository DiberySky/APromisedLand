using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace MafSampleApi.Services;

/// <summary>
/// 直接调用 vLLM 的自定义 IChatClient。
/// 不依赖 OpenAI SDK，直接用 HttpClient 调用 vLLM /v1/chat/completions。
/// 每个请求体注入 chat_template_kwargs.enable_thinking=false。
/// </summary>
internal sealed class VllmChatClient : IChatClient
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly string _apiKey;
    private readonly ILogger _logger;

    public VllmChatClient(HttpClient http, string model, string apiKey, ILogger logger)
    {
        _http = http;
        _model = model;
        _apiKey = apiKey;
        _logger = logger;
    }

    // ─────────────────────────────────────────────
    //  非流式
    // ─────────────────────────────────────────────
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var payload = BuildPayload(messages, options, stream: false);
        var json = payload.ToJsonString();

        using var req = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        SetAuth(req);

        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, cancellationToken);
        var respJson = await resp.Content.ReadAsStringAsync(cancellationToken);

        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"vLLM {resp.StatusCode}: {respJson[..Math.Min(500, respJson.Length)]}");

        var doc = JsonNode.Parse(respJson) ?? throw new InvalidOperationException("vLLM 返回空");
        var choice = doc["choices"]?[0];
        var messageNode = choice?["message"];
        var message = ParseMessage(messageNode);
        var finishReason = ParseFinishReason(choice?["finish_reason"]?.ToString());

        return new ChatResponse(new[] { message })
        {
            ModelId = _model,
            ResponseId = doc["id"]?.ToString(),
            FinishReason = finishReason,
        };
    }

    // ─────────────────────────────────────────────
    //  流式
    // ─────────────────────────────────────────────
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var payload = BuildPayload(messages, options, stream: true);
        var json = payload.ToJsonString();

        using var req = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        SetAuth(req);

        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null) break;
            if (!line.StartsWith("data: ")) continue;
            var data = line.Substring(6);
            if (data == "[DONE]") break;

            var chunk = JsonNode.Parse(data);
            if (chunk?["choices"]?[0] is not JsonObject choiceObj) continue;

            var delta = choiceObj["delta"];
            var update = new ChatResponseUpdate();

            if (delta?["content"]?.ToString() is { Length: > 0 } text)
                update.Contents.Add(new TextContent(text));

            // tool_calls
            if (delta?["tool_calls"] is JsonArray toolCalls)
            {
                foreach (var tc in toolCalls)
                {
                    if (tc is not JsonObject tco) continue;
                    var id = tco["id"]?.ToString() ?? "";
                    var fn = tco["function"];
                    var name = fn?["name"]?.ToString() ?? "";
                    var args = fn?["arguments"]?.ToString() ?? "{}";

                    if (!string.IsNullOrEmpty(name))
                        update.Contents.Add(new FunctionCallContent(id, name, ParseArgs(args)));
                }
            }

            var finish = ParseFinishReason(choiceObj["finish_reason"]?.ToString());
            if (finish is not null)
                update.FinishReason = finish;

            if (update.Contents.Count > 0 || update.FinishReason is not null)
                yield return update;
        }
    }

    // ─────────────────────────────────────────────
    //  构建请求体
    // ─────────────────────────────────────────────
    private JsonObject BuildPayload(IEnumerable<ChatMessage> messages, ChatOptions? options, bool stream)
    {
        var payload = new JsonObject
        {
            ["model"] = _model,
            ["stream"] = stream,
            // ★ 核心：注入 enable_thinking=false
            ["chat_template_kwargs"] = new JsonObject { ["enable_thinking"] = false },
        };

        // messages
        var msgArray = new JsonArray();
        foreach (var msg in messages)
        {
            var msgObj = new JsonObject { ["role"] = msg.Role.ToString().ToLowerInvariant() };

            var textParts = new StringBuilder();
            var toolCalls = new JsonArray();
            var hasToolCalls = false;
            var isToolResult = false;

            foreach (var content in msg.Contents)
            {
                switch (content)
                {
                    case TextContent tc:
                        textParts.Append(tc.Text);
                        break;

                    case FunctionCallContent fc:
                        toolCalls.Add(new JsonObject
                        {
                            ["id"] = fc.CallId ?? "",
                            ["type"] = "function",
                            ["function"] = new JsonObject
                            {
                                ["name"] = fc.Name ?? "",
                                ["arguments"] = fc.Arguments is null
                                    ? "{}"
                                    : JsonSerializer.Serialize(fc.Arguments),
                            }
                        });
                        hasToolCalls = true;
                        break;

                    case FunctionResultContent fr:
                        msgObj["role"] = "tool";
                        msgObj["tool_call_id"] = fr.CallId ?? "";
                        msgObj["content"] = fr.Result?.ToString() ?? "";
                        isToolResult = true;
                        break;
                }
            }

            if (!isToolResult)
            {
                if (hasToolCalls)
                {
                    msgObj["tool_calls"] = toolCalls;
                    msgObj["content"] = textParts.ToString();
                }
                else if (textParts.Length > 0)
                {
                    msgObj["content"] = textParts.ToString();
                }
            }

            msgArray.Add(msgObj);
        }
        payload["messages"] = msgArray;

        // 采样参数
        if (options is not null)
        {
            if (options.Temperature is { } t) payload["temperature"] = t;
            if (options.TopP is { } p) payload["top_p"] = p;
            if (options.Seed is { } s) payload["seed"] = s;
            if (options.MaxOutputTokens is { } max) payload["max_tokens"] = max;
            if (options.StopSequences is { Count: > 0 } stop)
                payload["stop"] = new JsonArray(stop.Select(s => (JsonNode)s).ToArray());

            // tools
            if (options.Tools is { Count: > 0 } tools)
            {
                var toolsArray = new JsonArray();
                foreach (var tool in tools)
                {
                    if (tool is AIFunction fn)
                    {
                        var paramsJson = fn.JsonSchema.ValueKind == JsonValueKind.Undefined
                            ? "{}"
                            : fn.JsonSchema.GetRawText();
                        toolsArray.Add(new JsonObject
                        {
                            ["type"] = "function",
                            ["function"] = new JsonObject
                            {
                                ["name"] = fn.Name,
                                ["description"] = fn.Description ?? "",
                                ["parameters"] = JsonNode.Parse(paramsJson) ?? new JsonObject(),
                            }
                        });
                    }
                    else
                    {
                        toolsArray.Add(new JsonObject
                        {
                            ["type"] = "function",
                            ["function"] = new JsonObject
                            {
                                ["name"] = tool.Name,
                                ["description"] = "",
                                ["parameters"] = new JsonObject { ["type"] = "object" },
                            }
                        });
                    }
                }
                payload["tools"] = toolsArray;
            }
        }

        return payload;
    }

    // ─────────────────────────────────────────────
    //  解析响应消息
    // ─────────────────────────────────────────────
    private static ChatMessage ParseMessage(JsonNode? message)
    {
        var role = message?["role"]?.ToString() ?? "assistant";
        var content = message?["content"]?.ToString() ?? "";
        var chatRole = new ChatRole(role);

        var contents = new List<AIContent>();

        if (content.Length > 0)
            contents.Add(new TextContent(content));

        if (message?["tool_calls"] is JsonArray toolCalls)
        {
            foreach (var tc in toolCalls)
            {
                if (tc is not JsonObject tco) continue;
                var id = tco["id"]?.ToString() ?? "";
                var fn = tco["function"];
                var name = fn?["name"]?.ToString() ?? "";
                var args = fn?["arguments"]?.ToString() ?? "{}";
                contents.Add(new FunctionCallContent(id, name, ParseArgs(args)));
            }
        }

        return new ChatMessage(chatRole, contents);
    }

    /// <summary>解析工具参数 JSON 为 Dictionary</summary>
    private static IDictionary<string, object?>? ParseArgs(string args)
    {
        if (string.IsNullOrWhiteSpace(args)) return null;
        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(args);
            return dict;
        }
        catch { return null; }
    }

    private static ChatFinishReason? ParseFinishReason(string? reason) => reason switch
    {
        "stop" => ChatFinishReason.Stop,
        "length" => ChatFinishReason.Length,
        "tool_calls" => ChatFinishReason.ToolCalls,
        "content_filter" => ChatFinishReason.ContentFilter,
        _ => null,
    };

    private void SetAuth(HttpRequestMessage req)
    {
        if (!string.IsNullOrEmpty(_apiKey))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
    }

    public object? GetService(Type serviceType, object? key = null) => null;

    public void Dispose() => _http.Dispose();
}
