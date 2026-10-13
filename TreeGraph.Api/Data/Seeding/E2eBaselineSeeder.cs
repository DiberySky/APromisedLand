using Microsoft.EntityFrameworkCore;

namespace TreeGraph.Api.Data.Seeding;

/// <summary>
/// E2E 基线种子（已清空）：不再播种任何属性类型、属性值及 iNode 归属。
/// 保留空实现以维持 Program.cs 中的调用链与事务框架。
/// </summary>
public static class E2eBaselineSeeder
{
    // ───── 固定 EntityId 前缀，便于 E2E 断言 ─────
    public const string ItemPrefix = "item-seed-";
    public const string UserPrefix = "user-seed-";
    public const string ProjectPrefix = "project-seed-";

    // ───── 固定 InodeId 前缀 ─────
    // 一个 iNode 可声明多类型、每类型挂 1 个实体。
    // 5 个 iNode 覆盖 10 个实体：001/002 三类型齐全，003 双类型，004/005 单类型。
    public const string InodePrefix = "inode-seed-";

    public static async Task SeedAsync(TreeGraphDbContext db, CancellationToken ct = default)
    {
        if (await IsAlreadySeededAsync(db, ct))
            return;

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                await SeedInternalAsync(db, ct);
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });
    }

    /// <summary>严格幂等：item 类型 + 属性 + 示例实体 + iNode 归属都存在才跳过。</summary>
    private static async Task<bool> IsAlreadySeededAsync(TreeGraphDbContext db, CancellationToken ct)
    {
        // 所有 seed 属性类型已清空，直接返回 true 跳过 seed。
        return await Task.FromResult(true);
    }

    /// <summary>实际的 seed 逻辑（已清空：不再播种任何测试属性类型及属性值）。</summary>
    private static async Task SeedInternalAsync(TreeGraphDbContext db, CancellationToken ct)
    {
        // 所有 seed 属性类型已清空，此处保留空实现以维持调用链与事务框架。
        await Task.CompletedTask;
    }
}
