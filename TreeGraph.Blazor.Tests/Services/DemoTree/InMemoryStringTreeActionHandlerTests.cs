using TreeGraph.Blazor.Services.DemoTree;
using TreeGraph.Blazor.Shared.Trees.StringTree.Models;
using Xunit;

namespace TreeGraph.Blazor.Tests.Services.DemoTree;

/// <summary>
/// InMemoryStringTreeActionHandler：验证每个方法都委托到 store，
/// 且返回语义与接口契约一致。
/// </summary>
public class InMemoryStringTreeActionHandlerTests
{
    private readonly InMemoryStringTreeStore _store = new();
    private readonly InMemoryStringTreeActionHandler _sut;

    public InMemoryStringTreeActionHandlerTests()
    {
        _sut = new InMemoryStringTreeActionHandler(_store);
    }

    [Fact]
    public async Task CreateChildAsync_UnderLegalParent_ReturnsCreated()
    {
        var root = _store.GetRoots().First();

        var created = await _sut.CreateChildAsync(root.Id, new StringNodeMeta { Text = "新项" });

        Assert.NotNull(created);
        Assert.Equal(root.Id, created!.ParentId);
        Assert.NotNull(_store.GetById(created.Id));
    }

    [Fact]
    public async Task CreateChildAsync_UnderIllegalParent_ReturnsNull()
    {
        var iphone = Descendant("iPhone");
        Assert.False(iphone!.CanHaveChildren);

        var result = await _sut.CreateChildAsync(iphone.Id, new StringNodeMeta { Text = "X" });

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateNodeAsync_Existing_ReturnsSameNode()
    {
        var target = _store.GetRoots().First();
        var updated = new StringNodeMeta { Id = target.Id, Text = "改后" };

        var result = await _sut.UpdateNodeAsync(updated);

        Assert.NotNull(result);
        Assert.Equal("改后", result!.Text);
        Assert.Equal("改后", _store.GetById(target.Id)!.Text);
    }

    [Fact]
    public async Task UpdateNodeAsync_Missing_ReturnsNull()
    {
        Assert.Null(await _sut.UpdateNodeAsync(new StringNodeMeta { Id = "nonexistent" }));
    }

    [Fact]
    public async Task DeleteNodeAsync_Existing_ReturnsTrue()
    {
        var root = _store.GetRoots().First();
        Assert.True(await _sut.DeleteNodeAsync(root.Id));
        Assert.Null(_store.GetById(root.Id));
    }

    [Fact]
    public async Task DeleteNodeAsync_Missing_ReturnsFalse()
    {
        Assert.False(await _sut.DeleteNodeAsync("nonexistent"));
    }

    [Fact]
    public async Task MoveNodeAsync_Legal_ReturnsTrue()
    {
        var phone = Descendant("手机");
        var computer = Descendant("电脑");

        Assert.True(await _sut.MoveNodeAsync(phone!.Id, computer!.Id));
        Assert.Equal(computer.Id, _store.GetById(phone.Id)!.ParentId);
    }

    [Fact]
    public async Task MoveNodeAsync_Illegal_ReturnsFalse()
    {
        var phone = Descendant("手机");
        Assert.False(await _sut.MoveNodeAsync(phone!.Id, phone.Id));
    }

    [Fact]
    public async Task SortChildrenAsync_ReordersByPosition()
    {
        var electronics = _store.GetRoots().First();
        var children = _store.GetChildren(electronics.Id);
        var reversedIds = children.Select(c => c.Id).Reverse().ToArray();

        Assert.True(await _sut.SortChildrenAsync(electronics.Id, reversedIds));

        var after = _store.GetChildren(electronics.Id);
        Assert.Equal(reversedIds, after.Select(c => c.Id).ToArray());
    }

    private StringNodeMeta? Descendant(string text)
    {
        var queue = new Queue<StringNodeMeta>(_store.GetRoots());
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            if (c.Text == text) return c;
            foreach (var ch in _store.GetChildren(c.Id)) queue.Enqueue(ch);
        }
        return null;
    }
}
