using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.Data.Seeding;
using TreeGraph.Api.LineSky.Services;
using TreeGraph.Api.LineSky.Templates;
using TreeGraph.Api.EdgeSky.Services;
using TreeGraph.Api.GraphSyncSky;
using TreeGraph.Api.MessagingSky;
using TreeGraph.Api.NodeEavSky.Infrastructure;
using TreeGraph.Api.NodeEavSky.SemanticSearch;
using TreeGraph.Api.NodeEavSky.Services;
using TreeGraph.Api.TreeEavSky.Services;
using Wolverine;

var builder = WebApplication.CreateBuilder(args);

// ★ Aspire ServiceDefaults：服务发现、健康检查、OpenTelemetry
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddControllers(options =>
    {
        // ★ NodeEavSky 全局 ApiResponse 信封（过滤器内部按控制器命名空间自行判定生效范围）
        options.Filters.Add<ApiEnvelopeFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new NumericValueJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new DynamicCompositeValueJsonConverter());
        // 枚举序列化为字符串（与 EF Core HasConversion<string>() 对齐，前端 DTO 用 string 接收）
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddOpenApi();

// ★ 全局异常处理：把 EF/Npgsql 已知异常映射为 409/400
builder.Services.AddExceptionHandler<DbExceptionHandler>();
builder.Services.AddProblemDetails();

// .NET Aspire 集成:自动从 ConnectionStrings:TreeGraphDb 注入连接字符串
builder.AddNpgsqlDbContext<TreeGraphDbContext>("TreeGraphDb");

builder.Services.AddMemoryCache();
// 实体归属守卫聚合器（具体 guard 由各业务模块注册；无守卫时安全放行）
builder.Services.AddScoped<EntityOwnerGuardRegistry>();
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

// ★ 语义搜索 + 向量同步管道（Qdrant + vLLM bge-m3 + RabbitMQ 事件驱动）
builder.Services.AddSemanticSearch(builder.Configuration);

// ★ 图同步管道（PostgreSQL → NebulaGraph，经 TreeGraph.NebulaFastApi REST 网关）
//   Testing 环境（EavApiFactory 集成测试）无 RabbitMQ/NebulaGraph：
//   跳过 Wolverine 总线（消费者 handler 随 UseWolverine 一并跳过，IEventPublisher
//   由测试工厂注册 Noop 空桩）与 schema 初始化后台服务。
//   Wolverine 按约定自动发现本程序集中的 *Consumer handler（向量同步 + 图同步）。
var isTesting = builder.Environment.IsEnvironment("Testing");
if (!isTesting)
    builder.Host.UseWolverine(opts => opts.UseTreeGraphMessaging(builder.Configuration));
builder.Services.AddGraphSync(builder.Configuration, enableSchemaInitializer: !isTesting);

// Edge（关系/边）
builder.Services.AddScoped<IEdgeNodeValidator, EdgeNodeValidator>();
builder.Services.AddScoped<EdgeMetadataService>();
builder.Services.AddScoped<EdgeInstanceService>();
builder.Services.AddScoped<TreeGraph.Api.LineSky.Services.LineService>();
builder.Services.AddScoped<TreeGraph.Api.LineSky.Services.LineStructureService>();

// ★ TreeEavSky iNode 宿主守卫：节点关注类型 / 写入实体数据前校验 inodeId 是
//   真实的 UnitTree/CategoryTree/SpaceTree 节点，防止属性数据脱离宿主节点产生孤儿数据。
builder.Services.AddScoped<IInodeOwnerGuard, UnitTreeInodeGuard>();
builder.Services.AddScoped<IInodeOwnerGuard, CategoryTreeInodeGuard>();
builder.Services.AddScoped<IInodeOwnerGuard, KnowledgeTreeInodeGuard>();
builder.Services.AddScoped<IInodeOwnerGuard, SpaceTreeInodeGuard>();
builder.Services.AddScoped<IInodeOwnerGuard, TreeGraph.Api.LineSky.Services.LineNodeOwnerGuard>();
builder.Services.AddScoped<IInodeOwnerGuard, TreeGraph.Api.LineSky.Services.LineOwnerGuard>();

// LineSky 模板（L1：快速预填；后端 ValidateCore 不感知模板，模板只在 UI 层预填默认值）
builder.Services.AddSingleton<ILineTemplate, RailwayFeedingTemplate>();
builder.Services.AddScoped<LineTemplateRegistry>();
builder.Services.AddScoped<IInodeOwnerGuard, TreeGraph.Api.EdgeSky.Services.EdgeOwnerGuard>();

// ★ TreeEavSky：计量单位树 / 分类树 / 空间树（表 unit_trees、category_trees、space_trees，
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
    var db = scope.ServiceProvider.GetRequiredService<TreeGraphDbContext>();
    await db.Database.MigrateAsync();
    // 审计分区表：确保当月+下月分区存在（幂等；DEFAULT 分区已兜底，此处让数据按月归位）
    await AuditPartitionMaintenance.EnsureMonthlyPartitionsAsync(db);
    await EavSeeder.SeedAsync(db);
    // 内置 Inode 宿主类型（三树）：边端点下拉与属性挂载依赖目录可见（幂等 upsert）
    await BuiltinHostTypeSeeder.SeedAsync(db);

    // E2E 基线（item/user/project + 固定 EntityId 示例值）仅开发/测试环境注入
    if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
    {
        await E2eBaselineSeeder.SeedAsync(db);
    }
}

app.Run();

// ★ 让 WebApplicationFactory<Program> 可引用（集成测试）
public partial class Program { }
