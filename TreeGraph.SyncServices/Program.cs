using Microsoft.EntityFrameworkCore;
using TreeGraph.Persistence.Data;
using TreeGraph.SyncServices.GraphSync;
using TreeGraph.SyncServices.Messaging;
using TreeGraph.SyncServices.SemanticSearch;
using TreeGraph.WebShared.Infrastructure;
using TreeGraph.WebShared.NodeEavSky;
using Wolverine;

var builder = WebApplication.CreateBuilder(args);

// ★ Aspire ServiceDefaults：服务发现、健康检查、OpenTelemetry
builder.AddServiceDefaults();

builder.Services.AddControllers(options =>
    {
        // NodeEavSky 全局 ApiResponse 信封（与核心 API 对齐）
        options.Filters.Add<ApiEnvelopeFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new DynamicCompositeValueJsonConverter());
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.Services.AddMemoryCache();

// ★ 共享 TreeGraphDbContext（只读消费：同步器读 PG 数据做 Nebula/Qdrant 投影源）
builder.AddNpgsqlDbContext<TreeGraphDbContext>(
    "TreeGraphDb",
    configureDbContextOptions: options =>
        options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

// ★ 图同步管道（PostgreSQL → NebulaGraph，经 TreeGraph.NebulaFastApi REST 网关）
builder.Services.AddGraphSync(builder.Configuration);

// ★ 语义搜索 + 向量同步管道（Qdrant + vLLM bge-m3）
builder.Services.AddSemanticSearch(builder.Configuration);

// ★ Wolverine 消息总线（消费端）：监听 RabbitMQ 队列，接收核心 API 发布的变更事件。
//   Wolverine 按约定自动发现本程序集中的 *Consumer handler（向量同步 + 图同步）。
builder.Host.UseWolverine(opts => opts.UseSyncServicesMessaging(builder.Configuration));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.MapControllers();

// ★ Aspire 默认端点（/health、/alive），供 Dashboard 探测
app.MapDefaultEndpoints();

app.Run();

// ★ 让 WebApplicationFactory<Program> 可引用（集成测试）
public partial class Program { }
