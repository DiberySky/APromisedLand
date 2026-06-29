using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace QuestionService.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Questions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Content = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    AskerId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    AskerDisplayName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreateAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ViewCount = table.Column<int>(type: "integer", nullable: false),
                    TagSlugs = table.Column<List<string>>(type: "text[]", nullable: false),
                    HasAcceptedAnswer = table.Column<bool>(type: "boolean", nullable: false),
                    Votes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Questions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Slug = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "Description", "Name", "Slug" },
                values: new object[,]
                {
                    { "aspire", "这是一个用于使用.NET 构建分布式应用程序的、可组合且功能完备的解决方案。它提供了仪表板、诊断功能以及简化的服务编排功能。", "Aspire", "aspire" },
                    { "dotnet", "这是一个现代化的跨平台开发平台，允许使用 C#和 F#语言来开发适用于云端、网页、移动设备、桌面端以及物联网领域的应用程序。", ".NET", "dotnet" },
                    { "ef-core", "这是一款适用于.NET 平台的现代对象-数据库映射工具（ORM），它支持 LINQ、变更跟踪以及数据库迁移功能，非常适合与关系型数据库进行交互。", "Entity Framework Core", "ef-core" },
                    { "keycloak", "一款适用于现代应用程序和服务的开源身份与访问管理解决方案。可轻松与 OAuth2、OIDC 及单点登录功能相集成。", "Keycloak", "keycloak" },
                    { "microservices", "这是一种将应用程序构建为一系列松耦合服务的架构风格。这些服务可以独立部署和扩展。", "Microservices", "microservices" },
                    { "nextjs", "这是一个用于构建快速、全功能 Web 应用的 React 框架，支持服务器端渲染、路由处理以及静态页面生成功能。", "Next.js", "nextjs" },
                    { "postgresql", "这是一款功能强大、开源的对象关系型数据库系统，以其可靠性、丰富的功能以及符合各种标准而著称。", "PostgreSQL", "postgresql" },
                    { "signalr", "这是一个用于 ASP.NET 的实时通信库，支持通过 WebSockets、长轮询等方式实现服务器与客户端之间的消息传递。", "SignalR", "signalr" },
                    { "typescript", "这是一种静态类型的 JavaScript 超集，编译后的结果仍然是标准的 JavaScript 代码。它借助各种工具，有助于构建大规模应用程序。", "TypeScript", "typescript" },
                    { "wolverine", "这是一个为.NET 平台打造的高性能消息传递和命令处理框架，内置了对中介机制、消息队列、重试机制以及持久化消息存储的支持。", "Wolverine", "wolverine" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Questions");

            migrationBuilder.DropTable(
                name: "Tags");
        }
    }
}
