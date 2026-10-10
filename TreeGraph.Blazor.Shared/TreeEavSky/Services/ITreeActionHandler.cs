using TreeGraph.Blazor.Shared.TreeEavSky.Models;
using TreeGraph.Shared.TreeEavSky.Models;
using TreeGraph.Shared.TreeEavSky.Abstractions;

namespace TreeGraph.Blazor.Shared.TreeEavSky.Services;

/// <summary>
/// 树节点写操作接口。
///
/// 职责边界：
///   - Handler = DTO 构造 + 调 API + 业务校验
///   - TreeEavSky = 对话框编排 + 消息提示 + UI 刷新 + CancellationToken
///
/// 与 <see cref="ITreeClientService{TTree}"/> 的关系：
///   - ITreeClientService = 读操作（宿主实现的通用契约）
///   - ITreeActionHandler  = 写操作（可替换的业务契约）
///
/// 默认实现 <see cref="DefaultTreeActionHandler{TItem}"/> 直接转发到
/// <see cref="TreeApiClient{T}"/>；宿主可替换为带校验 / 审计 / 缓存的实现。
/// </summary>
/// <typeparam name="TItem">树节点值类型</typeparam>
public interface ITreeActionHandler<TItem>
    where TItem : class, ITreeNodeBase<TItem>, new()
{
    /// <summary>创建子节点。返回创建后的节点（含后端生成的 ID 等）；失败返回 null。</summary>
    Task<TItem?> CreateChildAsync(
        TItem parent, TItem newChild, CancellationToken ct = default);

    /// <summary>更新节点信息（不含 ParentId，移动请走 <see cref="MoveNodeAsync"/>）。</summary>
    Task<TItem?> UpdateNodeAsync(
        TItem node, CancellationToken ct = default);

    /// <summary>删除节点（含全部后代）。节点不存在返回 false，其他失败抛异常。</summary>
    Task<bool> DeleteNodeAsync(
        TItem node, CancellationToken ct = default);

    /// <summary>移动节点到新父（null 表示根）。节点/新父不存在或构成环时返回 false。</summary>
    Task<bool> MoveNodeAsync(
        TItem node, TItem? newParent, CancellationToken ct = default);

    /// <summary>
    /// 按 <paramref name="orderedChildren"/> 的列表顺序重排父节点的子节点。
    /// 列表顺序即新顺序（后端按位置重新编号 SortOrder）。
    /// </summary>
    Task<bool> SortChildrenAsync(
        TItem parent, IReadOnlyList<TItem> orderedChildren, CancellationToken ct = default);
}
