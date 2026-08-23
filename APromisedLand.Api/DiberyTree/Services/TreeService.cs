using System.Collections.Concurrent;
using APromisedLand.Shared.DiberyTree;
using APromisedLand.Shared.DiberyTree.Interfaces;
using APromisedLand.Shared.DiberyTree.Models;

namespace APromisedLand.Api.DiberyTree.Services;

/// <summary>
/// 泛型树服务实现（内存存储示例）
/// </summary>
/// <typeparam name="T">节点值的类型</typeparam>
public class TreeService<T> where T : class, ITreeNodeBase<T> //ITreeService<T> 
{
    private readonly ConcurrentDictionary<string, TreeNodeDto<T>> _nodes = new();
    private readonly ConcurrentDictionary<string, List<string>> _parentChildren = new();
    //private int _idCounter;

    public TreeService()
    {
        // 初始化示例数据
        InitializeSampleData();
    }

    private void InitializeSampleData()
    {
        // 创建根节点
        var root1 = CreateNodeInternal(null, "根节点 1", default(T), "folder", true);
        var root2 = CreateNodeInternal(null, "根节点 2", default(T), "folder", true);
        
        // 创建子节点
        var child1 = CreateNodeInternal(root1.Id, "子节点 1-1", default(T), "description", false);
        var child2 = CreateNodeInternal(root1.Id, "子节点 1-2", default(T), "description", true);
        var child3 = CreateNodeInternal(root2.Id, "子节点 2-1", default(T), "description", false);
    
        // 创建孙节点
        var grandChild = CreateNodeInternal(child2.Id, "孙节点 1-2-1", default(T), "insert_drive_file", false);
    }

    private TreeNodeDto<T> CreateNodeInternal(string? parentId, string text, T? value, string? icon, bool hasChildren)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        var id = value.Id;
        
        // var node = new TreeNodeDto<T>
        // {
        //     Id = value?.Id ?? id,
        //     ParentId = parentId,
        //     Value = value,
        //     Text = value?.Text(),
        //     Icon = icon,
        //     HasChildren = hasChildren,
        //     Children = new List<TreeNodeDto<T>>()
        // };
        
        var node = value.ToNodeDto();
        
        _nodes.TryAdd(id, node);

        if (!string.IsNullOrEmpty(parentId))
        {
            _parentChildren.AddOrUpdate(parentId,
                _ => [id],
                (_, list) =>
                {
                    list.Add(id);
                    return list;
                });
        }

        return node;
    }

    public Task<List<TreeNodeDto<T>>> GetRootNodesAsync(CancellationToken cancellationToken = default)
    {
        var roots = _nodes.Values
            .Where(n => string.IsNullOrEmpty(n.ParentId))
            .ToList();
        return Task.FromResult(roots);
    }

    public Task<List<TreeNodeDto<T>>> GetChildrenAsync(string parentId, CancellationToken cancellationToken = default)
    {
        if (!_parentChildren.TryGetValue(parentId, out var childIds))
        {
            return Task.FromResult(new List<TreeNodeDto<T>>());
        }

        var children = childIds
            .Where(id => _nodes.TryGetValue(id, out _))
            .Select(id => _nodes[id])
            .ToList();

        return Task.FromResult(children);
    }

    public Task<List<TreeNodeDto<T>>> QueryNodesAsync(TreeQueryParams queryParams,
        CancellationToken cancellationToken = default)
    {
        var query = _nodes.Values.AsQueryable();

        // 按父节点过滤
        if (!string.IsNullOrEmpty(queryParams.ParentId))
        {
            query = query.Where(n => n.ParentId == queryParams.ParentId);
        }
        else if (queryParams.ParentId == null)
        {
            query = query.Where(n => string.IsNullOrEmpty(n.ParentId));
        }

        // 搜索过滤
        if (!string.IsNullOrEmpty(queryParams.SearchTerm))
        {
            query = query.Where(n =>
                (n.Text != null && n.Text.Contains(queryParams.SearchTerm, StringComparison.OrdinalIgnoreCase)) ||
                (n.Value != null && n.Value.ToString() != null &&
                 n.Value.ToString().Contains(queryParams.SearchTerm, StringComparison.OrdinalIgnoreCase)));
        }

        // 只包含有子节点的节点
        if (queryParams.OnlyWithChildren)
        {
            query = query.Where(n => n.HasChildren);
        }

        // 分页
        var result = query
            .Skip((queryParams.Page - 1) * queryParams.PageSize)
            .Take(queryParams.PageSize)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<TreeNodeDto<T>?> GetFullTreeAsync(string? rootId = null, CancellationToken cancellationToken = default)
    {
        TreeNodeDto<T>? root;
        if (!string.IsNullOrEmpty(rootId))
        {
            _nodes.TryGetValue(rootId, out root);
            if (root == null) return Task.FromResult<TreeNodeDto<T>?>(null);
        }
        else
        {
            // 获取第一个根节点
            root = _nodes.Values.FirstOrDefault(n => string.IsNullOrEmpty(n.ParentId));
            if (root == null) return Task.FromResult<TreeNodeDto<T>?>(null);
        }

        // 递归构建完整树
        var fullTree = BuildFullTree(root);
        return Task.FromResult<TreeNodeDto<T>?>(fullTree);
    }

    private TreeNodeDto<T> BuildFullTree(TreeNodeDto<T> node)
    {
        var cloned = new TreeNodeDto<T>
        {
            Id = node.Id,
            ParentId = node.ParentId,
            Value = node.Value,
            Text = node.Text,
            Icon = node.Icon,
            HasChildren = node.HasChildren,
            Expanded = node.Expanded,
            Selected = node.Selected,
            Children = new List<TreeNodeDto<T>>()
        };

        if (_parentChildren.TryGetValue(node.Id, out var childIds))
        {
            foreach (var childId in childIds)
            {
                if (_nodes.TryGetValue(childId, out var child))
                {
                    cloned.Children.Add(BuildFullTree(child));
                }
            }
        }

        return cloned;
    }

    public Task<TreeNodeDto<T>> CreateNodeAsync(TreeNodeDto<T> node, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid().ToString();
        node.Id = id;

        _nodes.TryAdd(id, node);

        if (!string.IsNullOrEmpty(node.ParentId))
        {
            _parentChildren.AddOrUpdate(node.ParentId,
                _ => new List<string> { id },
                (_, list) =>
                {
                    list.Add(id);
                    return list;
                });

            // 更新父节点的 HasChildren
            if (_nodes.TryGetValue(node.ParentId, out var parent))
            {
                parent.HasChildren = true;
            }
        }

        return Task.FromResult(node);
    }

    public Task<TreeNodeDto<T>> UpdateNodeAsync(TreeNodeDto<T> node, CancellationToken cancellationToken = default)
    {
        if (!_nodes.ContainsKey(node.Id))
        {
            throw new KeyNotFoundException($"节点 {node.Id} 不存在");
        }

        _nodes[node.Id] = node;
        return Task.FromResult(node);
    }

    public Task<bool> DeleteNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        if (!_nodes.TryGetValue(nodeId, out var node))
        {
            return Task.FromResult(false);
        }

        // 递归删除所有子节点
        DeleteNodeRecursive(nodeId);

        // 从父节点的子节点列表中移除
        if (!string.IsNullOrEmpty(node.ParentId) && _parentChildren.TryGetValue(node.ParentId, out var siblings))
        {
            siblings.Remove(nodeId);
            if (siblings.Count == 0 && _nodes.TryGetValue(node.ParentId, out var parent))
            {
                parent.HasChildren = false;
            }
        }

        return Task.FromResult(true);
    }

    private void DeleteNodeRecursive(string nodeId)
    {
        if (_parentChildren.TryGetValue(nodeId, out var childIds))
        {
            foreach (var childId in childIds.ToList())
            {
                DeleteNodeRecursive(childId);
            }

            _parentChildren.TryRemove(nodeId, out _);
        }

        _nodes.TryRemove(nodeId, out _);
    }

    public Task<bool> MoveNodeAsync(string nodeId, string? newParentId, CancellationToken cancellationToken = default)
    {
        if (!_nodes.TryGetValue(nodeId, out var node))
        {
            return Task.FromResult(false);
        }

        // 检查是否移动到自己的子节点下（防止循环）
        if (IsDescendant(nodeId, newParentId))
        {
            return Task.FromResult(false);
        }

        // 从原父节点移除
        if (!string.IsNullOrEmpty(node.ParentId) && _parentChildren.TryGetValue(node.ParentId, out var oldSiblings))
        {
            oldSiblings.Remove(nodeId);
        }

        // 添加到新父节点
        node.ParentId = newParentId;
        if (!string.IsNullOrEmpty(newParentId))
        {
            _parentChildren.AddOrUpdate(newParentId,
                _ => new List<string> { nodeId },
                (_, list) =>
                {
                    list.Add(nodeId);
                    return list;
                });

            if (_nodes.TryGetValue(newParentId, out var newParent))
            {
                newParent.HasChildren = true;
            }
        }

        return Task.FromResult(true);
    }

    private bool IsDescendant(string ancestorId, string? nodeId)
    {
        if (string.IsNullOrEmpty(nodeId)) return false;
        if (ancestorId == nodeId) return true;

        if (_nodes.TryGetValue(nodeId, out var node) && !string.IsNullOrEmpty(node.ParentId))
        {
            return IsDescendant(ancestorId, node.ParentId);
        }

        return false;
    }
}