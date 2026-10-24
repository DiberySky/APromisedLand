using TreeGraph.Shared.Messaging;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.RabbitMQ;

namespace TreeGraph.Api.MessagingSky;

/// <summary>
/// TreeGraph.Api 消息总线（Wolverine + RabbitMQ）拓扑与策略。
///
/// 拓扑：每事件类型一条 durable 队列，直发直收（无 exchange）：
///   InodeChangedEvent → treegraph.inode-changed（向量同步 + 图同步两个 handler 级联消费）
///   RelationChangedEvent  → treegraph.relation-changed（图边同步；R7 起 line_segment 段边也走此管道）
///
/// R7：线路结构变更专用事件及其队列已删除（段边即 relation 值，走 relation-changed 管道）。
///
/// 消费语义：同一消息类型的多个 handler 由 Wolverine 合并为一个逻辑 handler 串行执行；
/// 任一失败整条消息按 RetryWithCooldown 重放，消费端 IMemoryCache 去重窗口 + 幂等同步器兜底
/// （与原 MassTransit 指数重试 5 次语义对齐）；用尽后进入 wolverine-dead-letter-queue。
/// </summary>
public static class MessagingExtensions
{
    public static WolverineOptions UseTreeGraphMessaging(
        this WolverineOptions opts, IConfiguration config)
    {
        // 无持久化存储（无 EF 收件箱/发件箱），显式 Solo 避免 leader election 语义
        opts.Durability.Mode = DurabilityMode.Solo;

        // 连接优先级：ConnectionStrings:RabbitMQ（Aspire 注入）> 本地回退 amqp://localhost:5672
        var rabbitUri = config.GetConnectionString("RabbitMQ") ?? "amqp://localhost:5672";
        opts.UseRabbitMq(new Uri(rabbitUri)).AutoProvision();

        opts.PublishMessage<InodeChangedEvent>().ToRabbitQueue("treegraph.inode-changed");
        opts.ListenToRabbitQueue("treegraph.inode-changed");

        opts.PublishMessage<RelationChangedEvent>().ToRabbitQueue("treegraph.relation-changed");
        opts.ListenToRabbitQueue("treegraph.relation-changed");

        // 对齐原 MassTransit 指数重试 5 次（min 1s / max 1m）
        opts.Policies.OnAnyException().RetryWithCooldown(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(1));

        // IMessageBus 在 Wolverine DI 中为 Scoped，IEventPublisher 必须同为 Scoped
        opts.Services.AddScoped<IEventPublisher, WolverineEventPublisher>();

        return opts;
    }
}
