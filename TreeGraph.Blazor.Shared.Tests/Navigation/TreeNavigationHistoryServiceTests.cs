using TreeGraph.Blazor.Shared.Trees.Navigation;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.Navigation;

/// <summary>
/// 导航历史服务单元测试。
/// 覆盖：Push / Pop / Peek / Clear / CanGoBack / PopReturnUrlOrDefault。
/// </summary>
public class TreeNavigationHistoryServiceTests
{
    private readonly TreeNavigationHistoryService _sut = new();

    // ============================================================
    // 初始状态
    // ============================================================

    [Fact]
    public void NewInstance_IsEmpty()
    {
        Assert.False(_sut.CanGoBack);
        Assert.Empty(_sut.Stack);
    }

    [Fact]
    public void Pop_OnEmptyStack_ReturnsNull()
    {
        Assert.Null(_sut.Pop());
    }

    [Fact]
    public void Peek_OnEmptyStack_ReturnsNull()
    {
        Assert.Null(_sut.Peek());
    }

    // ============================================================
    // Push / Pop 基本行为
    // ============================================================

    [Fact]
    public void Push_AddsToStack()
    {
        _sut.Push("/page/1");

        Assert.True(_sut.CanGoBack);
        Assert.Single(_sut.Stack);
    }

    [Fact]
    public void Push_StoresAllFields()
    {
        _sut.Push("/page/1", rootId: "root-1", clickNodeId: "node-1");

        var entry = Assert.Single(_sut.Stack);
        Assert.Equal("/page/1", entry.Url);
        Assert.Equal("root-1", entry.RootId);
        Assert.Equal("node-1", entry.ClickNodeId);
    }

    [Fact]
    public void Push_Multiple_LifoOrder()
    {
        _sut.Push("/page/1");
        _sut.Push("/page/2");
        _sut.Push("/page/3");

        Assert.Equal(3, _sut.Stack.Count);

        // LIFO：先弹出的应是最后一个 Push 的
        Assert.Equal("/page/3", _sut.Pop()?.Url);
        Assert.Equal("/page/2", _sut.Pop()?.Url);
        Assert.Equal("/page/1", _sut.Pop()?.Url);
        Assert.Null(_sut.Pop());
    }

    // ============================================================
    // Peek
    // ============================================================

    [Fact]
    public void Peek_DoesNotRemove()
    {
        _sut.Push("/page/1");

        var peeked1 = _sut.Peek();
        var peeked2 = _sut.Peek();

        Assert.Same(peeked1, peeked2);
        Assert.Single(_sut.Stack);
        Assert.True(_sut.CanGoBack);
    }

    // ============================================================
    // Clear
    // ============================================================

    [Fact]
    public void Clear_RemovesAll()
    {
        _sut.Push("/page/1");
        _sut.Push("/page/2");

        _sut.Clear();

        Assert.False(_sut.CanGoBack);
        Assert.Empty(_sut.Stack);
        Assert.Null(_sut.Pop());
    }

    // ============================================================
    // PopReturnUrlOrDefault（回归测试：之前忽略 defaultUrl 参数）
    // ============================================================

    [Fact]
    public void PopReturnUrlOrDefault_WithHistory_ReturnsTop()
    {
        _sut.Push("/page/1");
        _sut.Push("/page/2");

        var result = _sut.PopReturnUrlOrDefault("/default");

        Assert.NotNull(result);
        Assert.Equal("/page/2", result!.Url);
        Assert.Single(_sut.Stack);   // /page/1 仍在
    }

    [Fact]
    public void PopReturnUrlOrDefault_WithEmptyHistory_ReturnsDefaultUrlEntry()
    {
        var result = _sut.PopReturnUrlOrDefault("/default");

        Assert.NotNull(result);
        Assert.Equal("/default", result!.Url);
        Assert.Null(result.RootId);
        Assert.Null(result.ClickNodeId);
    }
}
