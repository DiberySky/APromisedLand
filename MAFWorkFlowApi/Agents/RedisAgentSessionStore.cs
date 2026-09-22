using System.Text;
using System.Text.Json;
using MAFWorkFlowApi.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using AgentSessionStore = Microsoft.Agents.AI.Hosting.AgentSessionStore;

namespace MAFWorkFlowApi.Agents;

/// <summary>
/// 基于 Redis (IDistributedCache) 的 AgentSessionStore 实现，
/// 同时实现 IConversationCatalog 以支持会话列举、删除与历史读取。
/// </summary>
public sealed class RedisAgentSessionStore : AgentSessionStore, IConversationCatalog
{
    private const int MaxConversationIdLength = 128;
    private const string CacheKeyPrefix = "maf:session:";
    private const string DisplayNameKeyPrefix = "maf:session-name:";

    private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan SlidingTtl = TimeSpan.FromHours(4);

    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer _multiplexer;
    private readonly ILogger<RedisAgentSessionStore> _logger;

    public RedisAgentSessionStore(
        IDistributedCache cache,
        IConnectionMultiplexer multiplexer,
        ILogger<RedisAgentSessionStore> logger)
    {
        _cache = cache;
        _multiplexer = multiplexer;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────
    // AgentSessionStore 抽象实现
    // ─────────────────────────────────────────────────────────────

    public override async ValueTask<AgentSession> GetSessionAsync(
        AIAgent agent,
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        ValidateConversationId(conversationId);
        var cacheKey = GetCacheKey(conversationId);

        _logger.LogDebug("Loading session from Redis: {ConversationId}", conversationId);

        byte[]? jsonBytes;
        try
        {
            jsonBytes = await _cache.GetAsync(cacheKey, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis 读取失败：{ConversationId}", conversationId);
            throw new InvalidOperationException(
                $"从 Redis 加载会话失败：{conversationId}", ex);
        }

        if (jsonBytes is null or { Length: 0 })
            return null!;

        try
        {
            using var document = JsonDocument.Parse(jsonBytes);
            return await agent.DeserializeSessionAsync(
                document.RootElement,
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "会话 JSON 损坏，已清理并回退为新会话：{ConversationId}",
                conversationId);
            await TryRemoveAsync(cacheKey, cancellationToken);
            return null!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "会话反序列化失败：{ConversationId}", conversationId);
            throw;
        }
    }

    public override async ValueTask SaveSessionAsync(
        AIAgent agent,
        string conversationId,
        AgentSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ValidateConversationId(conversationId);

        var cacheKey = GetCacheKey(conversationId);

        // ★ 关键：把 session 里的 functionCall / functionResult 消息剔除，
        //    只保留 user + assistant 纯文本，避免"上下文污染"。
        var sanitizedSession = await SanitizeSessionAsync(
            agent, session, cancellationToken);

        var jsonElement = await agent.SerializeSessionAsync(
            sanitizedSession, cancellationToken: cancellationToken);
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(jsonElement);

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = SessionTtl,
            SlidingExpiration = SlidingTtl,
        };

        try
        {
            await _cache.SetAsync(cacheKey, jsonBytes, options, cancellationToken);
            _logger.LogDebug(
                "Session saved successfully: {ConversationId} ({Size} bytes)",
                conversationId, jsonBytes.Length);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis 写入失败：{ConversationId}", conversationId);
            throw new InvalidOperationException(
                $"保存会话到 Redis 失败：{conversationId}", ex);
        }
    }

    /// <summary>
    /// 把 session 序列化后的 JSON 中的 functionCall / functionResult 消息剔除，
    /// 只保留 user + assistant text 消息，然后反序列化回新的 session。
    /// 这样下一轮 LLM 看到的上下文是干净的，不会被历史工具调用污染。
    /// </summary>
    private static async ValueTask<AgentSession> SanitizeSessionAsync(
        AIAgent agent,
        AgentSession originalSession,
        CancellationToken ct)
    {
        // 1. 序列化原始 session
        var json = await agent.SerializeSessionAsync(originalSession, cancellationToken: ct);

        // 2. 找到 messages 数组并过滤
        var filtered = FilterMessagesInJson(json);

        // 3. 反序列化回 session
        return await agent.DeserializeSessionAsync(filtered, cancellationToken: ct);
    }

    /// <summary>
    /// 递归查找 messages 数组，剔除含 functionCall / functionResult 的消息。
    /// 返回新的 JsonElement（原 JSON 已被改写）。
    /// </summary>
    private static JsonElement FilterMessagesInJson(JsonElement root)
    {
        // 转成可变 JSON 对象
        var jsonString = root.GetRawText();
        using var doc = JsonDocument.Parse(jsonString);

        // 使用 Utf8JsonWriter 重建 JSON，过滤 messages
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            WriteFilteredElement(writer, doc.RootElement);
        }

        ms.Position = 0;
        using var result = JsonDocument.Parse(ms);
        // 拷贝一份返回（JsonDocument 释放后元素会失效，所以先 clone）
        return result.RootElement.Clone();
    }

    /// <summary>
    /// 递归写 JSON：当遇到名为 "messages" 的数组时，过滤掉含 functionCall /
    /// functionResult 的消息；其余内容原样写出。
    /// </summary>
    private static void WriteFilteredElement(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var prop in element.EnumerateObject())
                {
                    writer.WritePropertyName(prop.Name);
                    if (string.Equals(prop.Name, "messages", StringComparison.OrdinalIgnoreCase) &&
                        prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        WriteFilteredMessages(writer, prop.Value);
                    }
                    else
                    {
                        WriteFilteredElement(writer, prop.Value);
                    }
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteFilteredElement(writer, item);
                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                element.WriteTo(writer);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
        }
    }

    /// <summary>
    /// 过滤 messages 数组：跳过含 functionCall 或 functionResult 的消息。
    /// </summary>
    private static void WriteFilteredMessages(Utf8JsonWriter writer, JsonElement messagesArray)
    {
        writer.WriteStartArray();

        foreach (var msg in messagesArray.EnumerateArray())
        {
            if (ShouldKeepMessage(msg))
                WriteFilteredElement(writer, msg);
        }

        writer.WriteEndArray();
    }

    /// <summary>
    /// 判断消息是否应保留：只保留 user 和 assistant + 纯文本的消息。
    /// 剔除：
    ///   - role=tool（工具结果）
    ///   - contents 中含 functionCall / functionResult 的消息
    /// </summary>
    private static bool ShouldKeepMessage(JsonElement msg)
    {
        if (msg.ValueKind != JsonValueKind.Object)
            return false;

        // 剔除 role=tool
        var role = ExtractRole(msg);
        if (string.Equals(role, "tool", StringComparison.OrdinalIgnoreCase))
            return false;

        // 剔除 contents 里含 functionCall / functionResult 的消息
        if (TryGetPropertyIgnoreCase(msg, "contents", out var contents) &&
            contents.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in contents.EnumerateArray())
            {
                if (c.ValueKind != JsonValueKind.Object) continue;
                var type = TryGetStringIgnoreCase(c, "$type");
                if (string.Equals(type, "functionCall", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(type, "functionResult", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }

        return true;
    }

    public override async ValueTask DeleteSessionAsync(
        AIAgent agent,
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        ValidateConversationId(conversationId);
        await TryRemoveAsync(GetCacheKey(conversationId), cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────
    // IConversationCatalog 实现
    // ─────────────────────────────────────────────────────────────

    public async ValueTask<IReadOnlyList<string>> ListConversationIdsAsync(
        CancellationToken cancellationToken = default)
    {
        var result = new List<string>();

        var server = _multiplexer.GetServers().FirstOrDefault();
        if (server is null)
        {
            _logger.LogWarning("未找到可用的 Redis 服务器连接，无法列举会话。");
            return result;
        }

        try
        {
            var pattern = $"{CacheKeyPrefix}*";
            await foreach (var key in server
                .KeysAsync(pattern: pattern, pageSize: 250)
                .WithCancellation(cancellationToken))
            {
                var raw = (string?)key;
                if (string.IsNullOrEmpty(raw)) continue;

                var id = raw.StartsWith(CacheKeyPrefix, StringComparison.Ordinal)
                    ? raw[CacheKeyPrefix.Length..]
                    : raw;

                if (!string.IsNullOrEmpty(id))
                    result.Add(id);
            }

            result.Sort(StringComparer.Ordinal);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "列举 Redis 会话失败");
            throw new InvalidOperationException("列举会话失败", ex);
        }

        return result;
    }

    public async ValueTask<bool> DeleteConversationAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        ValidateConversationId(conversationId);
        var cacheKey = GetCacheKey(conversationId);
        var nameKey = DisplayNameKeyPrefix + conversationId;   // ★

        try
        {
            var existed = await _cache.GetAsync(cacheKey, cancellationToken) is not null;
            await _cache.RemoveAsync(cacheKey, cancellationToken);
            await _cache.RemoveAsync(nameKey, cancellationToken);   // ★ 一并清除
            return existed;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除 Redis 会话失败：{ConversationId}", conversationId);
            throw new InvalidOperationException(
                $"删除会话失败：{conversationId}", ex);
        }
    }

    /// <summary>
    /// 从已持久化的会话 JSON 中提取消息列表（用于历史回放）。
    /// 
    /// MAF 实际序列化结构（已通过日志确认）：
    /// {
    ///   "stateBag": {
    ///     "InMemoryChatHistoryProvider": {
    ///       "messages": [
    ///         { "role": "user", "contents": [{"$type":"text","text":"..."}] },
    ///         { "role": "assistant", "contents": [{"$type":"functionCall","name":"...","arguments":{...}}] },
    ///         { "role": "tool", "contents": [{"$type":"functionResult","result":"...","callId":"..."}] },
    ///         { "role": "assistant", "contents": [{"$type":"text","text":"..."}] }
    ///       ]
    ///     }
    ///   }
    /// }
    /// 
    /// 策略：
    /// - 递归查找第一个名为 "messages" 的数组（不管嵌套多深）
    /// - 跳过 role=tool 的消息（工具结果）
    /// - 从 contents 里提取 $type=text 的文本
    /// - 跳过纯 functionCall 消息（无 text 内容）
    /// </summary>
    public async ValueTask<IReadOnlyList<SessionMessage>> GetSessionMessagesAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        ValidateConversationId(conversationId);
        var cacheKey = GetCacheKey(conversationId);

        byte[]? jsonBytes;
        try
        {
            jsonBytes = await _cache.GetAsync(cacheKey, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis 读取失败：{ConversationId}", conversationId);
            throw new InvalidOperationException(
                $"从 Redis 读取会话失败：{conversationId}", ex);
        }

        if (jsonBytes is null or { Length: 0 })
            return [];

        try
        {
            using var document = JsonDocument.Parse(jsonBytes);

            // ★ 递归查找 messages 数组
            var messagesElement = FindMessagesArray(document.RootElement);
            if (messagesElement is null ||
                messagesElement.Value.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning(
                    "会话 {ConvId} 中未找到 messages 数组", conversationId);
                return [];
            }

            var result = new List<SessionMessage>();

            foreach (var msg in messagesElement.Value.EnumerateArray())
            {
                // 跳过 tool 角色（工具执行结果，不属于对话历史）
                var role = ExtractRole(msg);
                if (string.Equals(role, "tool", StringComparison.OrdinalIgnoreCase))
                    continue;

                var text = ExtractMessageText(msg);
                if (string.IsNullOrWhiteSpace(text))
                    continue;   // 纯 functionCall 消息会被跳过

                // ★ 新增：跳过含 tool_call 标记的 assistant 消息（旧版本遗留的脏数据）
                if (string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase)
                    && ReplySanitizer.ContainsToolCallMarkers(text))
                {
                    _logger.LogDebug(
                        "跳过含 tool_call 标记的历史消息：{ConvId}", conversationId);
                    continue;
                }

                var author = TryGetStringIgnoreCase(msg, "authorName");

                result.Add(new SessionMessage(
                    Role: role,
                    Text: text,
                    AuthorName: author));
            }

            _logger.LogInformation(
                "会话 {ConvId} 解析出 {Count} 条用户可见消息",
                conversationId, result.Count);

            return result;
        }
        catch (OperationCanceledException) { throw; }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "会话 JSON 无法解析，返回空消息列表：{ConversationId}", conversationId);
            return [];
        }
    }
    
        // ═════════════════════════════════════════════════════════════
    // ★ 显示名管理
    // ═════════════════════════════════════════════════════════════

    public async ValueTask<string?> GetDisplayNameAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        ValidateConversationId(conversationId);
        var key = DisplayNameKeyPrefix + conversationId;

        try
        {
            var bytes = await _cache.GetAsync(key, cancellationToken);
            return bytes is null or { Length: 0 }
                ? null
                : System.Text.Encoding.UTF8.GetString(bytes);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取会话显示名失败：{ConversationId}", conversationId);
            return null;
        }
    }

    public async ValueTask SetDisplayNameAsync(
        string conversationId,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        ValidateConversationId(conversationId);
        var key = DisplayNameKeyPrefix + conversationId;

        try
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                await _cache.RemoveAsync(key, cancellationToken);
            }
            else
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(displayName.Trim());
                await _cache.SetAsync(key, bytes, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = SessionTtl,
                    SlidingExpiration = SlidingTtl,
                }, cancellationToken);
            }

            _logger.LogDebug("会话显示名已保存：{ConversationId} → {Name}",
                conversationId, displayName);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "保存会话显示名失败：{ConversationId}", conversationId);
        }
    }

    public async ValueTask<IReadOnlyDictionary<string, string>> GetAllDisplayNamesAsync(
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, string>();

        var server = _multiplexer.GetServers().FirstOrDefault();
        if (server is null) return result;

        try
        {
            var pattern = $"{DisplayNameKeyPrefix}*";
            await foreach (var key in server
                .KeysAsync(pattern: pattern, pageSize: 250)
                .WithCancellation(cancellationToken))
            {
                var raw = (string?)key;
                if (string.IsNullOrEmpty(raw)) continue;

                var id = raw.StartsWith(DisplayNameKeyPrefix, StringComparison.Ordinal)
                    ? raw[DisplayNameKeyPrefix.Length..]
                    : raw;

                if (string.IsNullOrEmpty(id)) continue;

                var bytes = await _cache.GetAsync(raw, cancellationToken);
                if (bytes is { Length: > 0 })
                {
                    result[id] = System.Text.Encoding.UTF8.GetString(bytes);
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "列举会话显示名失败");
        }

        return result;
    }

    // ─────────────────────────────────────────────────────────────
    // 私有辅助：JSON 提取
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 递归查找 JSON 树中第一个名为 "messages" 的数组。
    /// 无论它嵌套多深都能找到。
    /// </summary>
    private static JsonElement? FindMessagesArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object &&
            element.ValueKind != JsonValueKind.Array)
            return null;

        // 优先匹配当前层
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (string.Equals(prop.Name, "messages", StringComparison.OrdinalIgnoreCase) &&
                    prop.Value.ValueKind == JsonValueKind.Array)
                {
                    return prop.Value;
                }
            }

            // 递归子对象
            foreach (var prop in element.EnumerateObject())
            {
                var found = FindMessagesArray(prop.Value);
                if (found is not null)
                    return found;
            }
        }

        // 递归数组元素
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var found = FindMessagesArray(item);
                if (found is not null)
                    return found;
            }
        }

        return null;
    }

    /// <summary>
    /// 提取消息的文本内容。
    /// 优先从 contents 数组里找 $type=text 的内容；
    /// 兼容顶层 text 字段的旧格式。
    /// </summary>
    private static string? ExtractMessageText(JsonElement msg)
    {
        // 1. 兼容旧格式：顶层 text 字段
        var directText = TryGetStringIgnoreCase(msg, "text");
        if (!string.IsNullOrWhiteSpace(directText))
            return directText;

        // 2. MAF 实际格式：contents[] 里找 $type=text
        if (TryGetPropertyIgnoreCase(msg, "contents", out var contentsEl) &&
            contentsEl.ValueKind == JsonValueKind.Array)
        {
            var sb = new StringBuilder();

            foreach (var c in contentsEl.EnumerateArray())
            {
                if (c.ValueKind == JsonValueKind.String)
                {
                    sb.Append(c.GetString());
                    continue;
                }

                if (c.ValueKind != JsonValueKind.Object)
                    continue;

                // 只提取 $type=text 的内容
                var type = TryGetStringIgnoreCase(c, "$type");
                if (!string.Equals(type, "text", StringComparison.OrdinalIgnoreCase))
                    continue;

                var t = TryGetStringIgnoreCase(c, "text");
                if (!string.IsNullOrWhiteSpace(t))
                    sb.Append(t);
            }

            if (sb.Length > 0)
                return sb.ToString();
        }

        // 3. 兼容 content 单数字段
        if (TryGetPropertyIgnoreCase(msg, "content", out var contentEl))
        {
            if (contentEl.ValueKind == JsonValueKind.String)
                return contentEl.GetString();

            var t = TryGetStringIgnoreCase(contentEl, "text");
            if (!string.IsNullOrWhiteSpace(t))
                return t;
        }

        return null;
    }

    /// <summary>提取消息的 role，兼容字符串/对象两种形式。</summary>
    private static string ExtractRole(JsonElement msg)
    {
        if (!TryGetPropertyIgnoreCase(msg, "role", out var roleEl))
            return "unknown";

        if (roleEl.ValueKind == JsonValueKind.String)
            return roleEl.GetString() ?? "unknown";

        if (roleEl.ValueKind == JsonValueKind.Object)
        {
            var label = TryGetStringIgnoreCase(roleEl, "label")
                ?? TryGetStringIgnoreCase(roleEl, "name")
                ?? TryGetStringIgnoreCase(roleEl, "value");
            if (!string.IsNullOrWhiteSpace(label))
                return label;
        }

        return "unknown";
    }

    private async Task TryRemoveAsync(string cacheKey, CancellationToken ct)
    {
        try
        {
            await _cache.RemoveAsync(cacheKey, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "清理 Redis 缓存失败：{CacheKey}", cacheKey);
        }
    }

    private static bool TryGetPropertyIgnoreCase(
        JsonElement element, string name, out JsonElement value)
    {
        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private static string? TryGetStringIgnoreCase(JsonElement element, string name)
    {
        if (!TryGetPropertyIgnoreCase(element, name, out var prop))
            return null;
        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : null;
    }

    private static void ValidateConversationId(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            throw new ArgumentException("会话 ID 不能为空。", nameof(conversationId));

        if (conversationId.Length > MaxConversationIdLength)
            throw new ArgumentException(
                $"会话 ID 长度超过限制（最多 {MaxConversationIdLength} 字符）。",
                nameof(conversationId));
    }

    private static string GetCacheKey(string conversationId)
        => $"{CacheKeyPrefix}{conversationId}";
}