using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace APromisedLand.Api.Data;

/// <summary>
/// 设计时工厂：专供 `dotnet ef migrations` / `dotnet ef database update` 使用。
/// 绕过 Program.cs 的启动逻辑，直接从 appsettings 或环境变量读取连接串。
/// </summary>
public class MafRagContextFactory : IDesignTimeDbContextFactory<MafRagContext>
{
    public MafRagContext CreateDbContext(string[] args)
    {
        // 1. 构建配置：从当前目录读取 appsettings.json 和环境变量
        var configuration = BuildConfiguration();

        // 2. 读取 MetadataDb 连接串
        var connectionString = configuration.GetConnectionString("MetadataDb")
                               ?? "Host=localhost;Port=8433;Database=MetadataDb;Username=postgres;Password=postgres";

        // 3. 构建 DbContextOptions
        var optionsBuilder = new DbContextOptionsBuilder<MafRagContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new MafRagContext(optionsBuilder.Options);
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables();

        return builder.Build();
    }
}