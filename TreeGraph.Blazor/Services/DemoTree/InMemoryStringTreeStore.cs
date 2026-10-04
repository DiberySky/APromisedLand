using MudBlazor;
using TreeGraph.Blazor.Shared.Trees.StringTree.Models;

namespace TreeGraph.Blazor.Services.DemoTree;

/// <summary>
/// 内存树共享存储：DataSource 与 ActionHandler 共用同一份数据。
/// 演示用途，非线程安全（UI 单线程访问）。
/// </summary>
public class InMemoryStringTreeStore
{
    private readonly Dictionary<string, StringNodeMeta> _nodes = new();
    private readonly object _lock = new();

    public InMemoryStringTreeStore()
    {
        Seed();
    }

    // ============================================================
    // 种子数据（3 层 8 节点）
    // ============================================================

    private void Seed()
    {
        AddRoot("电子产品", Icons.Material.Filled.Folder, order: 10, hasChildren: true);
        AddRoot("服装", Icons.Material.Filled.Checkroom, order: 20, hasChildren: true);

        var electronics = FindByName("电子产品")!;
        AddChild(electronics.Id, "手机", Icons.Material.Filled.Smartphone, order: 10, canHaveChildren: true);
        AddChild(electronics.Id, "电脑", Icons.Material.Filled.Laptop, order: 20, canHaveChildren: true);

        var phones = FindByName("手机")!;
        AddChild(phones.Id, "安卓手机", Icons.Material.Filled.PhoneAndroid, order: 10, canHaveChildren: false);
        AddChild(phones.Id, "iPhone", Icons.Material.Filled.PhoneIphone, order: 20, canHaveChildren: false);

        var computers = FindByName("电脑")!;
        AddChild(computers.Id, "笔记本", Icons.Material.Filled.LaptopMac, order: 10, canHaveChildren: false);

        var clothing = FindByName("服装")!;
        AddChild(clothing.Id, "男装", Icons.Material.Filled.Man, order: 10, canHaveChildren: false);
    }

    // ============================================================
    // 查询
    // ============================================================

    public IReadOnlyList<StringNodeMeta> GetRoots() => Snapshot()
        .Where(n => n.ParentId is null)
        .OrderBy(n => n.SortOrder).ThenBy(n => n.Text)
        .ToList();

    public IReadOnlyList<StringNodeMeta> GetChildren(string parentId) => Snapshot()
        .Where(n => n.ParentId == parentId)
        .OrderBy(n => n.SortOrder).ThenBy(n => n.Text)
        .ToList();

    public StringNodeMeta? GetById(string id)
        => _nodes.GetValueOrDefault(id);

    public List<string>? GetAncestorPath(string id)
    {
        var path = new List<string>();
        var cursor = _nodes.GetValueOrDefault(id);
        while (cursor is not null)
        {
            path.Insert(0, cursor.Id);
            cursor = cursor.ParentId is null
                ? null
                : _nodes.GetValueOrDefault(cursor.ParentId);
        }
        return path.Count > 0 ? path : null;
    }

    // ============================================================
    // 写入
    // ============================================================

    public StringNodeMeta? Create(string parentId, StringNodeMeta template)
    {
        var parent = _nodes.GetValueOrDefault(parentId);
        if (parent is null || !parent.CanHaveChildren) return null;

        var created = template.Clone();
        created.Id = Guid.NewGuid().ToString("D");
        created.ParentId = parentId;
        created.SortOrder = NextSortOrder(parentId);
        _nodes[created.Id] = created;

        parent.HasChildren = true;
        return created;
    }

    public bool Update(StringNodeMeta node)
    {
        var existing = _nodes.GetValueOrDefault(node.Id);
        if (existing is null) return false;

        existing.Text = node.Text;
        existing.Description = node.Description;
        existing.Subtitle = node.Subtitle;
        existing.Icon = node.Icon;
        existing.CanHaveChildren = node.CanHaveChildren;
        existing.ExtraData = node.ExtraData;
        return true;
    }

    public bool Delete(string id)
    {
        var node = _nodes.GetValueOrDefault(id);
        if (node is null) return false;

        // 级联删后代
        var queue = new Queue<string>();
        queue.Enqueue(id);
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            foreach (var child in _nodes.Values.Where(n => n.ParentId == cur).ToList())
                queue.Enqueue(child.Id);
            _nodes.Remove(cur);
        }

        // 更新父的 HasChildren
        if (node.ParentId is { } pid && _nodes.GetValueOrDefault(pid) is { } parent)
            parent.HasChildren = _nodes.Values.Any(n => n.ParentId == pid);

        return true;
    }

    public bool Move(string id, string? newParentId)
    {
        var node = _nodes.GetValueOrDefault(id);
        if (node is null) return false;

        // 防环：newParent 不能是自己或后代
        var cursor = newParentId;
        while (cursor is not null)
        {
            if (cursor == id) return false;
            cursor = _nodes.GetValueOrDefault(cursor)?.ParentId;
        }

        if (newParentId is not null)
        {
            var np = _nodes.GetValueOrDefault(newParentId);
            if (np is null || !np.CanHaveChildren) return false;
        }

        var oldParentId = node.ParentId;
        node.ParentId = newParentId;
        node.SortOrder = NextSortOrder(newParentId);

        // 更新新旧父的 HasChildren
        if (oldParentId is { } op && _nodes.GetValueOrDefault(op) is { } opNode)
            opNode.HasChildren = _nodes.Values.Any(n => n.ParentId == op);
        if (newParentId is { } np2 && _nodes.GetValueOrDefault(np2) is { } npNode)
            npNode.HasChildren = true;

        return true;
    }

    public bool Sort(string parentId, IReadOnlyList<string> orderedIds)
    {
        // 按列表位置重编号（★ 不按 SortOrder 排序，避免撤销拖拽结果）
        for (int i = 0; i < orderedIds.Count; i++)
        {
            var node = _nodes.GetValueOrDefault(orderedIds[i]);
            if (node is null || node.ParentId != parentId) continue;
            node.SortOrder = (i + 1) * 10;
        }
        return true;
    }

    // ============================================================
    // 辅助
    // ============================================================

    private List<StringNodeMeta> Snapshot()
    {
        lock (_lock) return _nodes.Values.ToList();
    }

    private StringNodeMeta? FindByName(string text)
        => _nodes.Values.FirstOrDefault(n => n.Text == text);

    private int NextSortOrder(string? parentId)
    {
        var siblings = _nodes.Values.Where(n => n.ParentId == parentId).ToList();
        return siblings.Count == 0 ? 10 : siblings.Max(n => n.SortOrder) + 10;
    }

    // 种子构造辅助
    private void AddRoot(string text, string? icon, int order, bool hasChildren)
    {
        var id = Guid.NewGuid().ToString("D");
        _nodes[id] = new StringNodeMeta
        {
            Id = id,
            ParentId = null,
            Text = text,
            Icon = icon,
            HasChildren = hasChildren,
            CanHaveChildren = true,
            SortOrder = order,
        };
    }

    private void AddChild(string parentId, string text, string? icon,
        int order, bool canHaveChildren)
    {
        var id = Guid.NewGuid().ToString("D");
        _nodes[id] = new StringNodeMeta
        {
            Id = id,
            ParentId = parentId,
            Text = text,
            Icon = icon,
            HasChildren = false,
            CanHaveChildren = canHaveChildren,
            SortOrder = order,
        };

        // ★ 与 Create() 语义对齐：子节点存在 ⇒ 父节点 HasChildren=true，
        //   否则父节点 Expandable=false，UI 上永远没有展开箭头
        var parent = _nodes.GetValueOrDefault(parentId);
        if (parent is not null) parent.HasChildren = true;
    }
}
