using Microsoft.EntityFrameworkCore;
using TreeGraph.Persistence.Data;
using TreeGraph.Api.Data.Seeding;
using TreeGraph.Api.LineSky.Services;
using TreeGraph.Api.LineSky.Templates;
using TreeGraph.Api.MessagingSky;
using TreeGraph.Api.NodeEavSky.Infrastructure;
using TreeGraph.Api.NodeEavSky.Services;
using TreeGraph.GraphSync;
using TreeGraph.WebShared.Infrastructure;
using TreeGraph.WebShared.NodeEavSky;
using TreeGraph.Api.TreeEavSky.Services;
using Wolverine;

var builder = WebApplication.CreateBuilder(args);

// ★ Aspire ServiceDefaults：服务发现、健康检查、OpenTelemetry
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddControllers(options =>
    {
        // EAV 专属异常先映射，再由共享过滤器统一包装响应。
        options.Filters.Add<EavExceptionFilter>();
        options.Filters.Add<ApiEnvelopeFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new DynamicCompositeValueJsonConverter());
        // 枚举序列化为字符串（与 EF Core HasConversion<string>() 对齐，前端 DTO 用 string 接收）
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddOpenApi();

// ★ 全局异常处理：把 EF/Npgsql 已知异常映射为 409/400
builder.Services.AddExceptionHandler<DbExceptionHandler>();
builder.Services.AddProblemDetails();

// .NET Aspire 集成:自动从 ConnectionStrings:TreeGraphDb 注入连接字符串
builder.AddNpgsqlDbContext<TreeGraphDbContext>(
    "TreeGraphDb",
    configureDbContextOptions: options =>
        options.UseNpgsql(npgsql => npgsql.MigrationsAssembly("TreeGraph.Api")));

builder.Services.AddMemoryCache();
builder.Services.AddScoped<TreeGraphRequestTransaction>();
// iNode 宿主守卫聚合器：inode 写链路（关注类型/首次写实体）校验宿主节点存在
builder.Services.AddScoped<InodeOwnerGuardRegistry>();
builder.Services.AddScoped<EavWriteService>();
builder.Services.AddScoped<EavReadService>();
builder.Services.AddScoped<EavQueryService>();
builder.Services.AddScoped<EavValidationService>();
builder.Services.AddScoped<CompositeValueService>();

// ★ 文件存储级联删除客户端：EavWriteService 注入 IFileStorageDeleter，
//   在 SaveChanges 成功后通知 FileStorageApi 删除孤儿文件对象。
//   BaseAddress 优先用 Aspire 服务发现注入的 HttpClient 命名 "FileStorageApi"；
//   本地开发无 Aspire 时 fallback 到 Configuration["FileStorageApi:BaseAddress"]。
var fileStorageBaseAddr = builder.Configuration["FileStorageApi:BaseAddress"]
                         ?? "http://localhost:5060";

builder.Services.AddHttpClient<IFileStorageDeleter, FileStorageDeleter>(client =>
{
    client.BaseAddress = new Uri(fileStorageBaseAddr);
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHttpClient<IFileInventoryClient, FileInventoryClient>(client =>
{
    client.BaseAddress = new Uri(fileStorageBaseAddr);
    client.Timeout = TimeSpan.FromSeconds(10);
});

// ★ 孤儿文件清理后台服务：对比 EAV value_file_meta ↔ FileStorageApi 文件库存，
//   删除差集（FileStorageApi 有但 EAV 已不引用的孤儿）。
builder.Services
    .AddOptions<OrphanedCleanupOptions>()
    .Bind(builder.Configuration.GetSection(OrphanedCleanupOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHostedService<OrphanedFileCleanupService>();
builder.Services.AddScoped<IInodeEntityService, InodeEntityService>();
builder.Services.AddScoped<InodeEavFacade>();
builder.Services.AddSingleton<IAttributeCache, AttributeCache>();
builder.Services.AddSingleton<ICompositeTypeCache, CompositeTypeCache>();
builder.Services.AddSingleton<ICustomTableCache, CustomTableCache>();
builder.Services.AddSingleton<IOptionSetCache, OptionSetCache>();

// ★ 子树加载服务（供 MafWfApi NodeExpandService 调用）
builder.Services.AddScoped<SubtreeService>();

builder.Services.AddScoped<CustomTableValidationService>();
builder.Services.AddScoped<CustomTableWriteService>();
builder.Services.AddScoped<CustomTableReadService>();
builder.Services.AddScoped<CustomTableQueryService>();

// 核心只发布业务变更并接收反向清理命令；投影消费者全部位于 SyncServices。
var isTesting = builder.Environment.IsEnvironment("Testing");
if (!isTesting)
    builder.Host.UseWolverine(opts => opts.UseTreeGraphMessaging(builder.Configuration));

// 核心 Edge/Space 管理仍通过共享 Nebula 客户端执行 DDL，不注册投影同步器或后台任务。
builder.Services.AddNebulaGraphClient(builder.Configuration);

// LineSky 服务
builder.Services.AddScoped<TreeGraph.Api.LineSky.Services.LineStructureService>();

// EdgeSky 服务（NebulaGraph 对齐的顶级边模块）
builder.Services.AddScoped<TreeGraph.Api.EdgeSky.Services.EdgeTypeService>();
builder.Services.AddScoped<TreeGraph.Api.EdgeSky.Services.EdgeService>();

// SpaceNodeSky 服务（空间节点：归属空间的通用节点，PG-only，图侧经总线收敛）
builder.Services.AddScoped<TreeGraph.Api.SpaceNodeSky.Services.SpaceNodeService>();
// 组合属性 → 图 Tag 投影账本（PG-only；Version++/墓碑 + 事件发布，图侧消费者异步收敛）
builder.Services.AddScoped<TreeGraph.Api.SpaceNodeSky.Services.TagProjectionService>();
// Nebula DDL 同步网关：Testing 环境（EavApiFactory 集成测试）无 NebulaGraph，注 Noop 空桩
if (isTesting)
    builder.Services.AddScoped<TreeGraph.Api.EdgeSky.Services.IEdgeSchemaGateway,
        TreeGraph.Api.EdgeSky.Services.NoopEdgeSchemaGateway>();
else
    builder.Services.AddScoped<TreeGraph.Api.EdgeSky.Services.IEdgeSchemaGateway,
        TreeGraph.Api.EdgeSky.Services.NebulaEdgeSchemaGateway>();

// ★ iNode 宿主守卫（R3 收敛）：nodes 注册表守卫认领全部注册节点类型；
//   尚未收编进 nodes 的 Line 保留专用守卫。EAV 写入前校验 inodeId 真实存在。
builder.Services.AddScoped<IInodeOwnerGuard, TreeGraph.Api.NodeSky.Services.NodesRegistryGuard>();
builder.Services.AddScoped<IInodeOwnerGuard, TreeGraph.Api.LineSky.Services.LineOwnerGuard>();

// LineSky 模板（L1：快速预填；后端 ValidateCore 不感知模板，模板只在 UI 层预填默认值）
builder.Services.AddSingleton<ILineTemplate, RailwayFeedingTemplate>();
builder.Services.AddScoped<LineTemplateRegistry>();

// ★ TreeEavSky：计量单位树 / 分类树 / 空间树 / EAV 树（表 unit_trees、category_trees、space_trees、eav_trees，
//   前两棵随迁移 HasData 播种；自 DiberyTreeService clone，响应信封由 ApiEnvelopeFilter 统一包装）
builder.Services.AddScoped<TreeGraph.Api.TreeEavSky.Services.ITreeService<
    TreeGraph.Shared.TreeEavSky.Entities.UnitTree>,
    TreeGraph.Api.TreeEavSky.Services.UnitTreeService>();
builder.Services.AddScoped<TreeGraph.Api.TreeEavSky.Services.ITreeService<
    TreeGraph.Shared.TreeEavSky.Entities.CategoryTree>,
    TreeGraph.Api.TreeEavSky.Services.CategoryTreeService>();
builder.Services.AddScoped<TreeGraph.Api.TreeEavSky.Services.ITreeService<
    TreeGraph.Shared.TreeEavSky.Entities.KnowledgeTree>,
    TreeGraph.Api.TreeEavSky.Services.KnowledgeTreeService>();
builder.Services.AddScoped<TreeGraph.Api.TreeEavSky.Services.ITreeService<
    TreeGraph.Shared.TreeEavSky.Entities.SpaceTree>,
    TreeGraph.Api.TreeEavSky.Services.SpaceTreeService>();
builder.Services.AddScoped<TreeGraph.Api.TreeEavSky.Services.ITreeService<
    TreeGraph.Shared.TreeEavSky.Entities.EavTree>,
    TreeGraph.Api.TreeEavSky.Services.EavTreeService>();

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

if (!isTesting)
    app.UseMiddleware<TreeGraphWriteTransactionMiddleware>();

app.MapControllers();

// ★ Aspire 默认端点（/health、/alive），供 Dashboard 探测
app.MapDefaultEndpoints();

// 启动时应用迁移并注入种子数据（EF 设计期跳过，避免 dotnet-ef 连接数据库）
if (!EF.IsDesignTime)
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<TreeGraphDbContext>();
    await db.Database.MigrateAsync();
    await EavSeeder.SeedAsync(db);
    var tagProjections = scope.ServiceProvider
        .GetRequiredService<TreeGraph.Api.SpaceNodeSky.Services.TagProjectionService>();
    await tagProjections.BackfillAsync(null);
    // 内置边类型不再自动播种：由「空间维护 → 关系设置 → 创建默认边（预置关系）」按需创建
    // 存量树节点 parent_of 派生边回填（幂等补插缺失行；仅对已存在 parent_of 边类型的空间生效）
    var backfilled = await ParentEdgeBackfill.BackfillAsync(db);
    if (backfilled > 0)
        app.Logger.LogInformation("parent_of 派生边回填完成：补插 {Count} 条", backfilled);

    // E2E 基线（item/user/project + 固定 EntityId 示例值）仅开发/测试环境注入
    if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
    {
        await E2eBaselineSeeder.SeedAsync(db);
    }
}

app.Run();

// ★ 让 WebApplicationFactory<Program> 可引用（集成测试）
public partial class Program { }
