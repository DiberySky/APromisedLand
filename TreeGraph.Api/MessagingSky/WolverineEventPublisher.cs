using Wolverine;

namespace TreeGraph.Api.MessagingSky;

/// <summary>
/// IEventPublisher 的 Wolverine 实现：委托 <see cref="IMessageBus"/> 路由发布。
/// 注意：Wolverine 6.x <c>PublishAsync</c> 无 CancellationToken 重载，
/// 接口上的 ct 仅为调用点签名兼容，不向前透传。
/// </summary>
public sealed class WolverineEventPublisher(IMessageBus bus) : IEventPublisher
{
    public Task Publish<T>(T message, CancellationToken ct = default)
        => bus.PublishAsync(message).AsTask();
}
