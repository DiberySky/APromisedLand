using MAFWorkFlowApi.Models;

namespace MAFWorkFlowApi.Agents;

/// <summary>
/// 会话目录能力：列举、删除、读取消息历史。
/// AgentSessionStore 抽象本身不提供这些能力，因此单独抽出此接口。
/// </summary>
public interface IConversationCatalog
{
    /// <summary>列举所有会话 ID。</summary>
    ValueTask<IReadOnlyList<string>> ListConversationIdsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>删除指定会话；返回是否确实删除了记录。</summary>
    ValueTask<bool> DeleteConversationAsync(
        string conversationId,
        CancellationToken cancellationToken = default);

    /// <summary>读取指定会话的消息历史。</summary>
    ValueTask<IReadOnlyList<SessionMessage>> GetSessionMessagesAsync(
        string conversationId,
        CancellationToken cancellationToken = default);
}