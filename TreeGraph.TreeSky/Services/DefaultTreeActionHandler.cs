using TreeGraph.TreeSky.Models;

namespace TreeGraph.TreeSky.Services;

/// <summary>
/// 默认写操作实现：直接转发到 <see cref="DiberyTreeApiClient{T}"/>。
///
/// 核心价值：集中"如何从 TItem 构造 TreeNodeDto"的业务知识
/// （此前散落在 TreeSky.HandleXxxAsync 中）。
///
/// 宿主可继承覆盖 <see cref="BuildDto"/> 等方法，或整体替换为自定义 Handler。
/// </summary>
public class DefaultTreeActionHandler<TItem>(
    DiberyTreeApiClient<TItem> client)
    : ITreeActionHandler<TItem>
    where TItem : class, ITreeNodeBase<TItem>, new()
{
    // ============================================================
    // 写操作
    // ============================================================

    public virtual async Task<TItem?> CreateChildAsync(
        TItem parent, TItem newChild, CancellationToken ct = default)
    {
        var dto = BuildDto(newChild, parentId: parent.Id);
        var result = await client.CreateNodeAsync(dto, ct);
        return result.Value;
    }

    public virtual async Task<TItem?> UpdateNodeAsync(
        TItem node, CancellationToken ct = default)
    {
        var dto = BuildDto(node, parentId: node.ParentId);
        var result = await client.UpdateNodeAsync(node.Id, dto, ct);
        return result.Value;
    }

    public virtual Task<bool> DeleteNodeAsync(
        TItem node, CancellationToken ct = default)
        => client.DeleteNodeAsync(node.Id, ct);

    public virtual Task<bool> MoveNodeAsync(
        TItem node, TItem? newParent, CancellationToken ct = default)
        => client.MoveNodeAsync(node.Id, newParent?.Id, ct);

    public virtual async Task<bool> SortChildrenAsync(
        TItem parent, IReadOnlyList<TItem> orderedChildren,
        CancellationToken ct = default)
    {
        var dto = new TreeNodeDto<TItem>
        {
            Id = parent.Id,
            Text = parent.Text(),
            Icon = TreeHelper.TreeItemIcons,
            ParentId = parent.ParentId,
            Value = parent,
            Children = orderedChildren
                .Select((child, index) => new TreeNodeDto<TItem>
                {
                    Id = child.Id,
                    Text = child.Text(),
                    Icon = TreeHelper.TreeItemIcons,
                    ParentId = parent.Id,
                    SortOrder = index,
                    Value = child,
                })
                .ToList(),
        };

        await client.UpdateChildrenAsync(dto, ct);
        return true;
    }

    // ============================================================
    // 可覆盖钩子
    // ============================================================

    /// <summary>
    /// 从 TItem 构造 TreeNodeDto（默认设置 Id / Text / Icon / ParentId / SortOrder / Value）。
    /// 宿主可覆盖以追加业务字段（如 ExtraData）。
    /// </summary>
    protected virtual TreeNodeDto<TItem> BuildDto(TItem node, string? parentId)
        => new()
        {
            Id = node.Id,
            Text = node.Text(),
            Icon = TreeHelper.TreeItemIcons,
            ParentId = parentId,
            SortOrder = node.SortOrder,
            Value = node,
        };
}
