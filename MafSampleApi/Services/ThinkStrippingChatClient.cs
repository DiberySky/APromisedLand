using System.Runtime.CompilerServices;
using System.Text;
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
///   - 非流式：整段正则剥离，构造新的 ChatResponse。
///   - 流式：用状态机跨 chunk 剥离。上游（vLLM）会把
///     &lt;think&gt; 和 &lt;/think&gt; 拆成独立 token 推送，单 chunk 正则匹配不到，
///     因此必须缓冲尾部，识别"半个标签"。
/// </summary>
public sealed class ThinkStrippingChatClient(
    IChatClient inner,
    ILogger<ThinkStrippingChatClient> logger) : DelegatingChatClient(inner)
{
    // <think>...</think> + 后续空白，Singleline 让 . 跨行
    private static readonly Regex ThinkBlock = new(
        @"<think>.*?</think>\s*",
        RegexOptions.Singleline | RegexOptions.Compiled);

    // ★ 给 FunctionResultContent 追加 /no_think，确保 tool result 轮次也抑制 thinking
    //   Qwen3 的 /no_think 是 per-message 指令，只对 user 消息生效。
    //   tool result 消息没有 /no_think → 模型恢复完整 thinking（每次 60-80s）。
    //   在此处拦截 messages，给工具结果追加 /no_think 后缀。
    private List<ChatMessage> AppendNoThinkToToolResults(IEnumerable<ChatMessage> messages)
    {
        var list = messages is IReadOnlyList<ChatMessage> rl
            ? new List<ChatMessage>(rl.Count) { }
            : new List<ChatMessage>();

        int toolResultCount = 0;

        foreach (var msg in messages)
        {
            bool hasFuncResult = false;
            var newContents = new List<AIContent>(msg.Contents.Count);

            foreach (var content in msg.Contents)
            {
                if (content is FunctionResultContent fr)
                {
                    toolResultCount++;
                    var resultStr = fr.Result?.ToString() ?? "";
                    logger.LogInformation(
                        "FunctionResultContent: callId={Id}, resultType={Type}, resultLen={Len}, result=[{R}]",
                        fr.CallId, fr.Result?.GetType().Name, resultStr.Length, resultStr);
                    DiagWrite($"FunctionResultContent: callId={fr.CallId}, type={fr.Result?.GetType().Name}, len={resultStr.Length}, result=[{resultStr}]");

                    if (!resultStr.Contains("/no_think", StringComparison.OrdinalIgnoreCase))
                    {
                        hasFuncResult = true;
                        newContents.Add(new FunctionResultContent(
                            fr.CallId, resultStr + " /no_think"));
                    }
                    else
                    {
                        newContents.Add(content);
                    }
                }
                else
                {
                    newContents.Add(content);
                }
            }

            list.Add(hasFuncResult
                ? new ChatMessage(msg.Role, newContents) { RawRepresentation = msg.RawRepresentation }
                : msg);
        }

        if (toolResultCount > 0)
        {
            logger.LogInformation(
                "AppendNoThinkToToolResults: found {Count} tool result(s)", toolResultCount);
            DiagWrite($"AppendNoThinkToToolResults: found {toolResultCount} tool result(s)");
        }

        return list;
    }

    // ══════════════════════════════════════════════════════════
    // 非流式
    // ══════════════════════════════════════════════════════════
    // ★ 诊断：写到运行目录确保可写
    private static readonly string DiagPath =
        System.IO.Path.Combine(AppContext.BaseDirectory, "diag.log");

    private static void DiagWrite(string msg)
    {
        try { System.IO.File.AppendAllText(DiagPath, $"{DateTime.Now:HH:mm:ss} {msg}\n"); }
        catch { /* ignore */ }
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        DiagWrite($"GetResponseAsync called, msgCount={messages.Count()}");
        var response = await base.GetResponseAsync(
            AppendNoThinkToToolResults(messages), options, cancellationToken);

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
            "Stripped <think> block (non-stream): {MsgCount} message(s)",
            newMessages.Count);

        return new ChatResponse(newMessages)
        {
            ModelId      = response.ModelId,
            ResponseId   = response.ResponseId,
            CreatedAt    = response.CreatedAt,
            FinishReason = response.FinishReason,
            Usage        = response.Usage,
        };
    }

    // ══════════════════════════════════════════════════════════
    // 流式（跨 chunk 状态机剥离）
    // ══════════════════════════════════════════════════════════
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // ★ 每个流一个独立 stripper，避免多次调用互相污染
        var stripper = new ThinkStreamStripper();

        DiagWrite($"GetStreamingResponseAsync called, msgCount={messages.Count()}");

        await foreach (var update in base.GetStreamingResponseAsync(
                           AppendNoThinkToToolResults(messages), options, cancellationToken))
        {
            for (int i = 0; i < update.Contents.Count; i++)
            {
                if (update.Contents[i] is TextContent text
                    && !string.IsNullOrEmpty(text.Text))
                {
                    var stripped = stripper.Process(text.Text);

                    // 整块被吞（缓冲中或 think 内）→ 丢弃该 content
                    if (stripped is null)
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

            // 内容已被清空且没有 finish 信息 → 整帧丢弃，不产生空 delta
            if (update.Contents.Count == 0 && update.FinishReason is null)
                continue;

            yield return update;
        }

        // 流结束时冲掉缓冲区（若仍在 think 中则整段丢弃）
        var tail = stripper.Flush();
        if (!string.IsNullOrEmpty(tail))
        {
            yield return new ChatResponseUpdate(
                ChatRole.Assistant,
                new List<AIContent> { new TextContent(tail) });
        }
    }

    // ══════════════════════════════════════════════════════════
    // 非流式剥离
    // ══════════════════════════════════════════════════════════
    private static string Strip(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var result = ThinkBlock.Replace(text, string.Empty);
        return result.TrimStart('\n', '\r', ' ', '\t');
    }

    // ══════════════════════════════════════════════════════════
    // 跨 chunk 状态机
    // ══════════════════════════════════════════════════════════
    /// <summary>
    /// 逐 chunk 剥离 &lt;think&gt;...&lt;/think&gt;。
    /// 维护一个缓冲区：不完整的标签前缀保留到下一次 Process。
    /// </summary>
    private sealed class ThinkStreamStripper
    {
        private const string OpenTag  = "<think>";
        private const string CloseTag = "</think>";
        private static readonly int MaxTagLen = Math.Max(OpenTag.Length, CloseTag.Length);

        private readonly StringBuilder _buffer = new();
        private bool _inThink;

        // ★ 跨 chunk：刚关闭 think，下一个 chunk 的前导空白要继续吞
        private bool _skipLeadingWhitespace;

        public string? Process(string chunk)
        {
            if (string.IsNullOrEmpty(chunk)) return null;

            // ★ 先处理跨 chunk 的前导空白
            if (_skipLeadingWhitespace)
            {
                int i = 0;
                while (i < chunk.Length &&
                       (chunk[i] == '\n' || chunk[i] == '\r' ||
                        chunk[i] == ' '  || chunk[i] == '\t'))
                    i++;

                if (i == chunk.Length)
                    return null;  // 整个 chunk 都是空白，丢弃，flag 保留

                _skipLeadingWhitespace = false;
                chunk = chunk[i..];
            }

            _buffer.Append(chunk);

            var sb = new StringBuilder();
            bool progress = true;

            while (progress)
            {
                progress = false;
                var s = _buffer.ToString();

                if (!_inThink)
                {
                    int idx = s.IndexOf(OpenTag, StringComparison.Ordinal);
                    if (idx >= 0)
                    {
                        if (idx > 0) sb.Append(s, 0, idx);
                        _buffer.Remove(0, idx + OpenTag.Length);
                        _inThink = true;
                        progress = true;
                    }
                    else
                    {
                        int safeLen = FindSafeFlushLength(s, OpenTag);
                        if (safeLen > 0)
                        {
                            sb.Append(s, 0, safeLen);
                            _buffer.Remove(0, safeLen);
                        }
                        break;
                    }
                }
                else
                {
                    int idx = s.IndexOf(CloseTag, StringComparison.Ordinal);
                    if (idx >= 0)
                    {
                        _buffer.Remove(0, idx + CloseTag.Length);
                        _inThink = false;
                        _skipLeadingWhitespace = true;  // ★ 关键：交给下一个 chunk 继续吞

                        // 同 buffer 内能吞的空白先吞掉
                        while (_buffer.Length > 0 &&
                               (_buffer[0] == '\n' || _buffer[0] == '\r' ||
                                _buffer[0] == ' '  || _buffer[0] == '\t'))
                        {
                            _buffer.Remove(0, 1);
                        }

                        // 如果 buffer 里还有内容 → 说明空白吞完了，取消 flag
                        if (_buffer.Length > 0)
                            _skipLeadingWhitespace = false;

                        progress = true;
                    }
                    else
                    {
                        int safeLen = FindSafeFlushLength(s, CloseTag);
                        if (safeLen > 0)
                            _buffer.Remove(0, safeLen);
                        break;
                    }
                }
            }

            return sb.Length > 0 ? sb.ToString() : null;
        }

        public string? Flush()
        {
            if (_inThink)
            {
                _buffer.Clear();
                return null;
            }
            if (_buffer.Length == 0) return null;

            var s = _buffer.ToString();
            _buffer.Clear();
            return s;
        }

        private static int FindSafeFlushLength(string s, string tag)
        {
            int maxCheck = Math.Min(tag.Length - 1, s.Length);
            for (int len = maxCheck; len >= 1; len--)
            {
                var tail = s.AsSpan(s.Length - len);
                if (tag.AsSpan(0, len).SequenceEqual(tail))
                    return s.Length - len;
            }
            return s.Length;
        }
    }
}