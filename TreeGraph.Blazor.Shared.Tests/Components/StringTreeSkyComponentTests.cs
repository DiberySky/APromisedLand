using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MudBlazor;
using MudBlazor.Services;
using TreeGraph.Blazor.Shared.Platform;
using TreeGraph.Blazor.Shared.Trees.TreeSky.Dialogs;
using TreeGraph.Blazor.Shared.Trees.StringTree.Components;
using TreeGraph.Blazor.Shared.Trees.StringTree.Extensions;
using TreeGraph.Blazor.Shared.Trees.StringTree.Models;
using TreeGraph.Blazor.Shared.Trees.StringTree.Services;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.Components;

// 旧 StringTree 体系组件别名：本测试类命名空间向上查找会命中新解耦组件的命名空间
// TreeGraph.Blazor.Shared.StringTreeSky（CS0118）；别名放在命名空间内部才能优先于
// 外层命名空间成员生效。
using StringTreeSky = TreeGraph.Blazor.Shared.Trees.StringTree.Components.StringTreeSky;

/// <summary>
/// StringTreeSky 组件测试（bUnit）。
///
/// 参考 TreeSkyComponentTests 模式：
/// - 用 stub IStringTreeDataSource / IStringTreeActionHandler（内联类，不用 Moq）
/// - JSInterop Loose 模式
/// - 覆盖加载状态 / 数据渲染 / 异常不崩溃 / 懒加载 / 深层展开
///
/// 不测：具体 DOM 交互（MudTreeView 的展开/点击，由 E2E 覆盖）。
/// </summary>
public class StringTreeSkyComponentTests : BunitTestBase {
    // ============================================================
    // 测试 Doubles
    // ============================================================

    private sealed class StubDataSource : IStringTreeDataSource
    {
        public List<StringNodeMeta> Roots { get; } = new();
        public Dictionary<string, List<StringNodeMeta>> Children { get; } = new();
        public Dictionary<string, List<string>> AncestorPaths { get; } = new();
        public int GetRootsCallCount { get; private set; }
        public int GetChildrenCallCount { get; private set; }

        public Task<IReadOnlyList<StringNodeMeta>> GetRootsAsync(CancellationToken ct = default)
        {
            GetRootsCallCount++;
            return Task.FromResult<IReadOnlyList<StringNodeMeta>>(Roots.ToList());
        }

        public Task<IReadOnlyList<StringNodeMeta>> GetChildrenAsync(
            string parentId, CancellationToken ct = default)
        {
            GetChildrenCallCount++;
            return Task.FromResult<IReadOnlyList<StringNodeMeta>>(
                Children.TryGetValue(parentId, out var list)
                    ? list.ToList()
                    : new List<StringNodeMeta>());
        }

        public Task<StringNodeMeta?> GetByIdAsync(string id, CancellationToken ct = default)
            => Task.FromResult(Roots.FirstOrDefault(r => r.Id == id));

        public Task<List<string>?> GetAncestorPathAsync(string id, CancellationToken ct = default)
            => Task.FromResult(AncestorPaths.TryGetValue(id, out var p) ? p : null);
    }

    /// <summary>GetRootsAsync 永不完成 → 组件停在加载态。</summary>
    private sealed class SlowDataSource : IStringTreeDataSource
    {
        private readonly TaskCompletionSource<IReadOnlyList<StringNodeMeta>> _tcs = new();

        public Task<IReadOnlyList<StringNodeMeta>> GetRootsAsync(CancellationToken ct = default)
            => _tcs.Task;

        public Task<IReadOnlyList<StringNodeMeta>> GetChildrenAsync(
            string parentId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<StringNodeMeta>>(new List<StringNodeMeta>());

        public Task<StringNodeMeta?> GetByIdAsync(string id, CancellationToken ct = default)
            => Task.FromResult<StringNodeMeta?>(null);

        public Task<List<string>?> GetAncestorPathAsync(string id, CancellationToken ct = default)
            => Task.FromResult<List<string>?>(null);
    }

    /// <summary>GetRootsAsync 抛异常 → 验证组件不崩溃。</summary>
    private sealed class ThrowingDataSource : IStringTreeDataSource
    {
        public Task<IReadOnlyList<StringNodeMeta>> GetRootsAsync(CancellationToken ct = default)
            => throw new InvalidOperationException("数据源故障");

        public Task<IReadOnlyList<StringNodeMeta>> GetChildrenAsync(
            string parentId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<StringNodeMeta>>(new List<StringNodeMeta>());

        public Task<StringNodeMeta?> GetByIdAsync(string id, CancellationToken ct = default)
            => Task.FromResult<StringNodeMeta?>(null);

        public Task<List<string>?> GetAncestorPathAsync(string id, CancellationToken ct = default)
            => Task.FromResult<List<string>?>(null);
    }

    private sealed class StubActionHandler : IStringTreeActionHandler
    {
        public Task<StringNodeMeta?> CreateChildAsync(
            string parentId, StringNodeMeta newChild, CancellationToken ct = default)
            => Task.FromResult<StringNodeMeta?>(newChild);

        public Task<StringNodeMeta?> UpdateNodeAsync(
            StringNodeMeta node, CancellationToken ct = default)
            => Task.FromResult<StringNodeMeta?>(node);

        public Task<bool> DeleteNodeAsync(string id, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<bool> MoveNodeAsync(
            string id, string? newParentId, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<bool> SortChildrenAsync(
            string parentId, IReadOnlyList<string> orderedChildIds,
            CancellationToken ct = default)
            => Task.FromResult(true);
    }

    private readonly StubDataSource _dataSource = new();

    public StringTreeSkyComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IPlatformContext>(new FakePlatformContext());
        Services.AddScoped<MessageService>();
        Services.AddLegacyTreeSky();
        Services.AddSingleton<IStringTreeDataSource>(_dataSource);
        Services.AddSingleton<IStringTreeActionHandler, StubActionHandler>();
    }

    private static StringNodeMeta Root(string id, string text, bool hasChildren = false)
        => new() { Id = id, Text = text, CanHaveChildren = true, HasChildren = hasChildren };

    // ============================================================
    // 加载状态
    // ============================================================

    [Fact]
    public void Render_WhileLoading_ShowsProgressCircular()
    {
        // 用 SlowDataSource 替换默认 Stub：GetRootsAsync 永不完成
        Services.RemoveAll<IStringTreeDataSource>();
        Services.AddSingleton<IStringTreeDataSource, SlowDataSource>();

        var cut = Render<StringTreeSky>();

        // 组件停在首次 await，保持 loading
        Assert.Contains("mud-progress-circular", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("mud-treeview", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_EmptyRoots_ShowsTreeViewNotProgress()
    {
        _dataSource.Roots.Clear();

        var cut = Render<StringTreeSky>();

        cut.WaitForState(
            () => cut.Markup.Contains("mud-treeview", StringComparison.Ordinal),
            TimeSpan.FromSeconds(2));
        // 空数据渲染空树，不显示加载圈
        Assert.DoesNotContain("mud-progress-circular", cut.Markup, StringComparison.Ordinal);
    }

    // ============================================================
    // 数据到达
    // ============================================================

    [Fact]
    public void Render_AfterLoad_ShowsTreeViewWithRootText()
    {
        _dataSource.Roots.Add(Root("1", "根节点A"));
        _dataSource.Roots.Add(Root("2", "根节点B"));

        var cut = Render<StringTreeSky>();

        cut.WaitForState(
            () => cut.Markup.Contains("mud-treeview", StringComparison.Ordinal),
            TimeSpan.FromSeconds(2));
        Assert.Contains("根节点A", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("根节点B", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("mud-progress-circular", cut.Markup, StringComparison.Ordinal);
    }

    // ============================================================
    // 加载异常不崩溃
    // ============================================================

    [Fact]
    public void Render_LoadThrows_DoesNotCrash()
    {
        Services.RemoveAll<IStringTreeDataSource>();
        Services.AddSingleton<IStringTreeDataSource, ThrowingDataSource>();

        var cut = Render<StringTreeSky>();

        // 异常被 OnInitializedAsync catch，组件渲染空树而非抛异常
        cut.WaitForState(
            () => cut.Markup.Contains("mud-treeview", StringComparison.Ordinal),
            TimeSpan.FromSeconds(2));
    }

    // ============================================================
    // 初始加载只调一次
    // ============================================================

    [Fact]
    public void Render_CallsGetRootsExactlyOnce()
    {
        _dataSource.Roots.Add(Root("1", "A"));

        Render<StringTreeSky>();

        Assert.Equal(1, _dataSource.GetRootsCallCount);
    }

    // ============================================================
    // 深层书签直达
    // ============================================================

    [Fact]
    public void DeepLink_ClickNodeId_LoadsAncestorPath()
    {
        _dataSource.Roots.Add(Root("1", "根", hasChildren: true));
        _dataSource.Children["1"] = new()
        {
            new StringNodeMeta { Id = "2", Text = "子", ParentId = "1", HasChildren = true }
        };
        _dataSource.Children["2"] = new()
        {
            new StringNodeMeta { Id = "3", Text = "孙", ParentId = "2" }
        };
        _dataSource.AncestorPaths["3"] = new() { "1", "2", "3" };

        var cut = Render<StringTreeSky>(p => p
            .Add(x => x.ClickNodeId, "3"));

        cut.WaitForState(
            () => cut.Instance.GetAllLoadedNodes().Any(n => n.Id == "3"),
            TimeSpan.FromSeconds(2));

        // 路径 1→2→3：展开 1 和 2，至少调 2 次 GetChildren
        Assert.True(_dataSource.GetChildrenCallCount >= 2);
    }

    // ============================================================
    // GetAllLoadedNodes 不触发懒加载
    // ============================================================

    [Fact]
    public void GetAllLoadedNodes_DoesNotTriggerLazyLoad()
    {
        _dataSource.Roots.Add(Root("1", "根"));

        var cut = Render<StringTreeSky>();
        cut.WaitForState(() => cut.Instance.GetAllLoadedNodes().Count == 1,
            TimeSpan.FromSeconds(2));

        // 初始加载只调 GetRoots，不调 GetChildren
        Assert.Equal(0, _dataSource.GetChildrenCallCount);
    }

    // ============================================================
    // EnsureAllNodesLoadedAsync 显式加载
    // ============================================================

    [Fact]
    public async Task EnsureAllNodesLoadedAsync_LoadsAllChildren()
    {
        _dataSource.Roots.Add(Root("1", "根", hasChildren: true));
        _dataSource.Children["1"] = new()
        {
            new StringNodeMeta { Id = "2", Text = "子", ParentId = "1" }
        };

        var cut = Render<StringTreeSky>();
        cut.WaitForState(() => cut.Instance.GetAllLoadedNodes().Count == 1,
            TimeSpan.FromSeconds(2));

        var all = await cut.InvokeAsync(() => cut.Instance.EnsureAllNodesLoadedAsync());

        Assert.Equal(2, all.Count);
        Assert.Contains(all, n => n.Id == "2");
    }

    // ============================================================
    // RefreshAsync
    // ============================================================

    [Fact]
    public async Task RefreshAsync_ReloadsRoots()
    {
        _dataSource.Roots.Add(Root("1", "A"));

        var cut = Render<StringTreeSky>();
        cut.WaitForState(() => cut.Instance.GetAllLoadedNodes().Count == 1,
            TimeSpan.FromSeconds(2));

        _dataSource.Roots.Clear();
        _dataSource.Roots.Add(Root("2", "B"));

        await cut.InvokeAsync(() => cut.Instance.RefreshAsync());

        var nodes = cut.Instance.GetAllLoadedNodes();
        Assert.Single(nodes);
        Assert.Equal("2", nodes[0].Id);
    }
}
