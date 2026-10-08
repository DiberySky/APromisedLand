using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using TreeGraph.FileStorageApi.Data;
using TreeGraph.FileStorageApi.Security;
using TreeGraph.FileStorageApi.Storage;
using Testcontainers.PostgreSql;
using Xunit;

namespace TreeGraph.FileStorageApi.Tests.Fixtures;

/// <summary>
/// 启动真实 PostgreSQL 容器 + 完整 WebApplication。
/// - 替换 IObjectStorage 为内存实现（S3 行为模拟，含 Range / 故障注入）
/// - 替换 ICallerContext 为可变 TestCallerContext（scoped，租户可注入）
/// - 关闭 UploadSessionCleanupService（后台清理会与测试种子数据竞争）
/// - 显式执行 EF 迁移（Program.cs 仅在 Development 环境 MigrateAsync）
/// 全集成测试集合共享此 fixture。
/// </summary>
public sealed class FileStorageApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("FileMetadataDb")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly InMemoryObjectStorage _storage = new();

    private bool _disposed;

    public InMemoryObjectStorage Storage => _storage;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _ = Services;   // 强制宿主构建并启动（ValidateOnStart 在此生效）

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FileStorageContext>();
        await db.Database.MigrateAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await base.DisposeAsync();      // 先停宿主
        await _postgres.DisposeAsync(); // 再释放容器
    }

    // xunit 2.x 的 IAsyncLifetime.DisposeAsync 返回 Task，
    // 与 WebApplicationFactory.DisposeAsync（ValueTask）签名冲突，显式实现桥接。
    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:FileMetadataDb", _postgres.GetConnectionString());
        builder.UseSetting("UploadCleanup:Enabled", "false");

        // 清空默认日志提供程序（含 Windows EventLog）。
        // 否则集合清理阶段宿主停机时，AuditWriterService.StopAsync 的 LogWarning
        // 会命中已释放的 EventLogInternal，抛 ObjectDisposedException 导致
        // "Test Collection Cleanup Failure"。
        builder.ConfigureLogging(logging => logging.ClearProviders());

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IObjectStorage>();
            services.AddSingleton(_storage);
            services.AddSingleton<IObjectStorage>(sp =>
                sp.GetRequiredService<InMemoryObjectStorage>());

            services.RemoveAll<ICallerContext>();
            services.AddScoped<TestCallerContext>();
            services.AddScoped<ICallerContext>(sp =>
                sp.GetRequiredService<TestCallerContext>());
        });
    }
}

[CollectionDefinition("FileStorageIntegration")]
public sealed class FileStorageIntegrationCollection : ICollectionFixture<FileStorageApiFactory> { }
