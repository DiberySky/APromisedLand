using TreeGraph.Persistence.Data;
using Wolverine.EntityFrameworkCore;

namespace TreeGraph.Api.MessagingSky;

/// <summary>
/// 将业务事件写入当前 <see cref="TreeGraphDbContext"/> 的持久化 Outbox。
/// 请求中间件负责提交事务并 flush；此处不向 RabbitMQ 即时发送。
/// </summary>
public sealed class WolverineEventPublisher : IEventPublisher
{
    private readonly IDbContextOutbox _outbox;

    public WolverineEventPublisher(
        IDbContextOutbox outbox, TreeGraphDbContext db)
    {
        _outbox = outbox;
        _outbox.Enroll(db);
    }

    public Task Publish<T>(T message, CancellationToken ct = default)
        => _outbox.PublishAsync(message).AsTask();
}
