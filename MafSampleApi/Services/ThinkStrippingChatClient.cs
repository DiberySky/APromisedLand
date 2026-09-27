using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace MafSampleApi.Services;

/// <summary>
/// 装饰器：剥离上游 IChatClient 返回内容中的 &lt;think&gt;...&lt;/think&gt; 块。
///
/// 背景：
///   Qwen3 在 thinking 关闭时仍输出 &lt;think&gt;\n\n&lt;/think&gt; 空壳。
///   /no_think 只抑制推理内容，不抑制标签本身。
///
/// 实现要点：
///   - ChatResponse.Text 是懒加载 + 缓存属性。若已读过一次，
///     只改 Messages 不会刷新 Text。这里选择构造新的 ChatResponse
///     返回，避免依赖内部缓存行为。
///   - 流式（GetStreamingResponseAsync）逐块剥离。跨块边界不做缓冲，
///     对"空 think 块 + 紧跟回答"这种最常见形态足够。
/// </summary>
public sealed class ThinkStrippingChatClient(
    IChatClient inner,
    ILogger<ThinkStrippingChatClient> logger) : DelegatingChatClient(inner)
{
    // <think>...</think> + 后续空白，Singleline 让 . 跨行
    private static readonly Regex ThinkBlock = new(
        @"<think>.*?</think>\s*",
        RegexOptions.Singleline | RegexOptions.Compiled);

    // ══════════════════════════════════════════════════════════
    // 非流式
    // ══════════════════════════════════════════════════════════
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(messages, options, cancellationToken);

        // 先检查有没有需要剥离的内容，避免无谓地重建对象
        bool anyChanged = false;

        foreach (var msg in response.Messages)
        {
            foreach (var content in msg.Contents)
            {
                if (content is TextContent text
                    && !string.IsNullOrEmpty(text.Text)
                    && Strip(text.Text) != text.Text)
                {
                    anyChanged = true;
                    break;
                }
            }
            if (anyChanged) break;
        }

        if (!anyChanged)
            return response;

        // 构造剥离后的 Messages
        var newMessages = new List<ChatMessage>(response.Messages.Count);

        foreach (var msg in response.Messages)
        {
            var newContents = new List<AIContent>(msg.Contents.Count);
            bool msgChanged = false;

            foreach (var content in msg.Contents)
            {
                if (content is TextContent text
                    && !string.IsNullOrEmpty(text.Text))
                {
                    var stripped = Strip(text.Text);
                    if (stripped != text.Text)
                    {
                        msgChanged = true;
                        newContents.Add(new TextContent(stripped));
                        continue;
                    }
                }
                newContents.Add(content);
            }

            newMessages.Add(msgChanged
                ? new ChatMessage(msg.Role, newContents)
                : msg);
        }

        logger.LogDebug(
            "Stripped <think> block: {MsgCount} message(s), {OrigChars} → {NewChars} chars",
            newMessages.Count,
            response.Text?.Length ?? 0,
            string.Concat(newMessages.SelectMany(m => m.Contents.OfType<TextContent>()).Select(t => t.Text)).Length);

        // 构造新响应，保留关键元数据
        return new ChatResponse(newMessages)
        {
            ModelId       = response.ModelId,
            ResponseId    = response.ResponseId,
            CreatedAt     = response.CreatedAt,
            FinishReason  = response.FinishReason,
            Usage         = response.Usage,
        };
    }

    // ══════════════════════════════════════════════════════════
    // 流式（逐块剥离）
    // ══════════════════════════════════════════════════════════
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var update in base.GetStreamingResponseAsync(
                           messages, options, cancellationToken))
        {
            for (int i = 0; i < update.Contents.Count; i++)
            {
                if (update.Contents[i] is TextContent text
                    && !string.IsNullOrEmpty(text.Text))
                {
                    var stripped = Strip(text.Text);

                    // 整块是 think 内部或纯空白 → 丢弃
                    if (stripped.Length == 0 && text.Text.Length > 0)
                    {
                        update.Contents.RemoveAt(i);
                        i--;
                        continue;
                    }

                    if (stripped != text.Text)
                    {
                        update.Contents[i] = new TextContent(stripped);
                    }
                }
            }

            yield return update;
        }
    }

    // ══════════════════════════════════════════════════════════
    // 剥离逻辑
    // ══════════════════════════════════════════════════════════
    private static string Strip(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var result = ThinkBlock.Replace(text, string.Empty);
        // 剥完后可能残留前导换行/空格
        return result.TrimStart('\n', '\r', ' ', '\t');
    }
}