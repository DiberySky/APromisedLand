using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.NodeEavSky.Data;
using TreeGraph.Api.NodeEavSky.Data.Seeding;
using TreeGraph.Api.NodeEavSky.Infrastructure;
using TreeGraph.Api.NodeEavSky.Services;
using TreeGraph.Api.TreeSky;
using TreeGraph.Api.StringTreeSky;
using TreeGraph.Api.StringTreeSky.Extensions;

var builder = WebApplication.CreateBuilder(args);

// ★ Aspire ServiceDefaults：服务发现、健康检查、OpenTelemetry
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new NumericValueJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new DynamicCompositeValueJsonConverter());
    });
builder.Services.AddOpenApi();

// ★ 全局异常处理：把 EF/Npgsql 已知异常映射为 409/400
builder.Services.AddExceptionHandler<DbExceptionHandler>();
builder.Services.AddProblemDetails();

// .NET Aspire 集成:自动从 ConnectionStrings:TreeGraphDb 注入连接字符串
builder.AddNpgsqlDbContext<EavDbContext>("TreeGraphDb");

builder.Services.AddMemoryCache();
builder.Services.AddScoped<EavWriteService>();
builder.Services.AddScoped<EavReadService>();
builder.Services.AddScoped<EavQueryService>();
builder.Services.AddScoped<EavValidationService>();
builder.Services.AddScoped<CompositeValueService>();
builder.Services.AddScoped<IInodeEntityService, InodeEntityService>();
builder.Services.AddScoped<InodeEavFacade>();
builder.Services.AddSingleton<IAttributeCache, AttributeCache>();
builder.Services.AddSingleton<ICompositeTypeCache, CompositeTypeCache>();
builder.Services.AddSingleton<IUnitCache, UnitCache>();
builder.Services.AddSingleton<UnitConverter>();
builder.Services.AddSingleton<ICustomTableCache, CustomTableCache>();
builder.Services.AddSingleton<IOptionSetCache, OptionSetCache>();
builder.Services.AddScoped<UnitSeedService>();
builder.Services.AddScoped<CustomTableValidationService>();
builder.Services.AddScoped<CustomTableWriteService>();
builder.Services.AddScoped<CustomTableReadService>();
builder.Services.AddScoped<CustomTableQueryService>();

// ★ TreeSky 树组件后端：泛型 EF 实现 + StringTreeNode 字符串节点树
builder.Services.AddScoped<TreeGraph.Api.TreeSky.ITreeService<TreeGraph.Blazor.Shared.Trees.TreeSky.Models.StringTreeNode>,
    TreeGraph.Api.TreeSky.EfTreeService<TreeGraph.Blazor.Shared.Trees.TreeSky.Models.StringTreeNode>>();

// ★ StringTreeSky 解耦版：表 string_tree_sky_nodes 并入 EavDbContext（treegraphdb），
//   仅服务/控制器独立，与 TreeSky 泛型体系并行（StringTreeSky 完全解耦方案）。
builder.Services.AddStringTreeApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// ★ 研发阶段：暂不启用身份验证/授权管道
//   UseAuthorization() 会解析 IAuthorizationPolicyProvider，
//   未 AddAuthorization() 时首次请求抛 InvalidOperationException。
// app.UseAuthorization();

// ★ 全局异常处理
app.UseExceptionHandler();

app.MapControllers();

// ★ Aspire 默认端点（/health、/alive），供 Dashboard 探测
app.MapDefaultEndpoints();

// 启动时应用迁移并注入种子数据（EF 设计期跳过，避免 dotnet-ef 连接数据库）
if (!EF.IsDesignTime)
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<EavDbContext>();
    await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<UnitSeedService>().SeedAsync();
    await EavSeeder.SeedAsync(db);
    await StringTreeNodeSeeder.SeedAsync(db);

    // ★ StringTreeSky 解耦版：空库播种（幂等）
    await TreeGraph.Api.StringTreeSky.Seeding.StringTreeNodeSeeder.SeedAsync(db);

    // E2E 基线（item/user/project + 固定 EntityId 示例值）仅开发/测试环境注入
    if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
    {
        await E2eBaselineSeeder.SeedAsync(db);
    }
}

app.Run();

// ★ 让 WebApplicationFactory<Program> 可引用（集成测试）
public partial class Program { }
