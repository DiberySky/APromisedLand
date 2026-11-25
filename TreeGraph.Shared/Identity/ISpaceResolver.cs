namespace TreeGraph.Shared.Identity;

/// <summary>
/// 解析节点（inode）所属空间 ID。供空间授权过滤器在“节点级”端点
/// （路径参数是实体 id 而非 spaceId）上判定访问权限。
/// </summary>
public interface ISpaceResolver
{
    /// <summary>返回节点所属空间 ID；无归属节点返回 null。</summary>
    Task<string?> ResolveSpaceIdAsync(string nodeId, CancellationToken ct = default);
}
