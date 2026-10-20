using TreeGraph.Api.MessagingSky;

namespace TreeGraph.Api.Tests.Fixtures;

/// <summary>
/// 无操作 IEventPublisher 桩：服务单元测试直接 new 服务实例时注入，
/// 发布消息即丢弃（不关心事件投递，仅满足构造签名）。
/// </summary>
public sealed class NoopEventPublisher : IEventPublisher
{
    public Task Publish<T>(T message, CancellationToken ct = default)
        => Task.CompletedTask;
}
