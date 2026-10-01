using Microsoft.EntityFrameworkCore;
using TreeGraphApi.Entities;

namespace TreeGraphApi.Data.Seeding;

public class TreeSeeder
{
    private readonly TreeDbContext _context;
    private readonly ILogger<TreeSeeder> _logger;

    public TreeSeeder(TreeDbContext context, ILogger<TreeSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 优化 #11:用事务包裹种子写入,避免多实例启动时的 TOCTOU 风险。
    /// 注意:Aspire 的 AddNpgsqlDbContext 给上下文配了 NpgsqlRetryingExecutionStrategy,
    /// 该策略不允许用户主动开事务,必须用 CreateExecutionStrategy().ExecuteAsync(...)
    /// 把整段(含事务)包成一个可重试单元,否则会抛 InvalidOperationException。
    /// </summary>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(
            state: this,
            operation: async (db, state, token) =>
            {
                var ctx = (TreeDbContext)db;
                await using var tx = await ctx.Database.BeginTransactionAsync(token);

                if (await ctx.TreeNodes.AnyAsync(token))
                {
                    await tx.RollbackAsync(token);
                    state._logger.LogInformation("Tree nodes already exist, skip seeding.");
                    return true;
                }

                var entities = new List<TreeNodeEntity>();
                foreach (var root in TreeSeedData.GetNodes())
                    Flatten(root, parentId: null, parentPath: null, entities);

                ctx.TreeNodes.AddRange(entities);
                await ctx.SaveChangesAsync(token);
                await tx.CommitAsync(token);

                state._logger.LogInformation("Seeded {Count} tree nodes.", entities.Count);
                return true;
            },
            verifySucceeded: null,
            cancellationToken: ct);
    }

    private static void Flatten(SeedNode node, string? parentId, string? parentPath,
                                List<TreeNodeEntity> output)
    {
        var id = node.Id ?? Guid.NewGuid().ToString();
        // 物化路径:从根到当前节点,以 '.' 分隔(纯字符串,不依赖 ltree)
        var path = parentPath is null ? id : $"{parentPath}.{id}";

        output.Add(new TreeNodeEntity
        {
            Id = id,
            ParentId = parentId,
            Text = node.Text,
            Icon = node.Icon,
            NodeType = node.NodeType,
            SortOrder = node.SortOrder,
            Path = path,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        foreach (var child in node.Children)
            Flatten(child, parentId: id, parentPath: path, output);
    }
}
