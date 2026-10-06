using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.NodeEavSky.Data;
using TreeGraph.Api.StringTreeSky.Entities;

namespace TreeGraph.Api.StringTreeSky.Seeding;

public static class StringTreeNodeSeeder
{
    public static async Task SeedAsync(EavDbContext db)
    {
        if (await db.StringTreeSkyNodes.AnyAsync()) return;

        var root = new StringNodeEntity { Id = Guid.NewGuid().ToString("D"), Name = "物品总类", SortOrder = 0 };

        var electronics = new StringNodeEntity { Id = Guid.NewGuid().ToString("D"), Name = "电子产品", ParentId = root.Id, SortOrder = 0 };
        var office = new StringNodeEntity { Id = Guid.NewGuid().ToString("D"), Name = "办公用品", ParentId = root.Id, SortOrder = 1 };
        var furniture = new StringNodeEntity { Id = Guid.NewGuid().ToString("D"), Name = "家具", ParentId = root.Id, SortOrder = 2 };

        db.StringTreeSkyNodes.AddRange(root, electronics, office, furniture);
        db.StringTreeSkyNodes.AddRange(
            new StringNodeEntity { Id = Guid.NewGuid().ToString("D"), Name = "手机", ParentId = electronics.Id, SortOrder = 0 },
            new StringNodeEntity { Id = Guid.NewGuid().ToString("D"), Name = "笔记本电脑", ParentId = electronics.Id, SortOrder = 1 },
            new StringNodeEntity { Id = Guid.NewGuid().ToString("D"), Name = "笔", ParentId = office.Id, SortOrder = 0 },
            new StringNodeEntity { Id = Guid.NewGuid().ToString("D"), Name = "文件夹", ParentId = office.Id, SortOrder = 1 },
            new StringNodeEntity { Id = Guid.NewGuid().ToString("D"), Name = "办公桌", ParentId = furniture.Id, SortOrder = 0 }
        );
        await db.SaveChangesAsync();
    }
}
