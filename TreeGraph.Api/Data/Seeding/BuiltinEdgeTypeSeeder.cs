using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.EdgeSky.Entities;
using TreeGraph.Shared.EdgeSky;

namespace TreeGraph.Api.Data.Seeding;

/// <summary>
/// 内置边类型播种（启动时幂等 upsert；按空间循环——EdgeType 从属于图空间）。
///
/// line_segment 是线路结构段边（LineNode → LineNode 链边，rank 恒 0），
/// LineSky 写路径硬依赖该类型存在，故在线路所属空间（Dynamic）的 edge_types 中播种一份。
/// parent_of 是树父子结构派生边（父节点 → 子节点，rank 恒 0），单位/类型/知识库/动态空间
/// 都存在树结构，故所有空间各播种一份；GraphSynchronizer 会在节点创建/移动时写入 edges 行。
/// connects_to 仍为纯图侧硬编码边（GraphSchemaInitializer），不入 PG。
///
/// 与 BuiltinHostTypeSeeder 同款模式：从迁移内嵌 HasData 改为启动时播种，
/// 新增内置边类型不再要求重建迁移。新建空间由 SpaceController 调 <see cref="SeedForSpaceAsync"/> 即时补种。
/// 幂等规则（空间内）：
///   - 类型不存在 → 插入（固定时间戳）；
///   - 类型已存在 → 不动（尊重管理员对注释/显示名的调整）；内置显示名缺失时补齐；
///   - 属性缺失 → 补齐（Schema 演进时自动补列）。
/// </summary>
public static class BuiltinEdgeTypeSeeder
{
    private static readonly DateTimeOffset SeedTime =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private sealed record BuiltinType(string Name, string DisplayName, string Comment, BuiltinTypeProp[] Props);

    private sealed record BuiltinTypeProp(string PropName, string DisplayName, string DataType, bool Nullable, string Comment);

    private static readonly BuiltinType[] Builtin =
    [
        new("line_segment",
            "线段",
            "线路结构段边（内置：LineNode → LineNode 链边）",
            [
                new("name", "名称", EdgePropDataTypes.String, true, "段边展示名")
            ]),
        new("parent_of",
            "父子关系",
            "树父子结构派生边（内置：父节点 → 子节点，rank 恒 0）",
            [])
    ];

    /// <summary>启动时播种：对所有空间逐空间幂等播种内置边类型（Dynamic 空间额外含 line_segment）。</summary>
    public static async Task SeedAsync(TreeGraphDbContext db, CancellationToken ct = default)
    {
        var spaceIds = await db.Spaces.AsNoTracking().Select(s => s.Id).ToListAsync(ct);
        foreach (var spaceId in spaceIds)
            await SeedForSpaceAsync(db, spaceId, ct);
    }

    /// <summary>单空间播种（新建空间时即时补种；幂等）。line_segment 仅 Dynamic 空间需要。</summary>
    public static async Task SeedForSpaceAsync(TreeGraphDbContext db, string spaceId, CancellationToken ct = default)
    {
        var isDynamic = await db.Spaces.AsNoTracking()
            .Where(s => s.Id == spaceId)
            .Select(s => s.Type == TreeGraph.Api.SpaceSky.Entities.SpaceType.Dynamic)
            .FirstOrDefaultAsync(ct);

        var defs = isDynamic
            ? Builtin
            : Builtin.Where(b => b.Name != "line_segment").ToArray();

        var names = defs.Select(b => b.Name).ToList();
        var existingTypes = await db.EdgeTypes
            .Where(t => t.SpaceId == spaceId && names.Contains(t.Name))
            .Include(t => t.Props)
            .ToListAsync(ct);

        foreach (var def in defs)
        {
            var found = existingTypes.FirstOrDefault(t => t.Name == def.Name);
            if (found is null)
            {
                db.EdgeTypes.Add(new EdgeType
                {
                    SpaceId = spaceId,
                    Name = def.Name,
                    DisplayName = def.DisplayName,
                    Comment = def.Comment,
                    CreatedAt = SeedTime,
                    UpdatedAt = SeedTime,
                    Props = def.Props.Select(p => new EdgeTypeProp
                    {
                        SpaceId = spaceId,
                        PropName = p.PropName,
                        DisplayName = p.DisplayName,
                        DataType = p.DataType,
                        Nullable = p.Nullable,
                        Comment = p.Comment
                    }).ToList()
                });
                continue;
            }

            // 内置中文显示名缺失时补齐（管理员已手动改过的不动）
            if (string.IsNullOrWhiteSpace(found.DisplayName))
                found.DisplayName = def.DisplayName;

            // 补齐缺失属性（不覆盖已有定义，尊重管理员调整）；已有属性显示名缺失时补齐
            foreach (var p in def.Props)
            {
                var existingProp = found.Props.FirstOrDefault(x => x.PropName == p.PropName);
                if (existingProp is null)
                {
                    db.EdgeTypeProps.Add(new EdgeTypeProp
                    {
                        SpaceId = spaceId,
                        EdgeTypeName = def.Name,
                        PropName = p.PropName,
                        DisplayName = p.DisplayName,
                        DataType = p.DataType,
                        Nullable = p.Nullable,
                        Comment = p.Comment
                    });
                }
                else if (string.IsNullOrWhiteSpace(existingProp.DisplayName))
                {
                    existingProp.DisplayName = p.DisplayName;
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
