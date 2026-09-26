using Microsoft.Agents.AI;

namespace MafSampleApi.Services;

/// <summary>
/// 会话存储抽象：按 sessionId 管理 AIAgent 与 AgentSession 的配对。
/// </summary>
public interface ISessionStore
{
    /// <summary>
    /// 获取已有会话，或调用工厂创建新会话。
    /// </summary>
    /// <param name="sessionId">会话 ID。</param>
    /// <param name="model">模型名称（传给工厂）。</param>
    /// <param name="agentFactory">Agent 工厂，参数为模型名。</param>
    /// <param name="ct">取消令牌。</param>
    Task<SessionEntry> GetOrCreateAsync(
        string sessionId,
        string model,
        Func<string, AIAgent> agentFactory,
        CancellationToken ct);

    /// <summary>列出所有会话 ID。</summary>
    IReadOnlyList<string> ListIds();

    /// <summary>移除指定会话。</summary>
    bool TryRemove(string id);

    /// <summary>清空全部会话。</summary>
    void Clear();
}

/// <summary>
/// 会话条目：Agent 实例 + 对应的 Session。
/// </summary>
public sealed class SessionEntry
{
    public required string SessionId { get; init; }
    public required string Model { get; init; }
    public required AIAgent Agent { get; init; }
    public required AgentSession Session { get; init; }

    /// <summary>上次访问时间，用于 LRU 淘汰与列表排序。</summary>
    public DateTimeOffset LastAccessUtc { get; set; } = DateTimeOffset.UtcNow;
}
