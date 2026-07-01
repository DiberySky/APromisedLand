using Microsoft.EntityFrameworkCore;
using QuestionService.Models;

namespace QuestionService.Data;

public class QuestionDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<Question> Questions { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<Answer> Answers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<Tag>()
            .HasData(
                new Tag
                {
                    Id = "aspire",
                    Name = "Aspire",
                    Slug = "aspire",
                    Description =
                        "这是一个用于使用.NET 构建分布式应用程序的、可组合且功能完备的解决方案。它提供了仪表板、诊断功能以及简化的服务编排功能。"
                },
                new Tag
                {
                    Id = "keycloak",
                    Name = "Keycloak",
                    Slug = "keycloak",
                    Description =
                        "一款适用于现代应用程序和服务的开源身份与访问管理解决方案。可轻松与 OAuth2、OIDC 及单点登录功能相集成。"
                },
                new Tag
                {
                    Id = "dotnet",
                    Name = ".NET",
                    Slug = "dotnet",
                    Description =
                        "这是一个现代化的跨平台开发平台，允许使用 C#和 F#语言来开发适用于云端、网页、移动设备、桌面端以及物联网领域的应用程序。"
                },
                new Tag
                {
                    Id = "ef-core",
                    Name = "Entity Framework Core",
                    Slug = "ef-core",
                    Description =
                        "这是一款适用于.NET 平台的现代对象-数据库映射工具（ORM），它支持 LINQ、变更跟踪以及数据库迁移功能，非常适合与关系型数据库进行交互。"
                },
                new Tag
                {
                    Id = "wolverine",
                    Name = "Wolverine",
                    Slug = "wolverine",
                    Description =
                        "这是一个为.NET 平台打造的高性能消息传递和命令处理框架，内置了对中介机制、消息队列、重试机制以及持久化消息存储的支持。"
                },
                new Tag
                {
                    Id = "postgresql",
                    Name = "PostgreSQL",
                    Slug = "postgresql",
                    Description =
                        "这是一款功能强大、开源的对象关系型数据库系统，以其可靠性、丰富的功能以及符合各种标准而著称。"
                },
                new Tag
                {
                    Id = "signalr",
                    Name = "SignalR",
                    Slug = "signalr",
                    Description =
                        "这是一个用于 ASP.NET 的实时通信库，支持通过 WebSockets、长轮询等方式实现服务器与客户端之间的消息传递。"
                },
                new Tag
                {
                    Id = "nextjs",
                    Name = "Next.js",
                    Slug = "nextjs",
                    Description =
                        "这是一个用于构建快速、全功能 Web 应用的 React 框架，支持服务器端渲染、路由处理以及静态页面生成功能。"
                },
                new Tag
                {
                    Id = "typescript",
                    Name = "TypeScript",
                    Slug = "typescript",
                    Description =
                        "这是一种静态类型的 JavaScript 超集，编译后的结果仍然是标准的 JavaScript 代码。它借助各种工具，有助于构建大规模应用程序。"
                },
                new Tag
                {
                    Id = "microservices",
                    Name = "Microservices",
                    Slug = "microservices",
                    Description =
                        "这是一种将应用程序构建为一系列松耦合服务的架构风格。这些服务可以独立部署和扩展。"
                }
            );
    }
}