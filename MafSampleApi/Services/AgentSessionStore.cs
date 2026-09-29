using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using MafSampleApi.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MafSampleApi.Services;

/// <summary>
/// 方案 C：只接收基础工具（进指纹，Agent 创建时绑定）。
/// 高级工具由 Controller 在每次 RunAsync 时通过 RunOptions 动态注入。
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
        IReadOnlyList<AIFunction> baseTools,
        CancellationToken ct = default)
    {
        var baseToolNames = baseTools.Select(f => f.Name)
                                     .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                                     .ToArray();
        var fingerprint = ComputeFingerprint(model, systemPrompt, chatOptions, baseToolNames);

        if (_sessions.TryGetValue(sessionId, out var existing))
        {
            if (existing.Fingerprint == fingerprint)
            {
                existing.LastAccessUtc = DateTimeOffset.UtcNow;
                return existing;
            }
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

            var agentOptions = new ChatClientAgentOptions
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
                    // ★ 修复：List<AIFunction> → IList<AITool>
                    Tools           = baseTools.Cast<AITool>().ToList(),
                },
            };

            var agent   = new ChatClientAgent(chatClient, agentOptions);
            var session = await agent.CreateSessionAsync(ct).ConfigureAwait(false);

            var entry = new Entry
            {
                SessionId     = sessionId,
                Model         = model,
                SystemPrompt  = systemPrompt,
                Fingerprint   = fingerprint,
                BaseToolNames = baseToolNames,
                BoundChatOptions = agentOptions.ChatOptions,   // ★ 保留原始 ChatOptions 快照，供 RunOptions 重建时复用采样参数
                Agent         = agent,
                Session       = session,
                LastAccessUtc = DateTimeOffset.UtcNow,
            };

            _sessions[sessionId] = entry;

            logger.LogInformation(
                "Agent session created: id={Id}, model={Model}, baseTools={N}, total={C}",
                sessionId, model, baseToolNames.Length, _sessions.Count);

            return entry;
        }
        finally
        {
            _gate.Release();
        }
    }

    public IReadOnlyList<string> ListIds()
        => _sessions.OrderByDescending(kv => kv.Value.LastAccessUtc)
                    .Select(kv => kv.Key).ToArray();

    /// <summary>清理超过空闲阈值的会话，返回实际移除数。</summary>
    public int CleanupIdle(TimeSpan idleTimeout)
    {
        if (idleTimeout <= TimeSpan.Zero) return 0;

        var cutoff = DateTimeOffset.UtcNow - idleTimeout;
        var victims = _sessions
            .Where(kv => kv.Value.LastAccessUtc < cutoff)
            .Select(kv => kv.Key)
            .ToList();

        var removed = 0;
        foreach (var id in victims)
            if (_sessions.TryRemove(id, out var e))
            {
                DisposeEntry(e);
                removed++;
            }

        if (removed > 0)
            logger.LogInformation(
                "Idle session cleanup: removed {N}, remaining={C}", removed, _sessions.Count);

        return removed;
    }

    public bool TryRemove(string sessionId)
    {
        if (!_sessions.TryRemove(sessionId, out var e)) return false;
        DisposeEntry(e);
        return true;
    }

    public void Clear()
    {
        foreach (var kv in _sessions)
            if (_sessions.TryRemove(kv.Key, out var e))
                DisposeEntry(e);
    }

    private void EnforceCapacity()
    {
        if (_options.MaxSessions <= 0) return;
        if (_sessions.Count < _options.MaxSessions) return;

        var overflow = _sessions.Count - _options.MaxSessions + 1;
        var victims = _sessions.OrderBy(kv => kv.Value.LastAccessUtc)
                               .Take(overflow)
                               .Select(kv => kv.Key).ToList();
        foreach (var id in victims)
            if (_sessions.TryRemove(id, out var e)) DisposeEntry(e);
    }

    // ★ 修复：is 模式匹配 + catch 带变量
    // ★ 修复：优先走 IAsyncDisposable（MAF 的 Agent/Session 通常实现它），
    //   仅当对象未实现 IAsyncDisposable 时才回退到 IDisposable。
    //   同步阻塞 DisposeAsync 仅在清理路径触发（非热路径），ASP.NET Core 无 SyncContext 不会死锁。
    private void DisposeEntry(Entry e)
    {
        DisposeResource(e.Session, "session", e.SessionId);
        DisposeResource(e.Agent,  "agent",   e.SessionId);
    }

    private void DisposeResource(object? resource, string kind, string sessionId)
    {
        if (resource is null) return;

        if (resource is IAsyncDisposable asyncDisp)
        {
            try { asyncDisp.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
            catch (Exception ex)
            {
                logger.LogDebug(ex,
                    "DisposeAsync {Kind} failed: id={Id}", kind, sessionId);
            }
        }
        else if (resource is IDisposable syncDisp)
        {
            try { syncDisp.Dispose(); }
            catch (Exception ex)
            {
                logger.LogDebug(ex,
                    "Dispose {Kind} failed: id={Id}", kind, sessionId);
            }
        }
    }

    private static string ComputeFingerprint(
        string model, string systemPrompt, ChatOptions opts,
        IReadOnlyList<string> baseToolNames)
    {
        var sb = new StringBuilder();
        sb.Append(model).Append('\u0001')
          .Append(systemPrompt).Append('\u0001')
          .Append(opts.Temperature?.ToString("R")  ?? "-").Append('\u0001')
          .Append(opts.TopP?.ToString("R")         ?? "-").Append('\u0001')
          .Append(opts.Seed?.ToString()            ?? "-").Append('\u0001')
          .Append(opts.MaxOutputTokens?.ToString() ?? "-").Append('\u0001');

        if (opts.StopSequences is { Count: > 0 })
            foreach (var s in opts.StopSequences)
                sb.Append(s).Append('\u0002');

        sb.Append('\u0003');
        foreach (var n in baseToolNames)
            sb.Append(n).Append('\u0002');

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash);
    }

    public sealed class Entry
    {
        public required string       SessionId     { get; init; }
        public required string       Model         { get; init; }
        public required string       SystemPrompt  { get; init; }
        public required string       Fingerprint   { get; init; }
        public required string[]     BaseToolNames { get; init; }
        /// <summary>Agent 创建时绑定的 ChatOptions 快照（含采样参数），供 RunOptions 重建时复用。</summary>
        public required ChatOptions   BoundChatOptions { get; init; }
        public required AIAgent      Agent         { get; init; }
        public required AgentSession Session       { get; init; }
        public DateTimeOffset        LastAccessUtc { get; set; } = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// 后台定时扫描并清理超过 SessionIdleTimeout 的空闲 Agent 会话。
/// </summary>
public sealed class SessionCleanupService(
    AgentSessionStore sessionStore,
    IOptions<AgentOptions> options,
    ILogger<SessionCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var idleTimeout = options.Value.SessionIdleTimeout;
        if (idleTimeout <= TimeSpan.Zero)
        {
            logger.LogInformation("Session idle cleanup disabled (SessionIdleTimeout <= 0)");
            return;
        }

        logger.LogInformation(
            "Session idle cleanup started: idleTimeout={T}, scanInterval={S}",
            idleTimeout, ScanInterval);

        using var timer = new PeriodicTimer(ScanInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                sessionStore.CleanupIdle(idleTimeout);
            }
        }
        catch (OperationCanceledException) { /* shutting down */ }
    }
}