using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using TreeGraph.Blazor.Shared.Trees.TreeSky.Extensions;
using TreeGraph.Blazor.Shared.Trees.TreeSky.Models;
using TreeGraph.Blazor.Shared.Trees.TreeSky.Services;
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
