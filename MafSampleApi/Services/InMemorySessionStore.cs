using System.Collections.Concurrent;
using MafSampleApi.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Options;

namespace MafSampleApi.Services;

/// <summary>
/// 进程内会话存储。
///
/// 特性：
/// - 线程安全（ConcurrentDictionary + 单飞创建）
/// - LRU 容量控制（超过 MaxSessions 时淘汰最久未访问的会话）
/// - ListIds 按最近访问时间倒序返回，前端体验稳定
/// - 支持指定会话被移除后的资源释放（Dispose Agent / Session）
///
/// ⚠️ 单实例适用；多副本部署需换成 Redis 等分布式实现。
/// </summary>
public sealed class InMemorySessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<string, SessionEntry> _sessions = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _createGate = new(1, 1);
    private readonly AgentOptions _options;
    private readonly ILogger<InMemorySessionStore> _logger;

    public InMemorySessionStore(
        IOptions<AgentOptions> options,
        ILogger<InMemorySessionStore> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    // ─── 读 / 取或创建 ─────────────────────────────────────────
    public async Task<SessionEntry> GetOrCreateAsync(
        string sessionId,
        string model,
        Func<string, AIAgent> agentFactory,
        CancellationToken ct = default)
    {
        // 快路径：已存在，只更新访问时间
        if (_sessions.TryGetValue(sessionId, out var existing))
        {
            existing.LastAccessUtc = DateTimeOffset.UtcNow;
            return existing;
        }

        // 慢路径：加锁保证同一 sessionId 只创建一次
        await _createGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // 双检：等待锁期间可能已被别的请求创建
            if (_sessions.TryGetValue(sessionId, out existing))
            {
                existing.LastAccessUtc = DateTimeOffset.UtcNow;
                return existing;
            }

            // 容量检查与淘汰
            EnforceCapacity();

            // 创建 Agent 与 Session
            var agent = agentFactory(model);
            var session = await agent.CreateSessionAsync(ct).ConfigureAwait(false);

            var entry = new SessionEntry
            {
                SessionId = sessionId,
                Model = model,
                Agent = agent,
                Session = session,
                LastAccessUtc = DateTimeOffset.UtcNow,
            };

            _sessions[sessionId] = entry;

            _logger.LogInformation(
                "Session created: id={SessionId}, model={Model}, total={Count}",
                sessionId, model, _sessions.Count);

            return entry;
        }
        finally
        {
            _createGate.Release();
        }
    }

    // ─── 列出 / 删除 / 清空 ───────────────────────────────────
    /// <summary>按最近访问时间倒序返回所有会话 ID。</summary>
    public IReadOnlyList<string> ListIds()
        => _sessions
            .OrderByDescending(kv => kv.Value.LastAccessUtc)
            .Select(kv => kv.Key)
            .ToArray();

    public bool TryRemove(string sessionId)
    {
        if (!_sessions.TryRemove(sessionId, out var removed))
            return false;

        DisposeEntry(removed, "explicit");
        _logger.LogInformation(
            "Session removed: id={SessionId}, remaining={Count}",
            sessionId, _sessions.Count);
        return true;
    }

    public void Clear()
    {
        var count = _sessions.Count;
        foreach (var kv in _sessions)
        {
            if (_sessions.TryRemove(kv.Key, out var removed))
                DisposeEntry(removed, "clear-all");
        }

        _logger.LogInformation("All sessions cleared ({Count})", count);
    }

    // ─── 私有：LRU 淘汰 ───────────────────────────────────────
    /// <summary>
    /// 确保当前会话数不超过 MaxSessions。
    /// 超出时按 LastAccessUtc 升序淘汰最久未访问者。
    /// </summary>
    private void EnforceCapacity()
    {
        if (_options.MaxSessions <= 0) return;      // 0 或负数视为不限制
        if (_sessions.Count < _options.MaxSessions) return;

        // 需要为新会话腾出至少 1 个位置
        var overflow = _sessions.Count - _options.MaxSessions + 1;

        var victims = _sessions
            .OrderBy(kv => kv.Value.LastAccessUtc)
            .Take(overflow)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var id in victims)
        {
            if (_sessions.TryRemove(id, out var removed))
            {
                DisposeEntry(removed, "lru-evict");
                _logger.LogWarning(
                    "Session evicted (LRU): id={SessionId}, lastAccess={LastAccess:o}",
                    id, removed.LastAccessUtc);
            }
        }
    }

    // ─── 私有：资源释放 ───────────────────────────────────────
    /// <summary>
    /// 释放会话持有的资源。Agent / Session 若实现 IDisposable 则一并释放。
    /// 用 try-catch 兜底，避免单个 Dispose 异常影响其他会话清理。
    /// </summary>
    private void DisposeEntry(SessionEntry entry, string reason)
    {
        try
        {
            (entry.Session as IDisposable)?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to dispose session: id={SessionId}, reason={Reason}",
                entry.SessionId, reason);
        }

        try
        {
            (entry.Agent as IDisposable)?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to dispose agent: id={SessionId}, reason={Reason}",
                entry.SessionId, reason);
        }
    }
}