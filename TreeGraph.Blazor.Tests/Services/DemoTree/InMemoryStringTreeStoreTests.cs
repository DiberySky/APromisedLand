using TreeGraph.Blazor.Services.DemoTree;
using TreeGraph.Blazor.Shared.Trees.StringTree.Models;
using Xunit;

namespace TreeGraph.Blazor.Tests.Services.DemoTree;

/// <summary>
/// InMemoryStringTreeStore 完整单元测试。
///
/// 覆盖：种子数据、查询排序、祖先路径、CRUD、级联删除、
///       HasChildren 回填、CanHaveChildren 约束、防环、排序位置语义。
///
/// 每个测试建独立 Store 实例（种子每个实例独立跑）。
/// </summary>
public class InMemoryStringTreeStoreTests
{
    // ============================================================
    // 种子数据
    // ============================================================

    [Fact]
    public void Seed_HasExpectedNodeCount()
    {
        var store = new InMemoryStringTreeStore();

        // 3 层 8 节点（2 根 + 2 中层 + 4 叶）
        var all = AllNodes(store);
        Assert.Equal(8, all.Count);
    }

    [Fact]
    public void GetRoots_ReturnsTwoSortedRoots()
    {
        var store = new InMemoryStringTreeStore();

        var roots = store.GetRoots();

        Assert.Equal(2, roots.Count);
        Assert.Equal("电子产品", roots[0].Text);
        Assert.Equal("服装", roots[1].Text);
    }

    [Fact]
    public void GetRoots_AllHaveNullParentId()
    {
        var store = new InMemoryStringTreeStore();
        Assert.All(store.GetRoots(), r => Assert.Null(r.ParentId));
    }

    // ============================================================
    // 子节点排序
    // ============================================================

    [Fact]
    public void GetChildren_SortedBySortOrderThenText()
    {
        var store = new InMemoryStringTreeStore();
        var electronics = store.GetRoots().First(r => r.Text == "电子产品");

        var children = store.GetChildren(electronics.Id);

        Assert.Equal(new[] { "手机", "电脑" }, children.Select(c => c.Text).ToArray());
    }

    // ============================================================
    // 祖先路径
    // ============================================================

    [Fact]
    public void GetAncestorPath_DeepNode_ReturnsRootToSelf()
    {
        var store = new InMemoryStringTreeStore();
        var iphone = AllNodes(store).First(n => n.Text == "iPhone");

        var path = store.GetAncestorPath(iphone.Id);

        Assert.NotNull(path);
        Assert.Equal(3, path!.Count);
        // 根在首位
        var root = store.GetRoots().First(r => r.Text == "电子产品");
        Assert.Equal(root.Id, path[0]);
        Assert.Equal(iphone.Id, path[^1]);
    }

    [Fact]
    public void GetAncestorPath_Missing_ReturnsNull()
    {
        var store = new InMemoryStringTreeStore();
        Assert.Null(store.GetAncestorPath("nonexistent"));
    }

    // ============================================================
    // Create
    // ============================================================

    [Fact]
    public void Create_UnderCanHaveChildrenParent_Succeeds()
    {
        var store = new InMemoryStringTreeStore();
        var electronics = store.GetRoots().First(r => r.Text == "电子产品");

        var created = store.Create(electronics.Id, new StringNodeMeta { Text = "新项" });

        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, Guid.Parse(created!.Id));
        Assert.Equal(electronics.Id, created.ParentId);
        Assert.Equal("新项", created.Text);
    }

    [Fact]
    public void Create_GeneratesGuid()
    {
        var store = new InMemoryStringTreeStore();
        var root = store.GetRoots().First();

        var created = store.Create(root.Id, new StringNodeMeta { Text = "X" });

        Assert.True(Guid.TryParse(created!.Id, out _));
    }

    [Fact]
    public void Create_SetsParentHasChildren()
    {
        var store = new InMemoryStringTreeStore();
        var clothing = store.GetRoots().First(r => r.Text == "服装");

        // 服装最初有男装一个子项，HasChildren 已为 true
        var created = store.Create(clothing.Id, new StringNodeMeta { Text = "新叶子" });

        var refreshed = store.GetById(clothing.Id);
        Assert.True(refreshed!.HasChildren);
        Assert.NotNull(created);
    }

    [Fact]
    public void Create_UnderCanHaveChildrenFalse_ReturnsNull()
    {
        var store = new InMemoryStringTreeStore();
        var iphone = AllNodes(store).First(n => n.Text == "iPhone");
        Assert.False(iphone.CanHaveChildren);

        var result = store.Create(iphone.Id, new StringNodeMeta { Text = "不该创建" });

        Assert.Null(result);
        // 子节点数不变
        Assert.Empty(store.GetChildren(iphone.Id));
    }

    [Fact]
    public void Create_UnderMissingParent_ReturnsNull()
    {
        var store = new InMemoryStringTreeStore();
        Assert.Null(store.Create("nonexistent", new StringNodeMeta { Text = "X" }));
    }

    // ============================================================
    // Update
    // ============================================================

    [Fact]
    public void Update_ChangesAllEditableFields()
    {
        var store = new InMemoryStringTreeStore();
        var target = store.GetRoots().First();

        var updated = store.Update(new StringNodeMeta
        {
            Id = target.Id,
            Text = "改名后",
            Description = "新描述",
            Subtitle = "新副标题",
            Icon = "Icons.Material.Filled.NewIcon",
            CanHaveChildren = false,
        });

        Assert.True(updated);
        var refreshed = store.GetById(target.Id)!;
        Assert.Equal("改名后", refreshed.Text);
        Assert.Equal("新描述", refreshed.Description);
        Assert.Equal("新副标题", refreshed.Subtitle);
        Assert.Equal("Icons.Material.Filled.NewIcon", refreshed.Icon);
        Assert.False(refreshed.CanHaveChildren);
    }

    [Fact]
    public void Update_Missing_ReturnsFalse()
    {
        var store = new InMemoryStringTreeStore();
        Assert.False(store.Update(new StringNodeMeta { Id = "nonexistent" }));
    }

    // ============================================================
    // Delete
    // ============================================================

    [Fact]
    public void Delete_RemovesNodeAndDescendants()
    {
        var store = new InMemoryStringTreeStore();
        var electronics = store.GetRoots().First(r => r.Text == "电子产品");
        var before = AllNodes(store).Count;

        var ok = store.Delete(electronics.Id);

        Assert.True(ok);
        var after = AllNodes(store).Count;
        // 电子产品下有 手机(安卓手机, iPhone) + 电脑(笔记本) = 5 个后代 + 自身 = 6
        Assert.Equal(before - 6, after);
        Assert.Null(store.GetById(electronics.Id));
    }

    [Fact]
    public void Delete_UpdatesParentHasChildren()
    {
        var store = new InMemoryStringTreeStore();
        var clothing = store.GetRoots().First(r => r.Text == "服装");
        var menswear = store.GetChildren(clothing.Id).Single();

        store.Delete(menswear.Id);

        var refreshed = store.GetById(clothing.Id)!;
        Assert.False(refreshed.HasChildren);
    }

    [Fact]
    public void Delete_Missing_ReturnsFalse()
    {
        var store = new InMemoryStringTreeStore();
        Assert.False(store.Delete("nonexistent"));
    }

    // ============================================================
    // Move
    // ============================================================

    [Fact]
    public void Move_ToLegalParent_Succeeds()
    {
        var store = new InMemoryStringTreeStore();
        var phone = AllNodes(store).First(n => n.Text == "手机");
        var computer = AllNodes(store).First(n => n.Text == "电脑");

        var ok = store.Move(phone.Id, computer.Id);

        Assert.True(ok);
        Assert.Equal(computer.Id, store.GetById(phone.Id)!.ParentId);
    }

    [Fact]
    public void Move_ToSelf_ReturnsFalse()
    {
        var store = new InMemoryStringTreeStore();
        var phone = AllNodes(store).First(n => n.Text == "手机");
        Assert.False(store.Move(phone.Id, phone.Id));
    }

    [Fact]
    public void Move_ToDescendant_ReturnsFalse()
    {
        var store = new InMemoryStringTreeStore();
        var phone = AllNodes(store).First(n => n.Text == "手机");
        var android = AllNodes(store).First(n => n.Text == "安卓手机");

        // 手机不能移到自己的子节点下
        Assert.False(store.Move(phone.Id, android.Id));
    }

    [Fact]
    public void Move_ToCanHaveChildrenFalse_ReturnsFalse()
    {
        var store = new InMemoryStringTreeStore();
        var phone = AllNodes(store).First(n => n.Text == "手机");
        var iphone = AllNodes(store).First(n => n.Text == "iPhone");
        Assert.False(iphone.CanHaveChildren);

        Assert.False(store.Move(phone.Id, iphone.Id));
    }

    [Fact]
    public void Move_ToRoot_Succeeds()
    {
        var store = new InMemoryStringTreeStore();
        var phone = AllNodes(store).First(n => n.Text == "手机");

        Assert.True(store.Move(phone.Id, null));
        Assert.Null(store.GetById(phone.Id)!.ParentId);
    }

    [Fact]
    public void Move_UpdatesBothParentsHasChildren()
    {
        var store = new InMemoryStringTreeStore();
        var clothing = store.GetRoots().First(r => r.Text == "服装");
        var menswear = store.GetChildren(clothing.Id).Single();

        // 把服装唯一的子项移走
        store.Move(menswear.Id, null);

        Assert.False(store.GetById(clothing.Id)!.HasChildren);
    }

    // ============================================================
    // Sort
    // ============================================================

    [Fact]
    public void Sort_ReordersByPosition_NotOldSortOrder()
    {
        var store = new InMemoryStringTreeStore();
        var electronics = store.GetRoots().First(r => r.Text == "电子产品");
        var children = store.GetChildren(electronics.Id);   // 手机(10), 电脑(20)
        var phone = children.First(c => c.Text == "手机");
        var computer = children.First(c => c.Text == "电脑");

        // 反序提交
        store.Sort(electronics.Id, new[] { computer.Id, phone.Id });

        var reordered = store.GetChildren(electronics.Id);
        Assert.Equal("电脑", reordered[0].Text);
        Assert.Equal("手机", reordered[1].Text);
    }

    [Fact]
    public void Sort_IgnoresNodesNotUnderParent()
    {
        var store = new InMemoryStringTreeStore();
        var electronics = store.GetRoots().First(r => r.Text == "电子产品");
        var clothing = store.GetRoots().First(r => r.Text == "服装");
        var phone = store.GetChildren(electronics.Id).First();
        var menswear = store.GetChildren(clothing.Id).Single();

        var beforeMenswear = store.GetById(menswear.Id)!.SortOrder;

        store.Sort(electronics.Id, new[] { phone.Id, menswear.Id });

        // menswear 不属于 electronics → 不应被重排
        Assert.Equal(beforeMenswear, store.GetById(menswear.Id)!.SortOrder);
    }

    // ============================================================
    // 辅助
    // ============================================================

    /// <summary>递归收集全部节点（含种子）。</summary>
    private static List<StringNodeMeta> AllNodes(InMemoryStringTreeStore store)
    {
        var result = new List<StringNodeMeta>();
        var queue = new Queue<StringNodeMeta>(store.GetRoots());

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(current);
            foreach (var child in store.GetChildren(current.Id))
                queue.Enqueue(child);
        }

        return result;
    }
}
