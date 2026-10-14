using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using TreeGraph.Api.NodeEavSky.Services;
using Xunit;

namespace TreeGraph.Api.Tests.Fixtures;

/// <summary>
/// 启动一个真实 PostgreSQL 容器 + 完整 WebApplication。
/// 全测试集合共享此 fixture（见 IntegrationCollection）。
/// </summary>
public class EavApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("TreeGraphDb")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:TreeGraphDb", _postgres.GetConnectionString());
        builder.UseEnvironment("Testing");

        // iNode 测试使用任意 inodeId，不依赖树节点主表；
        // 移除树模块注册的宿主守卫，使 inode 链路处于"外部模式"（无守卫放行）。
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IInodeOwnerGuard>();
        });
    }
}

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<EavApiFactory> { }
