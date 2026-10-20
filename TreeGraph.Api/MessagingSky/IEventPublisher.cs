namespace TreeGraph.Api.MessagingSky;

/// <summary>
/// 事件发布抽象（仓库自有，隔离消息总线框架）。
/// 生产实现委托 Wolverine <c>IMessageBus</c>；测试用 Noop 空桩。
/// </summary>
public interface IEventPublisher
{
    /// <summary>发布事件（at-least-once，消费端幂等）。</summary>
    Task Publish<T>(T message, CancellationToken ct = default);
}
