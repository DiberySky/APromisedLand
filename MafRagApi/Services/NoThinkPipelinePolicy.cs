using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MafRagApi.Services;

/// <summary>
/// 在 HTTP 请求体中注入 chat_template_kwargs.enable_thinking=false。
/// 用 JsonNode 可变模型 + byte[] 创建 BinaryContent，避免 Stream 生命周期问题。
/// </summary>
internal sealed class NoThinkPipelinePolicy : PipelinePolicy
{
    private const string ChatTemplateKwargs = "chat_template_kwargs";

    public override async ValueTask ProcessAsync(
        PipelineMessage message,
        IReadOnlyList<PipelinePolicy> pipeline,
        int currentIndex)
    {
        var diagPath = System.IO.Path.Combine(AppContext.BaseDirectory, "diag.log");
        try { System.IO.File.AppendAllText(diagPath,
            $"{DateTime.Now:HH:mm:ss} NoThinkPolicy.ENTER: method={message.Request?.Method}, hasContent={message.Request?.Content is not null}\n"); }
        catch (Exception ex) { try { System.IO.File.AppendAllText(diagPath, $"{DateTime.Now:HH:mm:ss} NoThinkPolicy.ENTER LOG FAILED: {ex.Message}\n"); } catch { } }

        if (message.Request?.Content is { } content
            && message.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            // 读取原始 body
            using var ms = new MemoryStream();
            await content.WriteToAsync(ms, message.CancellationToken);
            ms.Position = 0;

            if (ms.Length > 0)
            {
                var origJson = Encoding.UTF8.GetString(ms.ToArray());

                // 用 JsonNode 解析（可变模型）
                var node = JsonNode.Parse(origJson);
                if (node is JsonObject root)
                {
                    // 注入 chat_template_kwargs.enable_thinking=false
                    if (!root.TryGetPropertyValue(ChatTemplateKwargs, out var kwargs))
                    {
                        root[ChatTemplateKwargs] = new JsonObject();
                    }
                    kwargs = root[ChatTemplateKwargs];
                    if (kwargs is JsonObject kwObj)
                    {
                        kwObj["enable_thinking"] = false;
                    }

                    // 序列化为 byte[]，写入 MemoryStream（不 dispose，BinaryContent 持有引用）
                    var modifiedBytes = Encoding.UTF8.GetBytes(root.ToJsonString());
                    var outStream = new MemoryStream(modifiedBytes);
                    message.Request.Content = BinaryContent.Create(outStream);

                    // 日志：前 300 字符
                    var modJson = Encoding.UTF8.GetString(modifiedBytes);
                    var preview = modJson.Length > 300 ? modJson[..300] + "..." : modJson;
                    try { System.IO.File.AppendAllText(diagPath,
                        $"{DateTime.Now:HH:mm:ss} NoThinkPolicy: body modified, len={modifiedBytes.Length}, has_kwargs={modJson.Contains(ChatTemplateKwargs)}, has_enable_thinking={modJson.Contains("enable_thinking")}\n  preview: {preview}\n"); }
                    catch { }
                }
            }
        }

        await ProcessNextAsync(message, pipeline, currentIndex);
    }

    public override void Process(
        PipelineMessage message,
        IReadOnlyList<PipelinePolicy> pipeline,
        int currentIndex)
        => ProcessAsync(message, pipeline, currentIndex).GetAwaiter().GetResult();
}
