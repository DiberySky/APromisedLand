using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MafSampleApi.Services;

/// <summary>
/// DelegatingHandler：在 HTTP 请求体中注入 chat_template_kwargs.enable_thinking=false。
/// 在传输层直接修改请求 body，绕过 OpenAI SDK 的 IChatClient 适配器不调用 pipeline 的问题。
/// </summary>
internal sealed class NoThinkHandler : DelegatingHandler
{
    private const string ChatTemplateKwargs = "chat_template_kwargs";

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var diagPath = System.IO.Path.Combine(AppContext.BaseDirectory, "diag.log");
        try { System.IO.File.AppendAllText(diagPath,
            $"{DateTime.Now:HH:mm:ss} NoThinkHandler.ENTER: {request.Method} {request.RequestUri}\n"); }
        catch { }

        if (request.Content is { } origContent
            && request.Method == HttpMethod.Post)
        {
            var bodyBytes = await origContent.ReadAsByteArrayAsync(cancellationToken);
            if (bodyBytes.Length > 0)
            {
                var origJson = Encoding.UTF8.GetString(bodyBytes);
                var node = JsonNode.Parse(origJson);
                if (node is JsonObject root)
                {
                    // 注入 chat_template_kwargs.enable_thinking=false
                    if (!root.TryGetPropertyValue(ChatTemplateKwargs, out _))
                        root[ChatTemplateKwargs] = new JsonObject();
                    if (root[ChatTemplateKwargs] is JsonObject kwObj)
                        kwObj["enable_thinking"] = false;

                    var modifiedJson = root.ToJsonString();
                    request.Content = new StringContent(modifiedJson, Encoding.UTF8, "application/json");

                    // 日志：验证注入
                    var preview = modifiedJson.Length > 300 ? modifiedJson[..300] + "..." : modifiedJson;
                    try { System.IO.File.AppendAllText(diagPath,
                        $"{DateTime.Now:HH:mm:ss} NoThinkHandler: injected, has_kwargs={modifiedJson.Contains(ChatTemplateKwargs)}, has_enable_thinking={modifiedJson.Contains("enable_thinking")}\n  preview: {preview}\n"); }
                    catch { }
                }
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
