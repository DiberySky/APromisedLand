using MAFWorkFlowApi.Models;

namespace MAFWorkFlowApi.Agents;

/// <summary>会话目录能力：列举、删除、读取消息历史、显示名管理。</summary>
public interface IConversationCatalog
{
    ValueTask<IReadOnlyList<string>> ListConversationIdsAsync(
        CancellationToken cancellationToken = default);

    ValueTask<bool> DeleteConversationAsync(
        string conversationId,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<SessionMessage>> GetSessionMessagesAsync(
        string conversationId,
        CancellationToken cancellationToken = default);

    // ★ 新增：显示名相关
    ValueTask<string?> GetDisplayNameAsync(
        string conversationId,
        CancellationToken cancellationToken = default);

    ValueTask SetDisplayNameAsync(
        string conversationId,
        string displayName,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyDictionary<string, string>> GetAllDisplayNamesAsync(
        CancellationToken cancellationToken = default);
}