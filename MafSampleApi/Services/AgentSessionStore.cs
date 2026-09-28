using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using MafSampleApi.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace MafSampleApi.Services;

/// <summary>
/// 增强版会话存储：key = sessionId。
/// 当 (model, systemPrompt, 采样参数) 指纹变化时自动重建 Agent + Session。
/// 与 InMemorySessionStore 完全独立，互不影响。
/// </summary>
public sealed class AgentSessionStore(
    IChatClient chatClient,
    IOptions<AgentOptions> agentOptions,
    ILogger<AgentSessionStore> logger)
{
    private readonly AgentOptions _options = agentOptions.Value;
    private readonly ConcurrentDictionary<string, Entry> _sessions = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<Entry> GetOrCreateAsync(
        string sessionId,
        string model,
        string systemPrompt,
        ChatOptions chatOptions,
        CancellationToken ct = default)
    {
        var fingerprint = ComputeFingerprint(model, systemPrompt, chatOptions);

        // 快路径：命中且指纹一致
        if (_sessions.TryGetValue(sessionId, out var existing))
        {
            if (existing.Fingerprint == fingerprint)
            {
                existing.LastAccessUtc = DateTimeOffset.UtcNow;
                return existing;
            }

            // 指纹变了 → 移除旧的，走慢路径重建
            if (_sessions.TryRemove(sessionId, out var stale))
            {
                DisposeEntry(stale);
                logger.LogInformation(
                    "Agent session invalidated (fingerprint change): id={Id}", sessionId);
            }
        }

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // 双检
            if (_sessions.TryGetValue(sessionId, out existing))
            {
                if (existing.Fingerprint == fingerprint)
                {
                    existing.LastAccessUtc = DateTimeOffset.UtcNow;
                    return existing;
                }
                if (_sessions.TryRemove(sessionId, out var stale))
                    DisposeEntry(stale);
            }

            EnforceCapacity();

            // 创建 Agent —— 采样参数直接进 ChatClientAgentOptions.ChatOptions
            var agentOptionsLocal = new ChatClientAgentOptions
            {
                Name = $"assistant-{model}",
                ChatOptions = new ChatOptions
                {
                    Instructions    = systemPrompt,
                    ModelId         = model,
                    Temperature     = chatOptions.Temperature,
                    TopP            = chatOptions.TopP,
                    Seed            = chatOptions.Seed,
                    StopSequences   = chatOptions.StopSequences,
                    MaxOutputTokens = chatOptions.MaxOutputTokens,
                },
            };

            var agent   = new ChatClientAgent(chatClient, agentOptionsLocal);
            var session = await agent.CreateSessionAsync(ct).ConfigureAwait(false);

            var entry = new Entry
            {
                SessionId     = sessionId,
                Model         = model,
                SystemPrompt  = systemPrompt,
                Fingerprint   = fingerprint,
                Agent         = agent,
                Session       = session,
                LastAccessUtc = DateTimeOffset.UtcNow,
            };

            _sessions[sessionId] = entry;

            logger.LogInformation(
                "Agent session created: id={Id}, model={Model}, total={N}",
                sessionId, model, _sessions.Count);

            return entry;
        }
        finally
        {
            _gate.Release();
        }
    }

    public IReadOnlyList<string> ListIds()
        => _sessions
            .OrderByDescending(kv => kv.Value.LastAccessUtc)
            .Select(kv => kv.Key)
            .ToArray();

    public bool TryRemove(string sessionId)
    {
        if (!_sessions.TryRemove(sessionId, out var removed)) return false;
        DisposeEntry(removed);
        logger.LogInformation(
            "Agent session removed: id={Id}, remaining={N}",
            sessionId, _sessions.Count);
        return true;
    }

    public void Clear()
    {
        var count = _sessions.Count;
        foreach (var kv in _sessions)
        {
            if (_sessions.TryRemove(kv.Key, out var e))
                DisposeEntry(e);
        }
        logger.LogInformation("Agent sessions cleared ({N})", count);
    }

    // ─── 私有 ────────────────────────────────────

    private void EnforceCapacity()
    {
        if (_options.MaxSessions <= 0) return;
        if (_sessions.Count < _options.MaxSessions) return;

        var overflow = _sessions.Count - _options.MaxSessions + 1;
        var victims = _sessions
            .OrderBy(kv => kv.Value.LastAccessUtc)
            .Take(overflow)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var id in victims)
        {
            if (_sessions.TryRemove(id, out var e))
            {
                DisposeEntry(e);
                logger.LogWarning("Agent session evicted (LRU): id={Id}", id);
            }
        }
    }

    private void DisposeEntry(Entry e)
    {
        try { (e.Session as IDisposable)?.Dispose(); } catch { /* 忽略 */ }
        try { (e.Agent   as IDisposable)?.Dispose(); } catch { /* 忽略 */ }
    }

    private static string ComputeFingerprint(
        string model, string systemPrompt, ChatOptions opts)
    {
        var sb = new StringBuilder();
        sb.Append(model).Append('\u0001')
          .Append(systemPrompt).Append('\u0001')
          .Append(opts.Temperature?.ToString("R") ?? "-").Append('\u0001')
          .Append(opts.TopP?.ToString("R")        ?? "-").Append('\u0001')
          .Append(opts.Seed?.ToString()           ?? "-").Append('\u0001')
          .Append(opts.MaxOutputTokens?.ToString() ?? "-").Append('\u0001');

        if (opts.StopSequences is { Count: > 0 })
            foreach (var s in opts.StopSequences)
                sb.Append(s).Append('\u0002');

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash);
    }

    public sealed class Entry
    {
        public required string        SessionId     { get; init; }
        public required string        Model         { get; init; }
        public required string        SystemPrompt  { get; init; }
        public required string        Fingerprint   { get; init; }
        public required AIAgent       Agent         { get; init; }
        public required AgentSession  Session       { get; init; }
        public DateTimeOffset         LastAccessUtc { get; set; } = DateTimeOffset.UtcNow;
    }
}