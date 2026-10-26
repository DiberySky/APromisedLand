using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.EdgeSky.Entities;
using TreeGraph.Shared.EdgeSky;

namespace TreeGraph.Api.Data.Seeding;

/// <summary>
/// 内置边类型播种（启动时幂等 upsert；按空间循环——EdgeType 从属于图空间）。
///
/// line_segment 是线路结构段边（LineNode → LineNode 链边，rank 恒 0），
/// LineSky 写路径硬依赖该类型存在，必须在线路所属空间的 edge_types 中可见，
/// 故每个空间各播种一份（同名跨空间互不影响，对齐 Nebula 空间级语义）。
/// parent_of / connects_to 为结构派生边，不入 PG（仍由 GraphSchemaInitializer 硬编码建 schema）。
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
            ])
    ];

    /// <summary>启动时播种：对所有现存空间逐空间幂等播种内置边类型。</summary>
    public static async Task SeedAsync(TreeGraphDbContext db, CancellationToken ct = default)
    {
        var spaceIds = await db.Spaces.AsNoTracking().Select(s => s.Id).ToListAsync(ct);
        foreach (var spaceId in spaceIds)
            await SeedForSpaceAsync(db, spaceId, ct);
    }

    /// <summary>单空间播种（新建空间时即时补种；幂等）。</summary>
    public static async Task SeedForSpaceAsync(TreeGraphDbContext db, string spaceId, CancellationToken ct = default)
    {
        var names = Builtin.Select(b => b.Name).ToList();
        var existingTypes = await db.EdgeTypes
            .Where(t => t.SpaceId == spaceId && names.Contains(t.Name))
            .Include(t => t.Props)
            .ToListAsync(ct);

        foreach (var def in Builtin)
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
