using TreeGraph.Api.Data;

namespace TreeGraph.Api.Data.Seeding;

/// <summary>
/// EAV 元数据种子（已清空）：
/// 不再播种任何实体类型、属性定义、组合类型、自定义表、选项集。
/// 保留空实现以维持调用链。
/// </summary>
public static class EavSeeder
{
    public static async Task SeedAsync(TreeGraphDbContext db, CancellationToken ct = default)
    {
        // 所有 seed 已清空，此处保留空实现以维持调用链与事务框架。
        await Task.CompletedTask;
    }
}
