using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor.Services;
using TreeGraph.Blazor.Shared.Trees.StringTree.Extensions;
using TreeGraph.Blazor.Shared.Trees.StringTree.Models;
using TreeGraph.Blazor.Shared.Trees.StringTree.Services;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.Extensions;

/// <summary>
/// AddLegacyTreeSky DI 冒烟：
///   - 默认注册 NoopStringTreeActionHandler
///   - 宿主可后注册覆盖
///   - 生命周期 Scoped
///   - StringTreeDialogService 可解析
/// </summary>
public class StringTreeServiceCollectionExtensionsTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMudServices();
        services.AddLegacyTreeSky();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    [Fact]
    public void AddLegacyTreeSky_ResolvesDefaultNoopHandler()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var handler = scope.ServiceProvider
            .GetRequiredService<IStringTreeActionHandler>();

        Assert.IsType<NoopStringTreeActionHandler>(handler);
    }

    [Fact]
    public void AddLegacyTreeSky_ResolvesDialogService()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var svc = scope.ServiceProvider.GetService<StringTreeDialogService>();
        Assert.NotNull(svc);
    }

    [Fact]
    public void AddLegacyTreeSky_HostRegistrationAfterCall_OverridesDefault()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLegacyTreeSky();

        // 宿主覆盖
        var mock = new Mock<IStringTreeActionHandler>().Object;
        services.AddScoped<IStringTreeActionHandler>(_ => mock);

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        var resolved = scope.ServiceProvider
            .GetRequiredService<IStringTreeActionHandler>();

        Assert.Same(mock, resolved);
    }

    [Fact]
    public void AddLegacyTreeSky_DefaultHandler_IsScoped()
    {
        using var provider = BuildProvider();

        using var scopeA = provider.CreateScope();
        var a1 = scopeA.ServiceProvider.GetRequiredService<IStringTreeActionHandler>();
        var a2 = scopeA.ServiceProvider.GetRequiredService<IStringTreeActionHandler>();
        Assert.Same(a1, a2);

        using var scopeB = provider.CreateScope();
        var b = scopeB.ServiceProvider.GetRequiredService<IStringTreeActionHandler>();
        Assert.NotSame(a1, b);
    }
}
