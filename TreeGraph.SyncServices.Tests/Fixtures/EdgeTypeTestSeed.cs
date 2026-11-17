using Microsoft.EntityFrameworkCore;
using TreeGraph.Persistence.Data;
using TreeGraph.Persistence.EdgeSky.Entities;
using TreeGraph.Persistence.SpaceSky.Entities;
using TreeGraph.Shared.EdgeSky;

namespace TreeGraph.SyncServices.Tests.Fixtures;

/// <summary>
/// 测试环境内置边类型补种：parent_of 全空间、line_segment（含 name STRING NULL 属性）仅 Dynamic 空间。
/// </summary>
public static class EdgeTypeTestSeed
{
    private static readonly DateTimeOffset SeedTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>幂等补插：类型不存在才插入（不动既有定义）；line_segment 仅 Dynamic 空间。</summary>
    public static async Task SeedAsync(TreeGraphDbContext db, CancellationToken ct = default)
    {
        var spaces = await db.Spaces.AsNoTracking()
            .Select(s => new { s.Id, s.Type })
            .ToListAsync(ct);

        foreach (var space in spaces)
        {
            var names = space.Type == SpaceType.Dynamic
                ? new[] { "parent_of", "line_segment" }
                : new[] { "parent_of" };
            var existing = await db.EdgeTypes
                .Where(t => t.SpaceId == space.Id && names.Contains(t.Name))
                .Select(t => t.Name)
                .ToListAsync(ct);

            foreach (var name in names.Except(existing))
            {
                var isParent = name == "parent_of";
                db.EdgeTypes.Add(new EdgeType
                {
                    SpaceId = space.Id,
                    Name = name,
                    DisplayName = isParent ? "父子关系" : "线段",
                    Comment = isParent
                        ? "树父子结构派生边（内置：父节点 → 子节点，rank 恒 0）"
                        : "线路结构段边（内置：LineNode → LineNode 链边）",
                    CreatedAt = SeedTime,
                    UpdatedAt = SeedTime
                });
                if (!isParent)
                {
                    db.EdgeTypeProps.Add(new EdgeTypeProp
                    {
                        SpaceId = space.Id,
                        EdgeTypeName = name,
                        PropName = "name",
                        DisplayName = "名称",
                        DataType = EdgePropDataTypes.String,
                        Nullable = true,
                        Comment = "段边展示名"
                    });
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
