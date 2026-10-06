using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.StringTreeSky.Data;
using TreeGraph.Api.StringTreeSky.Entities;

namespace TreeGraph.Api.StringTreeSky.Seeding;

public static class StringTreeNodeSeeder
{
    public static async Task SeedAsync(StringTreeDbContext db)
    {
        if (await db.StringNodes.AnyAsync()) return;

        var root = new StringNodeEntity { Name = "物品总类", SortOrder = 0 };
        db.StringNodes.Add(root);
        await db.SaveChangesAsync();

        var electronics = new StringNodeEntity { Name = "电子产品", ParentId = root.Id, SortOrder = 0 };
        var office = new StringNodeEntity { Name = "办公用品", ParentId = root.Id, SortOrder = 1 };
        var furniture = new StringNodeEntity { Name = "家具", ParentId = root.Id, SortOrder = 2 };
        db.StringNodes.AddRange(electronics, office, furniture);
        await db.SaveChangesAsync();

        db.StringNodes.AddRange(
            new StringNodeEntity { Name = "手机", ParentId = electronics.Id, SortOrder = 0 },
            new StringNodeEntity { Name = "笔记本电脑", ParentId = electronics.Id, SortOrder = 1 },
            new StringNodeEntity { Name = "笔", ParentId = office.Id, SortOrder = 0 },
            new StringNodeEntity { Name = "文件夹", ParentId = office.Id, SortOrder = 1 },
            new StringNodeEntity { Name = "办公桌", ParentId = furniture.Id, SortOrder = 0 }
        );
        await db.SaveChangesAsync();
    }
}
