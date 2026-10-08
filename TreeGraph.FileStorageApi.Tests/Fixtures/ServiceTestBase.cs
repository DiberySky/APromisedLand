using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TreeGraph.FileStorageApi.Data;
using Xunit;

namespace TreeGraph.FileStorageApi.Tests.Fixtures;

/// <summary>
/// Tier 2 服务级测试基类：在一个 DI scope 内解析被测服务，
/// 测试内直接改 Caller.Tenant / Actor。每个测试前清空 DB 与内存对象存储，
/// 保证测试互相独立。
/// </summary>
[Collection("FileStorageIntegration")]
public abstract class ServiceTestBase : IDisposable
{
    protected readonly FileStorageApiFactory Factory;
    protected readonly IServiceScope Scope;
    protected readonly FileStorageContext Db;
    protected readonly TestCallerContext Caller;
    protected readonly InMemoryObjectStorage Storage;

    protected ServiceTestBase(FileStorageApiFactory factory)
    {
        Factory = factory;
        Scope   = Factory.Services.CreateScope();
        Db      = Scope.ServiceProvider.GetRequiredService<FileStorageContext>();
        Caller  = Scope.ServiceProvider.GetRequiredService<TestCallerContext>();
        Storage = Factory.Storage;
        Storage.Reset();
    }

    protected T Svc<T>() where T : notnull
        => Scope.ServiceProvider.GetRequiredService<T>();

    /// <summary>清空全部业务表，保证测试互不干扰（本集合内测试串行执行）。</summary>
    protected async Task WipeDbAsync()
    {
        await Db.Database.ExecuteSqlRawAsync("""
            DELETE FROM "UploadChunks";
            DELETE FROM "UploadSessions";
            DELETE FROM "DocumentMetadata";
            DELETE FROM "DocumentAudits";
            DELETE FROM "IndexTasks";
            """);
    }

    public void Dispose() => Scope.Dispose();
}

/// <summary>
/// Tier 3 HTTP 集成测试基类：通过 HttpClient 走完整请求管线。
/// </summary>
[Collection("FileStorageIntegration")]
public abstract class HttpTestBase
{
    protected readonly FileStorageApiFactory Factory;
    protected readonly HttpClient Client;
    protected readonly InMemoryObjectStorage Storage;

    protected HttpTestBase(FileStorageApiFactory factory)
    {
        Factory = factory;
        Client  = factory.CreateClient();
        Storage = factory.Storage;
        Storage.Reset();
    }
}
