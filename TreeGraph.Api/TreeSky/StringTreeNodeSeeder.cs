using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.NodeEavSky.Data;
using TreeGraph.Blazor.Shared.Models;

namespace TreeGraph.Api.TreeSky;

/// <summary>
/// TreeSky 演示树种子：与此前内存演示（InMemoryTreeStore）相同的数据，
/// 幂等（表非空即跳过），仅在空库时播种一次。
/// </summary>
public static class StringTreeNodeSeeder
{
    public static async Task SeedAsync(EavDbContext db, CancellationToken ct = default)
    {
        if (await db.StringTreeNodes.AnyAsync(ct))
            return;

        StringTreeNode Node(string id, string name, string? parentId, int sort,
            bool canHaveChildren, string? desc = null) => new()
        {
            Id = id,
            Name = name,
            Description = desc,
            ParentId = parentId,
            SortOrder = sort,
            CanHaveChildren = canHaveChildren,
        };

        db.StringTreeNodes.AddRange(
            Node("root", "物品总类", null, 0, true, "演示树的根节点"),
            Node("cat-electronics", "电子产品", "root", 0, true),
            Node("leaf-phone", "智能手机", "cat-electronics", 0, false, "可移动、可编辑的叶子节点"),
            Node("leaf-laptop", "笔记本电脑", "cat-electronics", 1, false),
            Node("cat-office", "办公用品", "root", 1, true),
            Node("leaf-pen", "中性笔", "cat-office", 0, false),
            Node("leaf-paper", "A4 打印纸", "cat-office", 1, false),
            Node("cat-furniture", "家具（空分类）", "root", 2, true, "暂无子项，可在此创建子节点"));

        await db.SaveChangesAsync(ct);
    }
}
