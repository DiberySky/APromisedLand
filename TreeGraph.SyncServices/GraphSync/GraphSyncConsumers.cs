using Microsoft.EntityFrameworkCore;
using TreeGraph.Contracts.Messaging;
using TreeGraph.Persistence.Data;

namespace TreeGraph.SyncServices.GraphSync;

/// <summary>iNode 变更 → 幂等同步图顶点与 parent_of 边；重复投递由持久化 Inbox 和幂等投影处理。</summary>
public sealed class InodeGraphSyncConsumer(IServiceScopeFactory scopeFactory)
{
    public async Task Consume(InodeChangedEvent msg, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var initializer = scope.ServiceProvider.GetRequiredService<GraphSchemaInitializer>();
        var synchronizer = scope.ServiceProvider.GetRequiredService<GraphSynchronizer>();
        var db = scope.ServiceProvider.GetRequiredService<TreeGraphDbContext>();

        await initializer.EnsureReadyAsync(ct);
        if (msg.Kind != InodeChangeKind.Deleted
            && await db.Spaces.AsNoTracking().AnyAsync(s => s.Id == msg.InodeId, ct))
        {
            await initializer.EnsureSpaceSchemaAsync(msg.InodeId, ct);
        }

        await synchronizer.SyncInodeAsync(msg.InodeId, ct);
        if (msg.Kind == InodeChangeKind.Deleted)
            await initializer.DropSpaceSchemaAsync(msg.InodeId, ct);
    }
}

/// <summary>边实例变更 → 幂等同步图边；Delete 使用事件四元组，Upsert 回查 PostgreSQL 当前态。</summary>
public sealed class EdgeGraphSyncConsumer(IServiceScopeFactory scopeFactory)
{
    public async Task Consume(EdgeChangedEvent msg, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var initializer = scope.ServiceProvider.GetRequiredService<GraphSchemaInitializer>();
        var synchronizer = scope.ServiceProvider.GetRequiredService<GraphSynchronizer>();

        await initializer.EnsureReadyAsync(ct);
        await synchronizer.SyncEdgeAsync(msg, ct);
    }
}
