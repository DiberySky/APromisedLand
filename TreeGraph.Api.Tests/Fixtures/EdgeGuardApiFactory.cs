using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TreeGraph.Api.NodeEavSky.Services;
using TreeGraph.Api.TreeEavSky.Services;
using Xunit;

namespace TreeGraph.Api.Tests.Fixtures;

/// <summary>
/// 在 <see cref="EavApiFactory"/>（外部模式，移除全部宿主守卫）基础上，
/// 重新注册单位树守卫，用于验证 EdgeSky 起点/终点节点
/// "必须是 Inode 宿主"校验在生产 DI 装配下的端到端行为。
/// </summary>
public class EdgeGuardApiFactory : EavApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            // 基类已 RemoveAll（外部模式）；此处在其之后重新注册，覆盖单位树
            services.RemoveAll<IInodeOwnerGuard>();
            services.AddScoped<IInodeOwnerGuard, UnitTreeInodeGuard>();
        });
    }
}

[CollectionDefinition("EdgeGuard")]
public class EdgeGuardCollection : ICollectionFixture<EdgeGuardApiFactory> { }
