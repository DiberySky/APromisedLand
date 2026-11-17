using TreeGraph.Contracts.Messaging;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.ErrorHandling;
using Wolverine.Postgresql;
using Wolverine.Persistence;
using Wolverine.RabbitMQ;

namespace TreeGraph.Api.MessagingSky;

/// <summary>
/// 核心只发布业务变更并接收投影侧反向清理命令。
/// 业务数据与 PostgreSQL Outbox 共用请求事务；RabbitMQ 提供 at-least-once 投递，
/// 持久化 Inbox 与幂等 handler 处理重复消息，不宣称端到端 exactly-once。
/// </summary>
public static class MessagingExtensions
{
    public static WolverineOptions UseTreeGraphMessaging(
        this WolverineOptions opts, IConfiguration config)
    {
        opts.PersistMessagesWithPostgresqlUsingNamedConnection("TreeGraphDb", "wolverine");
        opts.UseEntityFrameworkCoreTransactions(TransactionMiddlewareMode.Lightweight);
        opts.Policies.AutoApplyTransactions();

        var rabbitUri = config.GetConnectionString("RabbitMQ") ?? "amqp://localhost:5672";
        opts.UseRabbitMq(new Uri(rabbitUri)).AutoProvision();

        opts.PublishMessage<InodeChangedEvent>().ToRabbitQueue(MessagingQueueNames.InodeChanged);
        opts.PublishMessage<EdgeChangedEvent>().ToRabbitQueue(MessagingQueueNames.EdgeChanged);
        opts.PublishMessage<EntityChangedEvent>().ToRabbitQueue(MessagingQueueNames.EntityChanged);
        opts.PublishMessage<SpaceNodeTagProjectionChangedEvent>()
            .ToRabbitQueue(MessagingQueueNames.TagProjectionChanged);

        opts.ListenToRabbitQueue(MessagingQueueNames.TagProjectionCleanup);

        opts.Policies.UseDurableInboxOnAllListeners();
        opts.Policies.UseDurableOutboxOnAllSendingEndpoints();
        opts.Policies.OnAnyException().RetryWithCooldown(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(1));

        opts.Services.AddScoped<IEventPublisher, WolverineEventPublisher>();

        return opts;
    }
}
