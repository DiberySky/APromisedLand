using TreeGraph.Blazor.Services.DemoTree;
using TreeGraph.Blazor.Shared.Trees.StringTree.Models;
using Xunit;

namespace TreeGraph.Blazor.Tests.Services.DemoTree;

/// <summary>
/// InMemoryStringTreeDataSource：纯委托层，验证转发正确。
/// </summary>
public class InMemoryStringTreeDataSourceTests
{
    private readonly InMemoryStringTreeStore _store = new();
    private readonly InMemoryStringTreeDataSource _sut;

    public InMemoryStringTreeDataSourceTests()
    {
        _sut = new InMemoryStringTreeDataSource(_store);
    }

    [Fact]
    public async Task GetRootsAsync_DelegatesToStore()
    {
        var fromSource = await _sut.GetRootsAsync();
        var fromStore = _store.GetRoots();

        Assert.Equal(fromStore.Select(r => r.Id), fromSource.Select(r => r.Id));
    }

    [Fact]
    public async Task GetChildrenAsync_DelegatesToStore()
    {
        var root = _store.GetRoots().First();

        var fromSource = await _sut.GetChildrenAsync(root.Id);
        var fromStore = _store.GetChildren(root.Id);

        Assert.Equal(fromStore.Select(c => c.Id), fromSource.Select(c => c.Id));
    }

    [Fact]
    public async Task GetByIdAsync_DelegatesToStore()
    {
        var target = _store.GetRoots().First();

        var fetched = await _sut.GetByIdAsync(target.Id);

        Assert.NotNull(fetched);
        Assert.Equal(target.Id, fetched!.Id);
    }

    [Fact]
    public async Task GetAncestorPathAsync_DelegatesToStore()
    {
        var deep = Descendant(_store, "iPhone");
        Assert.NotNull(deep);

        var path = await _sut.GetAncestorPathAsync(deep!.Id);

        Assert.NotNull(path);
        Assert.Equal(3, path!.Count);
    }

    private static StringNodeMeta? Descendant(
        InMemoryStringTreeStore store, string text)
    {
        var queue = new Queue<StringNodeMeta>(store.GetRoots());
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            if (c.Text == text) return c;
            foreach (var ch in store.GetChildren(c.Id)) queue.Enqueue(ch);
        }
        return null;
    }
}
