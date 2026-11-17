using TreeGraph.Contracts.Messaging;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;

namespace TreeGraph.SyncServices.Messaging;

/// <summary>
/// SyncServices 消费四个业务变更队列，并发布反向 Tag 墓碑清理命令。
/// PostgreSQL 持久化 Inbox/Outbox + RabbitMQ 提供 at-least-once 投递；
/// 所有 Nebula/Qdrant 投影均幂等，不宣称端到端 exactly-once。
/// </summary>
public static class SyncServicesMessagingExtensions
{
    public static WolverineOptions UseSyncServicesMessaging(
        this WolverineOptions opts, IConfiguration config)
    {
        opts.PersistMessagesWithPostgresqlUsingNamedConnection("TreeGraphDb", "wolverine");

        var rabbitUri = config.GetConnectionString("RabbitMQ") ?? "amqp://localhost:5672";
        opts.UseRabbitMq(new Uri(rabbitUri)).AutoProvision();

        opts.ListenToRabbitQueue(MessagingQueueNames.InodeChanged);
        opts.ListenToRabbitQueue(MessagingQueueNames.EdgeChanged);
        opts.ListenToRabbitQueue(MessagingQueueNames.EntityChanged);
        opts.ListenToRabbitQueue(MessagingQueueNames.TagProjectionChanged);
        opts.PublishMessage<TagProjectionCleanupRequested>()
            .ToRabbitQueue(MessagingQueueNames.TagProjectionCleanup);

        opts.Policies.UseDurableInboxOnAllListeners();
        opts.Policies.UseDurableOutboxOnAllSendingEndpoints();
        opts.Policies.OnAnyException().RetryWithCooldown(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(1));

        return opts;
    }
}
