using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TreeGraph.IdentityAdmin.Audit;

/// <summary>
/// 设计时上下文工厂，供 <c>dotnet ef</c> 生成迁移使用。
/// 连接串优先读环境变量 <c>ConnectionStrings__IdentityAdminDb</c>，否则使用本地默认值。
/// </summary>
public sealed class IdentityAdminDbContextFactory : IDesignTimeDbContextFactory<IdentityAdminDbContext>
{
    public IdentityAdminDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__IdentityAdminDb")
            ?? "Host=localhost;Port=8433;Database=IdentityAdminDb;Username=postgres;Password=postgres";

        var builder = new DbContextOptionsBuilder<IdentityAdminDbContext>();
        builder.UseNpgsql(connectionString);
        return new IdentityAdminDbContext(builder.Options);
    }
}
