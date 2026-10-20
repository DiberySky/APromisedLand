using MassTransit;

namespace TreeGraph.Api.Tests.Fixtures;

/// <summary>
/// 无操作 IPublishEndpoint 桩：服务单元测试直接 new 服务实例时注入，
/// 发布消息即丢弃（不关心事件投递，仅满足构造签名）。
/// </summary>
public sealed class NoopPublishEndpoint : IPublishEndpoint
{
    public Task Publish<T>(T message, CancellationToken cancellationToken = default) where T : class
        => Task.CompletedTask;

    public Task Publish<T>(T message, IPipe<PublishContext<T>> publishPipe,
        CancellationToken cancellationToken = default) where T : class
        => Task.CompletedTask;

    public Task Publish<T>(T message, IPipe<PublishContext> publishPipe,
        CancellationToken cancellationToken = default) where T : class
        => Task.CompletedTask;

    public Task Publish(object message, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task Publish(object message, IPipe<PublishContext> publishPipe,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task Publish(object message, Type messageType, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task Publish(object message, Type messageType, IPipe<PublishContext> publishPipe,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task Publish<T>(object values, CancellationToken cancellationToken = default) where T : class
        => Task.CompletedTask;

    public Task Publish<T>(object values, IPipe<PublishContext<T>> publishPipe,
        CancellationToken cancellationToken = default) where T : class
        => Task.CompletedTask;

    public Task Publish<T>(object values, IPipe<PublishContext> publishPipe,
        CancellationToken cancellationToken = default) where T : class
        => Task.CompletedTask;

    // 单元测试不订阅发布观察者
    public ConnectHandle ConnectPublishObserver(IPublishObserver observer)
        => throw new NotSupportedException();
}
