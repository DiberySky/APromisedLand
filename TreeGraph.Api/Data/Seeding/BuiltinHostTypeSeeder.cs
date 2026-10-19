using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.NodeEavSky.Entities;

namespace TreeGraph.Api.Data.Seeding;

/// <summary>
/// 内置 Inode 宿主类型播种（启动时幂等 upsert）。
///
/// 四棵树（SpaceTree/UnitTree/CategoryTree/KnowledgeTree）是边类型端点选择与 EAV 属性挂载的
/// 合法宿主（由 InodeOwnerGuardRegistry 认领），必须在实体类型目录中可见，
/// 否则边类型对话框的「起点/终点节点类型」下拉无候选。
///
/// 与 EavSeeder（用户元数据，已清空）分开：这里只维护系统必需的内置类型。
/// 幂等规则：
///   - 不存在 → 插入（固定 Id/时间戳）；
///   - 已存在且未删除 → 不动（尊重管理员对显示名的调整）；
///   - 已存在但被软删 → 复活（系统功能依赖其可见）。
/// </summary>
public static class BuiltinHostTypeSeeder
{
    private static readonly DateTimeOffset SeedTime =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly EntityTypeDefinition[] Builtin =
    [
        new()
        {
            EntityTypeId = "b0000001-0000-4000-8000-000000000001",
            EntityType = "SpaceTree",
            DisplayName = "空间树",
            Description = "内置 Inode 宿主：空间树节点（边端点/属性挂载用）",
            DisplayOrder = 0,
            CreatedAt = SeedTime,
            UpdatedAt = SeedTime
        },
        new()
        {
            EntityTypeId = "b0000002-0000-4000-8000-000000000002",
            EntityType = "UnitTree",
            DisplayName = "单位树",
            Description = "内置 Inode 宿主：单位树节点（边端点/属性挂载用）",
            DisplayOrder = 1,
            CreatedAt = SeedTime,
            UpdatedAt = SeedTime
        },
        new()
        {
            EntityTypeId = "b0000003-0000-4000-8000-000000000003",
            EntityType = "CategoryTree",
            DisplayName = "分类树",
            Description = "内置 Inode 宿主：分类树节点（边端点/属性挂载用）",
            DisplayOrder = 2,
            CreatedAt = SeedTime,
            UpdatedAt = SeedTime
        },
        new()
        {
            EntityTypeId = "b0000004-0000-4000-8000-000000000004",
            EntityType = "KnowledgeTree",
            DisplayName = "知识树",
            Description = "内置 Inode 宿主：知识树节点（边端点/属性挂载用）",
            DisplayOrder = 3,
            CreatedAt = SeedTime,
            UpdatedAt = SeedTime
        }
    ];

    public static async Task SeedAsync(TreeGraphDbContext db, CancellationToken ct = default)
    {
        var names = Builtin.Select(b => b.EntityType).ToList();
        var existing = await db.EntityTypes
            .Where(t => names.Contains(t.EntityType))
            .ToListAsync(ct);

        foreach (var def in Builtin)
        {
            var found = existing.FirstOrDefault(
                t => t.EntityType == def.EntityType);
            if (found is null)
            {
                db.EntityTypes.Add(def);
            }
            else if (found.IsDeleted)
            {
                found.IsDeleted = false;
                found.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
