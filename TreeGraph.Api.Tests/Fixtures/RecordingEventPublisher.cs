using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TreeGraph.Api.MessagingSky;
using Xunit;

namespace TreeGraph.Api.Tests.Fixtures;

/// <summary>记录所有发布事件的空桩（替代 Noop，供事件断言使用）。</summary>
public sealed class RecordingEventPublisher : IEventPublisher
{
    private readonly ConcurrentQueue<object> _events = new();

    public Task Publish<T>(T message, CancellationToken ct = default)
    {
        _events.Enqueue(message!);
        return Task.CompletedTask;
    }

    public IReadOnlyList<T> Of<T>() => _events.OfType<T>().ToList();

    public void Clear() => _events.Clear();
}

/// <summary>
/// 在 <see cref="EavApiFactory"/> 基础上把 IEventPublisher 换成记录器，
/// 用于验证「结构变更 → EdgeChangedEvent」（R7：段边直写 edges 表，走边事件链）。
/// </summary>
public class RecordingApiFactory : EavApiFactory
{
    public RecordingEventPublisher Publisher { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEventPublisher>();
            services.AddSingleton<IEventPublisher>(Publisher);
        });
    }
}

[CollectionDefinition("LineStructureEvents")]
public class LineStructureEventsCollection : ICollectionFixture<RecordingApiFactory> { }
