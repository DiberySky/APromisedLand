using Aspire.Npgsql.EntityFrameworkCore.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TreeGraphApi.Data;
using TreeGraphApi.Data.Seeding;
using TreeGraphApi.Entities;
using TreeGraphApi.Repositories;
using TreeGraphApi.Services;

var builder = WebApplication.CreateBuilder(args);

// .NET Aspire 集成:自动从 ConnectionStrings:TreeGraphDb 注入连接字符串
builder.AddNpgsqlDbContext<TreeDbContext>("TreeGraphDb");

// 仓储与服务
builder.Services.AddScoped<ITreeRepository, TreeRepository>();
builder.Services.AddScoped<ITreeService, TreeService>();
builder.Services.AddScoped<TreeSeeder>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 优化 #10:CORS 收紧 — 仅允许配置中的 origins,而非 AllowAnyOrigin
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

// 无条件应用迁移 + 幂等种子:
// - MigrateAsync 是幂等的(已应用过则跳过),开发/生产均安全
// - TreeSeeder.SeedAsync 内部用 AnyAsync 检查 + 事务包裹,数据已存在时跳过
// 注意:不使用 EnsureDeleted(生产会删库),也不依赖 IsDevelopment 判断
//       (Aspire 默认把子项目环境设为 Production,IsDevelopment 会跳过初始化)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TreeDbContext>();
    await db.Database.MigrateAsync();

    var seeder = scope.ServiceProvider.GetRequiredService<TreeSeeder>();
    await seeder.SeedAsync();
}

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.Run();
