using TreeGraph.TreeSky.Models;

namespace TreeGraph.Blazor.Services.DemoTree;

/// <summary>
/// 演示树内存存储（单例）。
/// 进程内独立演示库，不落任何业务库；构造时只播种一次，天然幂等。
/// 同时供 <see cref="DemoTreeClientService"/> 与 <see cref="DemoTreeApiHandler"/> 共享，
/// 保证“HTTP 写入 → 刷新读取”数据一致。所有读写加锁。
/// </summary>
public class InMemoryTreeStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, StringTreeNode> _nodes = new();

    public InMemoryTreeStore()
    {
        Seed();
    }

    // ==================== 查询 ====================

    /// <summary>根节点（ParentId == null）；rootId 指定时返回该节点所在的根层（演示数据仅一根）。</summary>
    public IReadOnlyList<TreeNodeDto<StringTreeNode>> GetRoots(string? rootId = null)
    {
        lock (_gate)
        {
            return _nodes.Values
                .Where(n => n.ParentId == null)
                .OrderBy(n => n.SortOrder).ThenBy(n => n.Name)
                .Select(ToDtoLazy)
                .ToList();
        }
    }

    public IReadOnlyList<TreeNodeDto<StringTreeNode>> GetChildren(string parentId)
    {
        lock (_gate)
        {
            if (!_nodes.TryGetValue(parentId, out var parent))
                return [];

            return parent.Children
                .OrderBy(n => n.SortOrder).ThenBy(n => n.Name)
                .Select(ToDtoLazy)
                .ToList();
        }
    }

    public IReadOnlyList<string> GetAncestorPath(string nodeId)
    {
        lock (_gate)
        {
            var path = new List<string>();
            var current = _nodes.GetValueOrDefault(nodeId);
            while (current != null)
            {
                path.Add(current.Id);
                current = current.ParentId == null ? null : _nodes.GetValueOrDefault(current.ParentId);
            }
            path.Reverse();
            return path;
        }
    }

    // ==================== 写入 ====================

    public TreeNodeDto<StringTreeNode> Create(TreeNodeDto<StringTreeNode> dto)
    {
        lock (_gate)
        {
            var node = dto.Value ?? new StringTreeNode();
            if (string.IsNullOrWhiteSpace(node.Id))
                node.Id = Guid.NewGuid().ToString("N");

            node.Id = node.Id.Trim();
            if (_nodes.ContainsKey(node.Id))
                throw new InvalidOperationException($"节点 {node.Id} 已存在");

            node.ParentId = dto.ParentId;
            node.SortOrder = dto.Value?.SortOrder ?? 0;

            Attach(node);
            return ToDtoLazy(node);
        }
    }

    public TreeNodeDto<StringTreeNode> Update(string id, TreeNodeDto<StringTreeNode> dto)
    {
        lock (_gate)
        {
            if (!_nodes.TryGetValue(id, out var node))
                throw new KeyNotFoundException($"节点 {id} 不存在");

            var incoming = dto.Value;
            if (incoming != null)
            {
                node.Name = incoming.Name;
                node.Description = incoming.Description;
                node.CanHaveChildren = incoming.CanHaveChildren;
                node.SortOrder = incoming.SortOrder;
            }

            // 移动：以“当前图挂接关系”（node.Parent）而非 node.ParentId 判断。
            // 客户端（TreeSky）发请求前就会把 node.ParentId 改成新父，只比字段会漏判。
            var newParentId = dto.ParentId;
            var currentParentId = node.Parent?.Id;
            if (newParentId != currentParentId)
            {
                if (newParentId != null && !_nodes.ContainsKey(newParentId))
                    throw new KeyNotFoundException($"父节点 {newParentId} 不存在");
                if (newParentId != null && IsAncestor(id, newParentId))
                    throw new InvalidOperationException("不能将节点移动到自己的子节点下");

                Detach(node);
                node.ParentId = newParentId;
                Attach(node);
            }

            return ToDtoLazy(node);
        }
    }

    public bool Delete(string id)
    {
        lock (_gate)
        {
            if (!_nodes.TryGetValue(id, out var node))
                return false;

            // 级联硬删除全部后代
            var toDelete = new List<string>();
            CollectIds(node, toDelete);
            foreach (var delId in toDelete)
            {
                var n = _nodes[delId];
                Detach(n);
                _nodes.Remove(delId);
            }
            return true;
        }
    }

    /// <summary>按给定顺序重排某父节点下的子项 SortOrder。</summary>
    public TreeNodeDto<StringTreeNode> UpdateChildren(TreeNodeDto<StringTreeNode> dto)
    {
        lock (_gate)
        {
            if (!_nodes.TryGetValue(dto.Id, out var parent))
                throw new KeyNotFoundException($"父节点 {dto.Id} 不存在");

            int order = 0;
            foreach (var childDto in dto.Children ?? [])
            {
                var childId = childDto.Value?.Id ?? childDto.Id;
                if (_nodes.TryGetValue(childId, out var child) && child.ParentId == parent.Id)
                {
                    child.SortOrder = order;
                    if (childDto.Value != null)
                    {
                        child.Name = childDto.Value.Name;
                        child.CanHaveChildren = childDto.Value.CanHaveChildren;
                    }
                }
                order++;
            }
            return ToDtoLazy(parent);
        }
    }

    // ==================== 内部 ====================

    private void Attach(StringTreeNode node)
    {
        node.Parent = node.ParentId == null ? null : _nodes.GetValueOrDefault(node.ParentId);
        node.Children = [];
        _nodes[node.Id] = node;
        if (node.Parent != null)
        {
            node.Parent.Children.Add(node);
            node.Parent.HasChildren = true;
        }
    }

    private static void Detach(StringTreeNode node)
    {
        if (node.Parent != null)
        {
            node.Parent.Children.Remove(node);
            node.Parent.HasChildren = node.Parent.Children.Count > 0;
            node.Parent = null;
        }
    }

    private bool IsAncestor(string ancestorId, string nodeId)
    {
        var current = _nodes.GetValueOrDefault(nodeId);
        while (current != null)
        {
            if (current.Id == ancestorId) return true;
            current = current.ParentId == null ? null : _nodes.GetValueOrDefault(current.ParentId);
        }
        return false;
    }

    private static void CollectIds(StringTreeNode node, List<string> ids)
    {
        ids.Add(node.Id);
        foreach (var child in node.Children)
            CollectIds(child, ids);
    }

    /// <summary>
    /// 转为 DTO。懒加载语义：不带子孙 Children（由 HasChildren 指示可展开），但带 Parent 引用。
    /// </summary>
    private static TreeNodeDto<StringTreeNode> ToDtoLazy(StringTreeNode n) => new()
    {
        Id = n.Id,
        ParentId = n.ParentId,
        Value = n,
        Parent = n.Parent,
        Text = n.Name,
        SortOrder = n.SortOrder,
        HasChildren = n.HasChildren,
        Children = null,
    };

    // ==================== 种子数据 ====================

    private void Seed()
    {
        StringTreeNode Node(string id, string name, string? parentId, int sort,
            bool canHaveChildren, string? desc = null)
        {
            var n = new StringTreeNode
            {
                Id = id,
                Name = name,
                Description = desc,
                ParentId = parentId,
                SortOrder = sort,
                CanHaveChildren = canHaveChildren,
            };
            Attach(n);
            return n;
        }

        Node("root", "物品总类", null, 0, true, "演示树的根节点");

        Node("cat-electronics", "电子产品", "root", 0, true);
        Node("leaf-phone", "智能手机", "cat-electronics", 0, false, "可移动、可编辑的叶子节点");
        Node("leaf-laptop", "笔记本电脑", "cat-electronics", 1, false);

        Node("cat-office", "办公用品", "root", 1, true);
        Node("leaf-pen", "中性笔", "cat-office", 0, false);
        Node("leaf-paper", "A4 打印纸", "cat-office", 1, false);

        Node("cat-furniture", "家具（空分类）", "root", 2, true, "暂无子项，可在此创建子节点");
    }
}

