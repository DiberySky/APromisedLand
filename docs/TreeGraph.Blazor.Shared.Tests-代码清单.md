# TreeGraph.Blazor.Shared.Tests C# 代码清单

- 生成时间：2026-10-04 21:37:24
- 文件总数：10
- 排除：bin/、obj/、csproj、README.md
- 项目状态：TreeSky 87 项单测（组件/Handler/DI 顺序/模型）；原 TreeSky.Tests 更名而来

## 文件 1/10 TreeGraph.Blazor.Shared.Tests/Attributes/TreeRouteAttributeTests.cs

```csharp
using System.Net;
using System.Text;
using TreeGraph.Blazor.Shared.Trees.Attributes;
using TreeGraph.Blazor.Shared.Trees.Models;
using TreeGraph.Blazor.Shared.Trees.Services;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.Attributes;

public class TreeRouteAttributeTests
{
    // ============================================================
    // 特性声明
    // ============================================================

    [Fact]
    public void StringTreeNode_IsDecorated_WithCurrentUrlPrefix()
    {
        var attr = typeof(StringTreeNode).GetCustomAttributes(false)
            .OfType<TreeRouteAttribute>()
            .SingleOrDefault();

        Assert.NotNull(attr);
        // 与后端 [Route("[controller]")] 的 StringTreeNodeController 一致
        Assert.Equal("StringTreeNode", attr!.Route);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_EmptyRoute_Throws(string route)
    {
        Assert.Throws<ArgumentException>(() => new TreeRouteAttribute(route));
    }

    // ============================================================
    // DiberyTreeApiClient 路由解析（桩 HttpClient）
    // ============================================================

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? LastUrl { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUrl = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"success":true,"data":[]}""", Encoding.UTF8, "application/json"),
            });
        }
    }

    [TreeRoute("custom-nodes")]
    private sealed class DecoratedNode : ITreeNodeBase<DecoratedNode>
    {
        public string Id { get; set; } = "";
        public string? ParentId { get; set; }
        public string? Description { get; set; }
        public bool CanHaveChildren { get; set; }
        public int SortOrder { get; set; }
        public bool HasChildren { get; set; }
        public DecoratedNode? Parent { get; set; }
        public string Text() => Id;
    }

    private sealed class PlainNode : ITreeNodeBase<PlainNode>
    {
        public string Id { get; set; } = "";
        public string? ParentId { get; set; }
        public string? Description { get; set; }
        public bool CanHaveChildren { get; set; }
        public int SortOrder { get; set; }
        public bool HasChildren { get; set; }
        public PlainNode? Parent { get; set; }
        public string Text() => Id;
    }

    [Fact]
    public async Task Client_DecoratedType_UsesAttributeRoute()
    {
        var handler = new RecordingHandler();
        var client = new DiberyTreeApiClient<DecoratedNode>(
            new HttpClient(handler) { BaseAddress = new Uri("https://test/") });

        await client.GetRootNodesAsync();

        Assert.Equal("https://test/custom-nodes/roots", handler.LastUrl!.ToString());
    }

    [Fact]
    public async Task Client_UndecoratedType_FallsBackToTypeName()
    {
        var handler = new RecordingHandler();
        var client = new DiberyTreeApiClient<PlainNode>(
            new HttpClient(handler) { BaseAddress = new Uri("https://test/") });

        await client.GetRootNodesAsync();

        // 未标注 → typeof(T).Name，即 "PlainNode"
        Assert.Equal("https://test/PlainNode/roots", handler.LastUrl!.ToString());
    }

    [Fact]
    public async Task Client_StringTreeNode_UrlUnchanged()
    {
        // 回归护栏：A 方案下现有 URL 一个字符都不能变
        var handler = new RecordingHandler();
        var client = new DiberyTreeApiClient<StringTreeNode>(
            new HttpClient(handler) { BaseAddress = new Uri("https://test/") });

        await client.GetRootNodesAsync();

        Assert.Equal("https://test/StringTreeNode/roots", handler.LastUrl!.ToString());
    }
}
```

## 文件 2/10 TreeGraph.Blazor.Shared.Tests/Components/TreeSkyComponentTests.cs

```csharp
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using TreeGraph.Blazor.Shared.Trees.Extensions;
using TreeGraph.Blazor.Shared.Trees;
using TreeGraph.Blazor.Shared.Trees.Models;
using TreeGraph.Blazor.Shared.Trees.Services;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.Components;

/// <summary>
/// TreeSky 组件渲染测试（bUnit）。
/// MudBlazor 的弹层/对话框 JS 交互无法在此完整模拟，只覆盖：
/// 加载中占位、数据到达后渲染树、空数据、加载异常不崩溃、初始加载调用一次。
/// </summary>
public class TreeSkyComponentTests : TestContext
{
    private readonly Mock<ITreeClientService<StringTreeNode>> _clientService = new();

    public TreeSkyComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddMudServices();

        // 注册 TreeSky 全部服务（BlazorService/MessageService/TreeNodeDialogService/
        // 导航历史/DiberyTreeApiClient + AddMudExtensions + HttpClient 工厂）
        Services.AddTreeSky();

        _clientService.SetupProperty(s => s.Title, "测试树");
        _clientService.SetupProperty(s => s.NewPageShow, false);
        _clientService.SetupProperty(s => s.SelectLeaf, false);
        Services.AddSingleton(_clientService.Object);
    }

    private static TreeNodeDto<StringTreeNode> RootDto() => new()
    {
        Id = "1",
        Text = "根节点",
        Value = new StringTreeNode { Id = "1", Name = "根节点" },
        HasChildren = false,
    };

    // ============================================================
    // 初始渲染：加载中
    // ============================================================

    [Fact]
    public void Render_WhileLoading_ShowsProgressCircular()
    {
        // 永不完成 → 组件停在首次 await，保持 loading
        var tcs = new TaskCompletionSource<IReadOnlyList<TreeNodeDto<StringTreeNode>>>();
        _clientService
            .Setup(s => s.LoadInitialDataAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);

        var cut = RenderComponent<TreeSky<StringTreeNode>>();

        Assert.Contains("mud-progress-circular", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("mud-treeview", cut.Markup, StringComparison.Ordinal);
    }

    // ============================================================
    // 加载完成：渲染树 + 节点文本
    // ============================================================

    [Fact]
    public void Render_AfterLoad_ShowsTreeViewWithNodeText()
    {
        _clientService
            .Setup(s => s.LoadInitialDataAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TreeNodeDto<StringTreeNode>> { RootDto() });

        var cut = RenderComponent<TreeSky<StringTreeNode>>();

        cut.WaitForState(
            () => cut.Markup.Contains("mud-treeview", StringComparison.Ordinal),
            TimeSpan.FromSeconds(2));
        Assert.Contains("根节点", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("mud-progress-circular", cut.Markup, StringComparison.Ordinal);
    }

    // ============================================================
    // 空数据：与加载中同占位
    // ============================================================

    [Fact]
    public void Render_EmptyData_ShowsProgressCircular()
    {
        _clientService
            .Setup(s => s.LoadInitialDataAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TreeNodeDto<StringTreeNode>>());

        var cut = RenderComponent<TreeSky<StringTreeNode>>();

        cut.WaitForState(
            () => cut.Markup.Contains("mud-progress-circular", StringComparison.Ordinal),
            TimeSpan.FromSeconds(2));
        Assert.DoesNotContain("mud-treeview", cut.Markup, StringComparison.Ordinal);
    }

    // ============================================================
    // 加载异常：组件内部吞掉异常（Message.Details），不崩溃
    // ============================================================

    [Fact]
    public void Render_LoadThrows_DoesNotCrash()
    {
        _clientService
            .Setup(s => s.LoadInitialDataAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("API 挂了"));

        var exception = Record.Exception(() => RenderComponent<TreeSky<StringTreeNode>>());

        Assert.Null(exception);
    }

    // ============================================================
    // 初始加载只调用一次
    // ============================================================

    [Fact]
    public void Render_CallsLoadInitialDataOnce()
    {
        _clientService
            .Setup(s => s.LoadInitialDataAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TreeNodeDto<StringTreeNode>> { RootDto() });

        RenderComponent<TreeSky<StringTreeNode>>();

        _clientService.Verify(
            s => s.LoadInitialDataAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ============================================================
    // 深路径：ClickNodeId 不在首屏节点中 → 沿祖先路径懒加载并选中
    // ============================================================

    [Fact]
    public void DeepLink_ClickNodeIdNotLoaded_ExpandsAncestorPathAndSelects()
    {
        var root = new TreeNodeDto<StringTreeNode>
        {
            Id = "1",
            Text = "根",
            Value = new StringTreeNode { Id = "1", Name = "根" },
            HasChildren = true,
        };
        _clientService
            .Setup(s => s.LoadInitialDataAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TreeNodeDto<StringTreeNode>> { root });

        _clientService
            .Setup(s => s.GetAncestorPathFromApiAsync("3", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["1", "2", "3"]);

        _clientService
            .Setup(s => s.LoadChildrenAsync(It.Is<StringTreeNode>(n => n.Id == "1"), It.IsAny<CancellationToken>()))
            .ReturnsAsync([NodeDto("2", hasChildren: true)]);
        _clientService
            .Setup(s => s.LoadChildrenAsync(It.Is<StringTreeNode>(n => n.Id == "2"), It.IsAny<CancellationToken>()))
            .ReturnsAsync([NodeDto("3")]);

        var cut = RenderComponent<TreeSky<StringTreeNode>>(
            ("ClickNodeId", "3"));

        cut.WaitForAssertion(
            () => Assert.Equal("3", cut.Instance.SelectedValue?.Id),
            TimeSpan.FromSeconds(2));

        // 关键：初始恢复与 OnParametersSetAsync 不会重复展开（各层只加载一次）
        _clientService.Verify(
            s => s.GetAncestorPathFromApiAsync("3", It.IsAny<CancellationToken>()), Times.Once);
        _clientService.Verify(
            s => s.LoadChildrenAsync(It.Is<StringTreeNode>(n => n.Id == "1"), It.IsAny<CancellationToken>()),
            Times.Once);
        _clientService.Verify(
            s => s.LoadChildrenAsync(It.Is<StringTreeNode>(n => n.Id == "2"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ============================================================
    // GetAllLoadedNodes 只读不触发懒加载；EnsureAllNodesLoadedAsync 显式加载
    // ============================================================

    [Fact]
    public async Task GetAllLoadedNodes_DoesNotTriggerLazyLoad()
    {
        _clientService
            .Setup(s => s.LoadInitialDataAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([NodeDto("1", hasChildren: true)]);
        _clientService
            .Setup(s => s.LoadChildrenAsync(It.Is<StringTreeNode>(n => n.Id == "1"), It.IsAny<CancellationToken>()))
            .ReturnsAsync([NodeDto("2")]);

        var cut = RenderComponent<TreeSky<StringTreeNode>>();
        cut.WaitForState(() => cut.Instance.GetAllLoadedNodes().Count == 1,
            TimeSpan.FromSeconds(2));

        Assert.Single(cut.Instance.GetAllLoadedNodes());
        _clientService.Verify(
            s => s.LoadChildrenAsync(It.IsAny<StringTreeNode>(), It.IsAny<CancellationToken>()), Times.Never);

        var all = await cut.Instance.EnsureAllNodesLoadedAsync();

        Assert.Equal(2, all.Count);
        _clientService.Verify(
            s => s.LoadChildrenAsync(It.Is<StringTreeNode>(n => n.Id == "1"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ============================================================
    // RemoveNodeFromParent：深层移除必须回写父节点 Children 与 HasChildren
    // ============================================================

    [Fact]
    public void RemoveNodeFromParent_DeepRemoval_WritesBackToOwner()
    {
        TreeItemData<StringTreeNode> Leaf(string id) => new()
        {
            Value = new StringTreeNode { Id = id, Name = id },
        };

        var root = new TreeItemData<StringTreeNode>
        {
            Value = new StringTreeNode { Id = "1", Name = "根", HasChildren = true },
            Children = new List<ITreeItemData<StringTreeNode>>
            {
                new TreeItemData<StringTreeNode>
                {
                    Value = new StringTreeNode { Id = "2", Name = "父", HasChildren = true },
                    Children = new List<ITreeItemData<StringTreeNode>> { Leaf("3"), Leaf("4") },
                },
            },
        };
        var roots = new List<TreeItemData<StringTreeNode>> { root };

        var cut = RenderComponent<TreeSky<StringTreeNode>>();

        Assert.True(cut.Instance.RemoveNodeFromParent(roots, "3"));

        var parent = (TreeItemData<StringTreeNode>)root.Children!.Single();
        Assert.Single(parent.Children!);
        Assert.Equal("4", parent.Children!.Single().Value?.Id);
        Assert.True(parent.Value!.HasChildren);

        Assert.True(cut.Instance.RemoveNodeFromParent(roots, "4"));
        Assert.Empty(parent.Children!);
        Assert.False(parent.Value.HasChildren);
    }

    private static TreeNodeDto<StringTreeNode> NodeDto(string id, bool hasChildren = false) => new()
    {
        Id = id,
        Text = id,
        Value = new StringTreeNode { Id = id, Name = id, HasChildren = hasChildren },
        HasChildren = hasChildren,
    };

    // ============================================================
    // 懒加载回写：用户点击展开（MudBlazor 内部 ServerData 路径）
    // 验证 ServerData 结果经 @bind-Items 回写进组件 _items 同一对象树
    // ============================================================

    [Fact]
    public void UserClickExpand_ServerDataResult_WritesBackToComponentItems()
    {
        // 首屏：仅根节点 1，子节点 2 未加载
        _clientService
            .Setup(s => s.LoadInitialDataAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([NodeDto("1", hasChildren: true)]);
        _clientService
            .Setup(s => s.LoadChildrenAsync(
                It.Is<StringTreeNode>(n => n.Id == "1"), It.IsAny<CancellationToken>()))
            .ReturnsAsync([NodeDto("2")]);

        var cut = RenderComponent<TreeSky<StringTreeNode>>();

        // 初始：组件侧 _items 只有 1，2 尚未加载
        Assert.Single(cut.Instance.GetAllLoadedNodes());

        // 用户点击 1 的展开箭头 → MudTreeViewItem 内部 OnItemExpanded
        // → TryInvokeServerLoadFunc → ServerData(1) → _itemsState.SetValueAsync
        var toggle = cut.Find(".mud-treeview-item-arrow button");
        toggle.Click();

        // 关键断言：MudBlazor 加载结果必须回写到组件 _items（context.Children），
        // 组件侧遍历必须能看到用户展开出来的 2
        cut.WaitForAssertion(() =>
        {
            var loaded = cut.Instance.GetAllLoadedNodes();
            Assert.Equal(2, loaded.Count);
            Assert.Contains(loaded, n => n.Id == "2");
            // UI 同步：两行树节点（MudCollapse 折叠后 DOM 保留，未加载则不存在）
            Assert.Equal(2, cut.FindAll("li.mud-treeview-item").Count);
        }, TimeSpan.FromSeconds(5));

        // 守卫实证：折叠后再次展开，Children 已存在 → 不得重复请求
        toggle.Click(); // 折叠
        toggle.Click(); // 重新展开
        _clientService.Verify(
            s => s.LoadChildrenAsync(
                It.Is<StringTreeNode>(n => n.Id == "1"), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
```

## 文件 3/10 TreeGraph.Blazor.Shared.Tests/Extensions/TreeSkyServiceCollectionExtensionsTests.cs

```csharp
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using TreeGraph.Blazor.Shared.Trees.Extensions;
using TreeGraph.Blazor.Shared.Trees.Models;
using TreeGraph.Blazor.Shared.Trees.Services;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.Extensions;

/// <summary>
/// AddTreeSky DI 注册冒烟测试：
/// 重点验证 #6 的开放泛型嵌套注册链
/// ITreeActionHandler&lt;T&gt; → DefaultTreeActionHandler&lt;T&gt; → DiberyTreeApiClient&lt;T&gt; → HttpClient
/// 对任意满足约束的 T 都能由容器构造，且宿主可在 AddTreeSky 之后覆盖默认 Handler。
/// </summary>
public class TreeSkyServiceCollectionExtensionsTests
{
    /// <summary>与 StringTreeNode 不同的第二个节点类型，用于证明开放泛型不依赖具体 T。</summary>
    private sealed class DiSmokeNode : ITreeNodeBase<DiSmokeNode>
    {
        public string Id { get; set; } = "";
        public string? ParentId { get; set; }
        public string? Description { get; set; }
        public bool CanHaveChildren { get; set; }
        public int SortOrder { get; set; }
        public bool HasChildren { get; set; }
        public DiSmokeNode? Parent { get; set; }
        public string Text() => Id;
    }

    /// <summary>宿主覆盖用的自定义 Handler（只验证可解析，方法不会被调用）。</summary>
    private sealed class CustomHandler<T> : ITreeActionHandler<T>
        where T : class, ITreeNodeBase<T>, new()
    {
        public Task<T?> CreateChildAsync(T parent, T newChild, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<T?> UpdateNodeAsync(T node, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<bool> DeleteNodeAsync(T node, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<bool> MoveNodeAsync(T node, T? newParent, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<bool> SortChildrenAsync(T parent, IReadOnlyList<T> orderedChildren, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        // 与真实宿主一致：AddTreeSky 依赖 MudBlazor 服务已注册（AddMudExtensions 扫描需要）
        services.AddMudServices();
        // 不配置 BaseAddress：本测试只验证容器解析链，不发起请求
        services.AddTreeSky();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    [Fact]
    public void AddTreeSky_ResolvesDefaultHandler_AndApiClient_ForStringTreeNode()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        var handler = sp.GetRequiredService<ITreeActionHandler<StringTreeNode>>();
        var apiClient = sp.GetRequiredService<DiberyTreeApiClient<StringTreeNode>>();

        Assert.IsType<DefaultTreeActionHandler<StringTreeNode>>(handler);
        Assert.NotNull(apiClient);
    }

    [Fact]
    public void AddTreeSky_OpenGenericRegistration_WorksForArbitraryNodeType()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        // 若开放泛型嵌套解析（Handler<T> → ApiClient<T> → HttpClient）断裂，此处即抛
        var handler = scope.ServiceProvider
            .GetRequiredService<ITreeActionHandler<DiSmokeNode>>();

        Assert.IsType<DefaultTreeActionHandler<DiSmokeNode>>(handler);
    }

    [Fact]
    public void AddTreeSky_HostRegistrationAfterCall_OverridesDefaultHandler()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMudServices();
        services.AddTreeSky();
        // 模拟宿主用自定义实现覆盖（AddTreeSky 用 AddScoped，后注册者胜）
        services.AddScoped<ITreeActionHandler<StringTreeNode>,
            CustomHandler<StringTreeNode>>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        var handler = scope.ServiceProvider
            .GetRequiredService<ITreeActionHandler<StringTreeNode>>();

        Assert.IsType<CustomHandler<StringTreeNode>>(handler);

        // 未被覆盖的另一个 T 仍走默认实现
        var otherHandler = scope.ServiceProvider
            .GetRequiredService<ITreeActionHandler<DiSmokeNode>>();
        Assert.IsType<DefaultTreeActionHandler<DiSmokeNode>>(otherHandler);
    }

    [Fact]
    public void AddTreeSky_DefaultHandler_IsScoped_SameInstanceWithinScope()
    {
        using var provider = BuildProvider();

        using var scopeA = provider.CreateScope();
        var a1 = scopeA.ServiceProvider.GetRequiredService<ITreeActionHandler<StringTreeNode>>();
        var a2 = scopeA.ServiceProvider.GetRequiredService<ITreeActionHandler<StringTreeNode>>();
        Assert.Same(a1, a2);

        using var scopeB = provider.CreateScope();
        var b = scopeB.ServiceProvider.GetRequiredService<ITreeActionHandler<StringTreeNode>>();
        Assert.NotSame(a1, b);
    }
}
```

## 文件 4/10 TreeGraph.Blazor.Shared.Tests/Models/StringTreeNodeTests.cs

```csharp
using System.Text.Json;
using TreeGraph.Blazor.Shared.Trees.Models;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.Models;

/// <summary>
/// StringTreeNode POCO 单元测试。
/// 覆盖：ITreeNodeBase 接口实现的基本契约 + JSON 循环引用防护。
/// </summary>
public class StringTreeNodeTests
{
    // ============================================================
    // Text()
    // ============================================================

    [Fact]
    public void Text_ReturnsName()
    {
        var node = new StringTreeNode { Name = "分类A" };

        Assert.Equal("分类A", node.Text());
    }

    [Fact]
    public void Text_DefaultName_ReturnsEmpty()
    {
        var node = new StringTreeNode();

        Assert.Equal(string.Empty, node.Text());
    }

    // ============================================================
    // 默认值
    // ============================================================

    [Fact]
    public void DefaultValues_AreSane()
    {
        var node = new StringTreeNode();

        Assert.Equal(string.Empty, node.Id);
        Assert.Equal(string.Empty, node.Name);
        Assert.Null(node.Description);
        Assert.Null(node.ParentId);
        Assert.Null(node.Parent);
        Assert.True(node.CanHaveChildren);   // ★ 默认 true
        Assert.False(node.HasChildren);
        Assert.Equal(0, node.SortOrder);
        Assert.NotNull(node.Children);
        Assert.Empty(node.Children);
    }

    // ============================================================
    // 属性设置
    // ============================================================

    [Fact]
    public void Properties_RoundTrip()
    {
        var node = new StringTreeNode
        {
            Id = "abc-123",
            Name = "测试节点",
            Description = "描述",
            ParentId = "parent-1",
            SortOrder = 5,
            CanHaveChildren = false,
            HasChildren = true,
        };

        Assert.Equal("abc-123", node.Id);
        Assert.Equal("测试节点", node.Name);
        Assert.Equal("描述", node.Description);
        Assert.Equal("parent-1", node.ParentId);
        Assert.Equal(5, node.SortOrder);
        Assert.False(node.CanHaveChildren);
        Assert.True(node.HasChildren);
    }

    // ============================================================
    // [JsonIgnore] 循环引用防护（Parent / Children 不进 JSON）
    // ============================================================

    [Fact]
    public void Json_IgnoresParentAndChildrenNavProperties()
    {
        var node = new StringTreeNode
        {
            Id = "child",
            Name = "子节点",
            Parent = new StringTreeNode { Id = "parent", Name = "父节点" },
            Children = [new StringTreeNode { Id = "grand", Name = "孙节点" }],
        };

        var json = JsonSerializer.Serialize(node);

        // 默认 PascalCase 输出；精确判断导航属性不存在（避免 parentId/canHaveChildren 子串误伤）
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.False(root.TryGetProperty("Parent", out _));
        Assert.False(root.TryGetProperty("Children", out _));
        Assert.Equal("child", root.GetProperty("Id").GetString());
    }
}
```

## 文件 5/10 TreeGraph.Blazor.Shared.Tests/Navigation/TreeNavigationHistoryServiceTests.cs

```csharp
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
```

## 文件 6/10 TreeGraph.Blazor.Shared.Tests/Services/DefaultTreeActionHandlerTests.cs

```csharp
using System.Net;
using System.Text;
using System.Text.Json;
using TreeGraph.Blazor.Shared.Trees.Models;
using TreeGraph.Blazor.Shared.Trees.Services;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.Services;

/// <summary>
/// DefaultTreeActionHandler 单测。
/// DiberyTreeApiClient 的方法非虚，Moq 无法拦截，
/// 因此用桩 HttpMessageHandler + 真实 HttpClient 走完整 HTTP 管道，
/// 同时验证 URL 路由、方法谓词与请求体 DTO 构造。
/// </summary>
public class DefaultTreeActionHandlerTests
{
    /// <summary>记录最近一次请求并返回预置 JSON 的桩处理器。</summary>
    private sealed class StubHttpHandler : HttpMessageHandler
    {
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
        public string ResponseJson { get; set; } = """{"success":true,"data":null}""";
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is not null
                ? await request.Content.ReadAsStringAsync(cancellationToken)
                : null;

            return new HttpResponseMessage(StatusCode)
            {
                Content = new StringContent(ResponseJson, Encoding.UTF8, "application/json"),
            };
        }
    }

    private static (DefaultTreeActionHandler<StringTreeNode> Handler, StubHttpHandler Stub) CreateHandler()
    {
        var stub = new StubHttpHandler();
        var client = new DiberyTreeApiClient<StringTreeNode>(
            new HttpClient(stub) { BaseAddress = new Uri("https://test/") });
        return (new DefaultTreeActionHandler<StringTreeNode>(client), stub);
    }

    private static StringTreeNode Node(string id, string? parentId = null, int sortOrder = 0)
        => new() { Id = id, Name = id, ParentId = parentId, SortOrder = sortOrder };

    private static string OkDataJson(string json) => $$"""{"success":true,"data":{{json}}}""";

    // ============================================================
    // CreateChildAsync
    // ============================================================

    [Fact]
    public async Task CreateChildAsync_PostsDto_WithParentId_AndReturnsValue()
    {
        var (handler, stub) = CreateHandler();
        stub.ResponseJson = OkDataJson(
            """{"id":"new-id","text":"子","value":{"id":"new-id","name":"子"}}""");

        var parent = Node("p1");
        var child = Node("", parentId: "p1");
        child.Name = "子";

        var result = await handler.CreateChildAsync(parent, child);

        Assert.NotNull(result);
        Assert.Equal("new-id", result!.Id);
        Assert.Equal(HttpMethod.Post, stub.Request!.Method);
        Assert.Equal("https://test/StringTreeNode", stub.Request.RequestUri!.ToString());

        var body = JsonDocument.Parse(stub.RequestBody!);
        Assert.Equal("p1", body.RootElement.GetProperty("parentId").GetString());
        Assert.Equal("子", body.RootElement.GetProperty("value").GetProperty("name").GetString());
    }

    // ============================================================
    // UpdateNodeAsync
    // ============================================================

    [Fact]
    public async Task UpdateNodeAsync_PutsToNodeUrl()
    {
        var (handler, stub) = CreateHandler();
        stub.ResponseJson = OkDataJson("""{"id":"n1","value":{"id":"n1","name":"新名"}}""");

        var result = await handler.UpdateNodeAsync(Node("n1", parentId: "p9"));

        Assert.NotNull(result);
        Assert.Equal("新名", result!.Name);
        Assert.Equal(HttpMethod.Put, stub.Request!.Method);
        Assert.Equal("https://test/StringTreeNode/n1", stub.Request.RequestUri!.ToString());

        var body = JsonDocument.Parse(stub.RequestBody!);
        Assert.Equal("p9", body.RootElement.GetProperty("parentId").GetString());
    }

    // ============================================================
    // DeleteNodeAsync
    // ============================================================

    [Fact]
    public async Task DeleteNodeAsync_Delete_ReturnsTrue()
    {
        var (handler, stub) = CreateHandler();
        stub.ResponseJson = """{"success":true,"data":true}""";

        var ok = await handler.DeleteNodeAsync(Node("n1"));

        Assert.True(ok);
        Assert.Equal(HttpMethod.Delete, stub.Request!.Method);
        Assert.Equal("https://test/StringTreeNode/n1", stub.Request.RequestUri!.ToString());
    }

    [Fact]
    public async Task DeleteNodeAsync_NotFound_ReturnsFalse()
    {
        var (handler, stub) = CreateHandler();
        stub.StatusCode = HttpStatusCode.NotFound;
        // 404 分支不解析响应体
        stub.ResponseJson = "";

        var ok = await handler.DeleteNodeAsync(Node("missing"));

        Assert.False(ok);
    }

    // ============================================================
    // MoveNodeAsync
    // ============================================================

    [Fact]
    public async Task MoveNodeAsync_PostsMoveRoute_WithNodeAndParentIds()
    {
        var (handler, stub) = CreateHandler();
        stub.ResponseJson = """{"success":true,"data":true}""";

        var ok = await handler.MoveNodeAsync(Node("n1"), Node("p2"));

        Assert.True(ok);
        Assert.Equal(HttpMethod.Post, stub.Request!.Method);
        Assert.Equal("/StringTreeNode/move", stub.Request.RequestUri!.AbsolutePath);
        var query = System.Web.HttpUtility.ParseQueryString(stub.Request.RequestUri.Query);
        Assert.Equal("n1", query["nodeId"]);
        Assert.Equal("p2", query["newParentId"]);
    }

    [Fact]
    public async Task MoveNodeAsync_ToRoot_OmitsNewParentId()
    {
        var (handler, stub) = CreateHandler();
        stub.ResponseJson = """{"success":true,"data":true}""";

        var ok = await handler.MoveNodeAsync(Node("n1"), newParent: null);

        Assert.True(ok);
        var query = System.Web.HttpUtility.ParseQueryString(stub.Request!.RequestUri!.Query);
        Assert.Equal("n1", query["nodeId"]);
        Assert.Null(query["newParentId"]);
    }

    // ============================================================
    // SortChildrenAsync
    // ============================================================

    [Fact]
    public async Task SortChildrenAsync_PostsChildrenInGivenOrder_IgnoringOldSortOrder()
    {
        var (handler, stub) = CreateHandler();

        var parent = Node("p1");
        // 旧 SortOrder：a=0、b=1；对话框拖拽后的新顺序为 [b, a]
        var a = Node("a", parentId: "p1", sortOrder: 0);
        var b = Node("b", parentId: "p1", sortOrder: 1);

        var ok = await handler.SortChildrenAsync(parent, [b, a]);

        Assert.True(ok);
        Assert.Equal(HttpMethod.Post, stub.Request!.Method);
        Assert.Equal("https://test/StringTreeNode/children", stub.Request.RequestUri!.ToString());

        var body = JsonDocument.Parse(stub.RequestBody!);
        var children = body.RootElement.GetProperty("children");
        Assert.Equal(2, children.GetArrayLength());

        var first = children[0];
        var second = children[1];
        // 列表位置即新顺序：b 在前且重编号为 0，a 在后重编号为 1
        Assert.Equal("b", first.GetProperty("id").GetString());
        Assert.Equal(0, first.GetProperty("sortOrder").GetInt32());
        Assert.Equal("a", second.GetProperty("id").GetString());
        Assert.Equal(1, second.GetProperty("sortOrder").GetInt32());
        Assert.Equal("p1", first.GetProperty("parentId").GetString());
    }
}
```

## 文件 7/10 TreeGraph.Blazor.Shared.Tests/Services/MessageServiceTests.cs

```csharp
using Moq;
using MudBlazor;
using TreeGraph.Blazor.Shared.Trees.Dialogs;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.Services;

/// <summary>
/// MessageService 单元测试。
/// Mock IDialogService + ISnackbar，覆盖通知与确认框方法。
/// 注意：MudBlazor 9.11 中 ISnackbar.Add 为 4 参（末参 key 可空），
/// SnackbarOptions.OnClick 为 Func&lt;Snackbar, Task&gt;。
/// </summary>
public class MessageServiceTests
{
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly Mock<ISnackbar> _snackbar = new();
    private readonly MessageService _sut;

    public MessageServiceTests()
    {
        _sut = new MessageService(_dialogService.Object, _snackbar.Object);
    }

    // ============================================================
    // Success / Warning / Info / Error：Snackbar.Add 调用
    // ============================================================

    [Fact]
    public void Success_CallsSnackbarWithCorrectSeverity()
    {
        _sut.Success("操作成功");

        _snackbar.Verify(s => s.Add(
                "操作成功",
                Severity.Success,
                It.IsAny<Action<SnackbarOptions>>(),
                It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public void Success_NullMessage_UsesFallback()
    {
        _sut.Success(null);

        _snackbar.Verify(s => s.Add(
                "没有信息。",
                Severity.Success,
                It.IsAny<Action<SnackbarOptions>>(),
                It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public void Warning_CallsSnackbarWithWarningSeverity()
    {
        _sut.Warning("警告");

        _snackbar.Verify(s => s.Add(
                "警告",
                Severity.Warning,
                It.IsAny<Action<SnackbarOptions>>(),
                It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public void Warning_NullMessage_UsesFallback()
    {
        _sut.Warning(null);

        _snackbar.Verify(s => s.Add(
                "没有信息。",
                Severity.Warning,
                It.IsAny<Action<SnackbarOptions>>(),
                It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public void Info_CallsSnackbarWithInfoSeverity()
    {
        _sut.Info("信息");

        _snackbar.Verify(s => s.Add(
                "信息",
                Severity.Info,
                It.IsAny<Action<SnackbarOptions>>(),
                It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public void Error_CallsSnackbarWithErrorSeverity()
    {
        _sut.Error("错误信息");

        _snackbar.Verify(s => s.Add(
                "错误信息",
                Severity.Error,
                It.IsAny<Action<SnackbarOptions>>(),
                It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public void Error_NullMessage_UsesFallback()
    {
        _sut.Error(null);

        _snackbar.Verify(s => s.Add(
                "没有信息。",
                Severity.Error,
                It.IsAny<Action<SnackbarOptions>>(),
                It.IsAny<string?>()),
            Times.Once);
    }

    // ============================================================
    // Details：Error + Action "查看" + OnClick 回调
    // ============================================================

    [Fact]
    public void Details_CallsSnackbarWithErrorAndAction()
    {
        SnackbarOptions? captured = null;

        _snackbar
            .Setup(s => s.Add(
                It.IsAny<string>(),
                It.IsAny<Severity>(),
                It.IsAny<Action<SnackbarOptions>>(),
                It.IsAny<string?>()))
            .Callback<string, Severity, Action<SnackbarOptions>, string?>(
                (_, _, configure, _) =>
                {
                    var opts = new SnackbarOptions(Severity.Normal, new SnackbarConfiguration());
                    configure(opts);
                    captured = opts;
                });

        _sut.Details("加载失败", "详细错误堆栈");

        Assert.NotNull(captured);
        Assert.Equal("查看", captured!.Action);
        Assert.Equal(Color.Info, captured.ActionColor);
        Assert.NotNull(captured.OnClick);
    }

    [Fact]
    public async Task Details_OnClickAction_ShowsMessageBox()
    {
        SnackbarOptions? captured = null;
        _snackbar
            .Setup(s => s.Add(
                It.IsAny<string>(),
                It.IsAny<Severity>(),
                It.IsAny<Action<SnackbarOptions>>(),
                It.IsAny<string?>()))
            .Callback<string, Severity, Action<SnackbarOptions>, string?>(
                (_, _, configure, _) =>
                {
                    var opts = new SnackbarOptions(Severity.Normal, new SnackbarConfiguration());
                    configure(opts);
                    captured = opts;
                });

        _sut.Details("加载失败", "详细错误堆栈");

        // MudBlazor 9.11：OnClick 为 Func<Snackbar, Task>，lambda 忽略入参，传 null 即可
        await captured!.OnClick!(null!);

        _dialogService.Verify(s => s.ShowMessageBoxAsync(
                "加载失败",
                "详细错误堆栈",
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<DialogOptions>()),
            Times.Once);
    }

    // ============================================================
    // BoolBoxAsync（result != null 判定）
    // ============================================================

    [Fact]
    public async Task BoolBoxAsync_UserConfirms_ReturnsTrue()
    {
        _dialogService
            .Setup(s => s.ShowMessageBoxAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<DialogOptions>()))
            .ReturnsAsync(true);

        var result = await _sut.BoolBoxAsync("确认删除？", "请确认");

        Assert.True(result);
    }

    [Fact]
    public async Task BoolBoxAsync_UserCancels_ReturnsFalse()
    {
        _dialogService
            .Setup(s => s.ShowMessageBoxAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<DialogOptions>()))
            .ReturnsAsync((bool?)null);

        var result = await _sut.BoolBoxAsync("确认删除？", "请确认");

        Assert.False(result);
    }

    // ============================================================
    // DeleteBox（result ?? false 判定）
    // ============================================================

    [Fact]
    public async Task DeleteBox_UserConfirms_ReturnsTrue()
    {
        _dialogService
            .Setup(s => s.ShowMessageBoxAsync(
                It.IsAny<MessageBoxOptions>(),
                It.IsAny<DialogOptions>()))
            .ReturnsAsync(true);

        var result = await _sut.DeleteBox("确认删除？", "删除");

        Assert.True(result);
    }

    [Fact]
    public async Task DeleteBox_UserCancels_ReturnsFalse()
    {
        _dialogService
            .Setup(s => s.ShowMessageBoxAsync(
                It.IsAny<MessageBoxOptions>(),
                It.IsAny<DialogOptions>()))
            .ReturnsAsync((bool?)null);

        var result = await _sut.DeleteBox("确认删除？", "删除");

        Assert.False(result);
    }
}
```

## 文件 8/10 TreeGraph.Blazor.Shared.Tests/Services/TreeNodeDialogServiceTests.cs

```csharp
using Microsoft.AspNetCore.Components;
using Moq;
using MudBlazor;
using TreeGraph.Blazor.Shared.Trees;
using TreeGraph.Blazor.Shared.Trees.Contracts;
using TreeGraph.Blazor.Shared.Trees.Dialogs;
using TreeGraph.Blazor.Shared.Trees.Nodes;
using TreeGraph.Blazor.Shared.Trees.Models;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.Services;

/// <summary>
/// TreeNodeDialogService 单元测试。
/// Mock IDialogService，验证对话框参数传递与结果提取（含取消语义）。
///
/// 注意：
/// - MudBlazor 9.11 的 DialogResult 无公共构造函数，取消时 Cancel() 实际返回 null。
/// - ShowExAsync 是扩展方法，经由非泛型 IDialogService.ShowAsync(Type,...) 落地，故 mock 该重载。
/// </summary>
public class TreeNodeDialogServiceTests
{
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly BlazorService _blazorService = new();
    private readonly TreeNodeDialogService<StringTreeNode> _sut;

    public TreeNodeDialogServiceTests()
    {
        _sut = new TreeNodeDialogService<StringTreeNode>(
            _dialogService.Object, _blazorService);
    }

    // ============================================================
    // 辅助
    // ============================================================

    private void SetupDialogResult<TDialog>(DialogResult? result)
        where TDialog : IComponent
    {
        var dialogRef = new Mock<IDialogReference>();
        dialogRef.SetupGet(r => r.Result).Returns(Task.FromResult(result)!);

        _dialogService
            .Setup(s => s.ShowAsync<TDialog>(
                It.IsAny<string>(),
                It.IsAny<DialogParameters>(),
                It.IsAny<DialogOptions>()))
            .ReturnsAsync(dialogRef.Object);
    }

    /// <summary>ShowExAsync 扩展方法最终走非泛型 ShowAsync(Type,...)，在此 mock。</summary>
    private void SetupExDialogResult<TDialog>(DialogResult? result)
        where TDialog : IComponent
    {
        var dialogRef = new Mock<IDialogReference>();
        dialogRef.SetupGet(r => r.Result).Returns(Task.FromResult(result)!);

        _dialogService
            .Setup(s => s.ShowAsync(
                typeof(TDialog),
                It.IsAny<string>(),
                It.IsAny<DialogParameters>(),
                It.IsAny<DialogOptions>()))
            .ReturnsAsync(dialogRef.Object);
    }

    private static StringTreeNode MakeNode(string id, string? parentId = null) =>
        new() { Id = id, Name = $"Node-{id}", ParentId = parentId };

    private static NodeTemplate<StringTreeNode> MakeTemplate(string id) =>
        new() { Node = new TreeItemData<StringTreeNode> { Value = MakeNode(id) } };

    // ============================================================
    // ShowActionsDialogAsync
    // ============================================================

    [Fact]
    public async Task ShowActionsDialogAsync_Canceled_ReturnsNull()
    {
        SetupDialogResult<TreeNodeActionsDialog<StringTreeNode>>(DialogResult.Cancel());

        var result = await _sut.ShowActionsDialogAsync(MakeTemplate("1"));

        Assert.Null(result);
    }

    [Fact]
    public async Task ShowActionsDialogAsync_ReturnsActionResult()
    {
        var expected = new NodeActionResult<StringTreeNode>
        {
            Action = NodeAction.Edit,
            Node = MakeNode("1"),
        };
        SetupDialogResult<TreeNodeActionsDialog<StringTreeNode>>(DialogResult.Ok(expected));

        var result = await _sut.ShowActionsDialogAsync(MakeTemplate("1"));

        Assert.NotNull(result);
        Assert.Equal(NodeAction.Edit, result!.Action);
        Assert.Equal("1", result.Node.Id);
    }

    // ============================================================
    // ShowCreateDialogAsync
    // ============================================================

    [Fact]
    public async Task ShowCreateDialogAsync_Canceled_ReturnsNull()
    {
        SetupDialogResult<TreeNodeEditDialog<StringTreeNode>>(DialogResult.Cancel());

        var result = await _sut.ShowCreateDialogAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task ShowCreateDialogAsync_ReturnsNode()
    {
        SetupDialogResult<TreeNodeEditDialog<StringTreeNode>>(
            DialogResult.Ok(MakeNode("new-1")));

        var result = await _sut.ShowCreateDialogAsync();

        Assert.NotNull(result);
        Assert.Equal("new-1", result!.Id);
    }

    // ============================================================
    // ShowEditDialogAsync
    // ============================================================

    [Fact]
    public async Task ShowEditDialogAsync_Canceled_ReturnsNull()
    {
        SetupDialogResult<TreeNodeEditDialog<StringTreeNode>>(DialogResult.Cancel());

        var result = await _sut.ShowEditDialogAsync(MakeTemplate("1"));

        Assert.Null(result);
    }

    [Fact]
    public async Task ShowEditDialogAsync_ReturnsNode()
    {
        SetupDialogResult<TreeNodeEditDialog<StringTreeNode>>(
            DialogResult.Ok(MakeNode("1")));

        var result = await _sut.ShowEditDialogAsync(MakeTemplate("1"));

        Assert.NotNull(result);
        Assert.Equal("1", result!.Id);
    }

    // ============================================================
    // ShowViewDialogAsync（返回 bool；取消时 Result 为 null）
    // ============================================================

    [Fact]
    public async Task ShowViewDialogAsync_Ok_ReturnsTrue()
    {
        SetupDialogResult<TreeNodeViewDialog<StringTreeNode>>(DialogResult.Ok(true));

        var result = await _sut.ShowViewDialogAsync(MakeNode("1"));

        Assert.True(result);
    }

    [Fact]
    public async Task ShowViewDialogAsync_Canceled_ReturnsFalse()
    {
        SetupDialogResult<TreeNodeViewDialog<StringTreeNode>>(DialogResult.Cancel());

        var result = await _sut.ShowViewDialogAsync(MakeNode("1"));

        Assert.False(result);
    }

    // ============================================================
    // ShowSortDialogAsync
    // ============================================================

    [Fact]
    public async Task ShowSortDialogAsync_Canceled_ReturnsNull()
    {
        SetupDialogResult<TreeNodeSortDialog<StringTreeNode>>(DialogResult.Cancel());

        var result = await _sut.ShowSortDialogAsync(
            new TreeItemData<StringTreeNode> { Value = MakeNode("1") });

        Assert.Null(result);
    }

    [Fact]
    public async Task ShowSortDialogAsync_ReturnsSortedList()
    {
        var sorted = new List<StringTreeNode> { MakeNode("2"), MakeNode("1") };
        SetupDialogResult<TreeNodeSortDialog<StringTreeNode>>(DialogResult.Ok(sorted));

        var result = await _sut.ShowSortDialogAsync(
            new TreeItemData<StringTreeNode> { Value = MakeNode("1") });

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
        Assert.Equal("2", result[0].Id);
    }

    // ============================================================
    // ShowParentSelectDialogAsync（List 版本，泛型 ShowAsync）
    // ============================================================

    [Fact]
    public async Task ShowParentSelectDialogAsync_ListVersion_ReturnsResult()
    {
        var expected = new ParentSelectResult<StringTreeNode>
        {
            IsConfirmed = true,
            SelectedParent = MakeNode("parent-2"),
            SelectedPath = ["parent-2"],
        };
        SetupDialogResult<TreeNodeParentSelectDialog<StringTreeNode>>(DialogResult.Ok(expected));

        var result = await _sut.ShowParentSelectDialogAsync(
            [MakeNode("1")], currentNode: MakeNode("1"));

        Assert.NotNull(result);
        Assert.True(result!.IsConfirmed);
        Assert.Equal("parent-2", result.SelectedParent?.Id);
    }

    [Fact]
    public async Task ShowParentSelectDialogAsync_ListVersion_Canceled_ReturnsNull()
    {
        SetupDialogResult<TreeNodeParentSelectDialog<StringTreeNode>>(DialogResult.Cancel());

        var result = await _sut.ShowParentSelectDialogAsync([MakeNode("1")]);

        Assert.Null(result);
    }

    // ============================================================
    // ShowExAsync 路径（TreeSelectDialogSky / TreeDialogPageSky）
    // ============================================================

    [Fact]
    public async Task ShowDialogPageAsync_Ok_ReturnsNode()
    {
        SetupExDialogResult<TreeDialogPageSky<StringTreeNode>>(
            DialogResult.Ok(MakeNode("reg-1")));

        var result = await _sut.ShowDialogPageAsync();

        Assert.Equal("reg-1", result?.Id);
    }

    [Fact]
    public async Task ShowTreeSelectDialogAsync_Ok_ReturnsNode()
    {
        SetupExDialogResult<TreeSelectDialogSky<StringTreeNode>>(
            DialogResult.Ok(MakeNode("pick-1")));

        var result = await _sut.ShowTreeSelectDialogAsync();

        Assert.Equal("pick-1", result?.Id);
    }

    [Fact]
    public async Task ShowParentSelectDialogAsync_SingleNode_SameParent_ReturnsNull()
    {
        var currentParent = MakeNode("parent-1");
        var node = new TreeItemData<StringTreeNode>
        {
            Value = new StringTreeNode
            {
                Id = "child-1",
                Name = "Child",
                ParentId = "parent-1",
                Parent = currentParent,
            }
        };
        // 选中的节点 Id 与当前 ParentId 相同 → 无变化
        SetupExDialogResult<TreeSelectDialogSky<StringTreeNode>>(
            DialogResult.Ok(MakeNode("parent-1")));

        var result = await _sut.ShowParentSelectDialogAsync(node);

        Assert.Null(result);
    }

    // ============================================================
    // ExecuteNodeOperationAsync（便捷方法）
    // ============================================================

    [Fact]
    public async Task ExecuteNodeOperationAsync_Canceled_ReturnsNull()
    {
        SetupDialogResult<TreeNodeActionsDialog<StringTreeNode>>(DialogResult.Cancel());

        var result = await _sut.ExecuteNodeOperationAsync(MakeTemplate("1"));

        Assert.Null(result);
    }

    [Fact]
    public async Task ExecuteNodeOperationAsync_ReturnsOutcome()
    {
        var actionResult = new NodeActionResult<StringTreeNode>
        {
            Action = NodeAction.Delete,
            Node = MakeNode("1"),
        };
        SetupDialogResult<TreeNodeActionsDialog<StringTreeNode>>(DialogResult.Ok(actionResult));

        var result = await _sut.ExecuteNodeOperationAsync(MakeTemplate("1"));

        Assert.NotNull(result);
        Assert.Equal(NodeAction.Delete, result!.Action);
        Assert.Equal("1", result.Node.Id);
    }
}
```

## 文件 9/10 TreeGraph.Blazor.Shared.Tests/TreeHelperIterationTests.cs

```csharp
using MudBlazor;
using TreeGraph.Blazor.Shared.Trees;
using TreeGraph.Blazor.Shared.Trees.Models;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests;

/// <summary>
/// 迭代遍历的深树安全性与防环测试（原递归实现在此规模会 StackOverflowException）。
/// </summary>
public class TreeHelperIterationTests
{
    private const int Depth = 10_000;

    /// <summary>构造 1→2→…→Depth 的单链，子集合在构造期就全部挂好。</summary>
    private static List<TreeItemData<StringTreeNode>> BuildChain(int depth)
    {
        TreeItemData<StringTreeNode>? leaf = null;

        for (int i = depth; i >= 1; i--)
        {
            var node = new TreeItemData<StringTreeNode>
            {
                Value = new StringTreeNode { Id = i.ToString(), Name = $"N{i}" },
                Children = leaf is null ? null : [leaf],
            };
            leaf = node;
        }

        return [leaf!];
    }

    [Fact]
    public void FindTreeItem_DeepChain_DoesNotStackOverflow()
    {
        var roots = BuildChain(Depth);

        var found = roots.FindTreeItem(Depth.ToString());

        Assert.Equal(Depth.ToString(), found?.Value?.Id);
    }

    [Fact]
    public void GetPathToNode_DeepChain_ReturnsFullPath()
    {
        var roots = BuildChain(Depth);

        var path = roots.GetPathToNode(Depth.ToString());

        Assert.NotNull(path);
        Assert.Equal(Depth, path!.Count);
        Assert.Equal("1", path.First());
        Assert.Equal(Depth.ToString(), path.Last());
    }

    [Fact]
    public void GetPathToNode_CyclicGraph_DoesNotInfiniteLoop()
    {
        // 手工构造环：a ↔ b
        var a = new TreeItemData<StringTreeNode>
        {
            Value = new StringTreeNode { Id = "a", Name = "A" },
        };
        var b = new TreeItemData<StringTreeNode>
        {
            Value = new StringTreeNode { Id = "b", Name = "B" },
            Children = [a],
        };
        a.Children = [b];

        var path = new List<TreeItemData<StringTreeNode>> { a }.GetPathToNode("b");

        Assert.Equal(["a", "b"], path);
    }
}
```

## 文件 10/10 TreeGraph.Blazor.Shared.Tests/TreeHelperTests.cs

```csharp
using MudBlazor;
using TreeGraph.Blazor.Shared.Trees;
using TreeGraph.Blazor.Shared.Trees.Models;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests;

/// <summary>
/// TreeHelper 扩展方法单元测试。
/// 覆盖：ToTreeItemData / FindTreeItem / GetPathToNode / ExpandAsync / ExpandToNodeAsync。
/// </summary>
public class TreeHelperTests
{
    // ============================================================
    // 测试数据辅助
    // ============================================================

    private static StringTreeNode Node(
        string id, string? parentId = null, string? name = null)
        => new() { Id = id, Name = name ?? $"Node-{id}", ParentId = parentId };

    private static TreeNodeDto<StringTreeNode> Dto(
        string id,
        string? parentId = null,
        string? text = null,
        bool hasChildren = false,
        int sortOrder = 0)
        => new()
        {
            Id = id,
            Text = text ?? $"Text-{id}",
            ParentId = parentId,
            SortOrder = sortOrder,
            HasChildren = hasChildren,
            Value = Node(id, parentId),
        };

    // ============================================================
    // ToTreeItemData
    // ============================================================

    [Fact]
    public void ToTreeItemData_MapsAllScalarFields()
    {
        var dto = Dto("1", text: "Hello", hasChildren: true, sortOrder: 5);

        var item = dto.ToTreeItemData<StringTreeNode>();

        Assert.Equal("1", item.Value?.Id);
        Assert.Equal("Hello", item.Text);
        Assert.True(item.Expandable);
        Assert.Equal(TreeHelper.TreeItemIcons, item.Icon);
    }

    [Fact]
    public void ToTreeItemData_WithNullValue_DoesNotThrow()
    {
        var dto = new TreeNodeDto<StringTreeNode>
        {
            Id = "1",
            Text = "Orphan",
            Value = null,   // ★ 关键：Value 为 null
        };

        // 修复前会 NRE；现在应该正常返回
        var item = dto.ToTreeItemData<StringTreeNode>();

        Assert.Null(item.Value);
        Assert.Equal("Orphan", item.Text);
    }

    [Fact]
    public void ToTreeItemData_WithChildren_Recurses()
    {
        var childDto = Dto("2", parentId: "1");
        var dto = new TreeNodeDto<StringTreeNode>
        {
            Id = "1",
            Text = "Parent",
            Value = Node("1"),
            Children = [childDto],
        };

        var item = dto.ToTreeItemData<StringTreeNode>();

        Assert.NotNull(item.Children);
        var child = Assert.Single(item.Children);
        Assert.Equal("2", child.Value?.Id);
    }

    [Fact]
    public void ToTreeItemData_SetsParentOnValue()
    {
        var parent = Node("p1");
        var dto = new TreeNodeDto<StringTreeNode>
        {
            Id = "1",
            Text = "Child",
            Value = Node("1", "p1"),
            Parent = parent,
        };

        var item = dto.ToTreeItemData<StringTreeNode>();

        Assert.Same(parent, item.Value?.Parent);
    }

    // ============================================================
    // FindTreeItem
    // ============================================================

    [Fact]
    public void FindTreeItem_DirectChild_Found()
    {
        var items = new List<TreeItemData<StringTreeNode>>
        {
            new() { Value = Node("1") },
            new() { Value = Node("2") },
        };

        var result = items.FindTreeItem("2");

        Assert.NotNull(result);
        Assert.Equal("2", result!.Value?.Id);
    }

    [Fact]
    public void FindTreeItem_DeepChild_Found()
    {
        var grandChild = new TreeItemData<StringTreeNode> { Value = Node("3") };
        var child = new TreeItemData<StringTreeNode>
        {
            Value = Node("2"),
            Children = [grandChild],
        };
        var root = new TreeItemData<StringTreeNode>
        {
            Value = Node("1"),
            Children = [child],
        };

        var result = new List<TreeItemData<StringTreeNode>> { root }.FindTreeItem("3");

        Assert.NotNull(result);
        Assert.Equal("3", result!.Value?.Id);
    }

    [Fact]
    public void FindTreeItem_NotFound_ReturnsNull()
    {
        var items = new List<TreeItemData<StringTreeNode>>
        {
            new() { Value = Node("1") },
        };

        Assert.Null(items.FindTreeItem("999"));
    }

    // ============================================================
    // GetPathToNode
    // ============================================================

    [Fact]
    public void GetPathToNode_Root_ReturnsSingleElement()
    {
        var items = new List<TreeItemData<StringTreeNode>>
        {
            new() { Value = Node("1") },
        };

        var path = items.GetPathToNode("1");

        Assert.NotNull(path);
        Assert.Equal(["1"], path);
    }

    [Fact]
    public void GetPathToNode_DeepNode_ReturnsFullPath()
    {
        var grandChild = new TreeItemData<StringTreeNode> { Value = Node("3") };
        var child = new TreeItemData<StringTreeNode>
        {
            Value = Node("2"),
            Children = [grandChild],
        };
        var root = new TreeItemData<StringTreeNode>
        {
            Value = Node("1"),
            Children = [child],
        };

        var path = new List<TreeItemData<StringTreeNode>> { root }.GetPathToNode("3");

        Assert.NotNull(path);
        Assert.Equal(["1", "2", "3"], path);
    }

    [Fact]
    public void GetPathToNode_NotFound_ReturnsNull()
    {
        var items = new List<TreeItemData<StringTreeNode>>
        {
            new() { Value = Node("1") },
        };

        Assert.Null(items.GetPathToNode("999"));
    }

    // ============================================================
    // ExpandAsync（单层展开）
    // ============================================================

    [Fact]
    public async Task ExpandAsync_LoadsChildren()
    {
        var item = new TreeItemData<StringTreeNode> { Value = Node("1") };

        var loadCount = 0;
        Task<IReadOnlyCollection<TreeItemData<StringTreeNode>>> Load(StringTreeNode? _)
        {
            loadCount++;
            return Task.FromResult<IReadOnlyCollection<TreeItemData<StringTreeNode>>>(
                new List<TreeItemData<StringTreeNode>>
                {
                    new() { Value = Node("2", parentId: "1") },
                    new() { Value = Node("3", parentId: "1") },
                });
        }

        await item.ExpandAsync(Load);

        Assert.True(item.Expanded);
        Assert.Equal(1, loadCount);
        Assert.Equal(2, item.Children?.Count);
    }

    [Fact]
    public async Task ExpandAsync_AlreadyLoaded_DoesNotReload()
    {
        var existingChild = new TreeItemData<StringTreeNode> { Value = Node("2") };
        var item = new TreeItemData<StringTreeNode>
        {
            Value = Node("1"),
            Children = [existingChild],
        };

        var loadCount = 0;
        Task<IReadOnlyCollection<TreeItemData<StringTreeNode>>> Load(StringTreeNode? _)
        {
            loadCount++;
            return Task.FromResult<IReadOnlyCollection<TreeItemData<StringTreeNode>>>(
                Array.Empty<TreeItemData<StringTreeNode>>());
        }

        await item.ExpandAsync(Load);

        Assert.True(item.Expanded);
        Assert.Equal(0, loadCount);   // 未重新加载
        Assert.Single(item.Children!);
    }

    // ============================================================
    // ExpandToNodeAsync（沿路径展开）
    // ============================================================

    [Fact]
    public async Task ExpandToNodeAsync_ExpandsAlongPath()
    {
        // 预置：根节点
        var root = new TreeItemData<StringTreeNode> { Value = Node("1") };
        var items = new List<TreeItemData<StringTreeNode>> { root };

        // 模拟 API：加载 "1" 的子节点 → 返回 "2"；加载 "2" 的子节点 → 返回 "3"
        Task<IReadOnlyCollection<TreeItemData<StringTreeNode>>> Load(
            StringTreeNode? parent)
        {
            var result = parent?.Id switch
            {
                "1" => new List<TreeItemData<StringTreeNode>>
                {
                    new() { Value = Node("2", parentId: "1") },
                },
                "2" => new List<TreeItemData<StringTreeNode>>
                {
                    new() { Value = Node("3", parentId: "2") },
                },
                _ => new List<TreeItemData<StringTreeNode>>(),
            };
            return Task.FromResult<IReadOnlyCollection<TreeItemData<StringTreeNode>>>(result);
        }

        StringTreeNode? selected = null;
        await items.ExpandToNodeAsync(
            path: ["1", "2", "3"],
            loadChildren: Load,
            onSelected: v => selected = v);

        Assert.NotNull(selected);
        Assert.Equal("3", selected!.Id);

        // 沿路径的节点应都被展开
        Assert.True(root.Expanded);
        var level2 = Assert.Single(root.Children!);
        Assert.Equal("2", level2.Value?.Id);
        Assert.True(level2.Expanded);
    }
}
```

