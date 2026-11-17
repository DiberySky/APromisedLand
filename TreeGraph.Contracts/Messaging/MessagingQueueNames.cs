namespace TreeGraph.Contracts.Messaging;

/// <summary>两个宿主共享的 RabbitMQ 队列契约。</summary>
public static class MessagingQueueNames
{
    public const string InodeChanged = "treegraph.inode-changed";
    public const string EdgeChanged = "treegraph.edge-changed";
    public const string EntityChanged = "treegraph.entity-changed";
    public const string TagProjectionChanged = "treegraph.space-node-tag-projection-changed";
    public const string TagProjectionCleanup = "treegraph.tag-projection-cleanup";
}
