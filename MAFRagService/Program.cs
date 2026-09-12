using Hangfire;
using MAFRagService.Initializers;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using MAFRagService.Startup.Configuration;
using MAFRagService.Startup.Extensions;
using MAFRagService.Startup;
using MAFRagService.Tools;
using Microsoft.Agents.AI.Hosting;

var builder = WebApplication.CreateBuilder(args);
var config  = builder.Configuration;
var env     = builder.Environment;

// ============================================================
// 1) Options
// ============================================================
builder.Services.AddRagOptions(config);

// ============================================================
// 2) 连接串
// ============================================================
var conns = ConnectionStrings.Resolve(config, env, warn: Console.WriteLine);

if (string.IsNullOrWhiteSpace(config.GetConnectionString("nebula")))
    config["ConnectionStrings:nebula"]    = conns.Nebula;
if (string.IsNullOrWhiteSpace(config.GetConnectionString("seaweedfs")))
    config["ConnectionStrings:seaweedfs"] = conns.SeaweedFs;
if (string.IsNullOrWhiteSpace(config.GetConnectionString("weaviate")))
    config["ConnectionStrings:weaviate"]  = conns.Weaviate;

// ============================================================
// 3) 特性开关
// ============================================================
var features = config.GetSection(FeatureFlags.SectionName)
                     .Get<FeatureFlags>() ?? new FeatureFlags();
builder.Services.AddSingleton(features);

Console.WriteLine(
    "[FEATURES] Rag={0}, Graph={1}, Entity={2}, Agents={3}, Indexing={4}",
    features.Rag, features.Graph, features.Entity, features.Agents, features.Indexing);

// ============================================================
// ★ 3.5) MAF 注册（Phase 1 最小引入）
// ============================================================
builder.AddOllamaApiClient("chat-model").AddChatClient();

builder.AddAIAgent(
    name: "RagTestAgent",
    instructions: "你是测试 Agent，仅用于验证 MAF 包已正确引入。");

// ============================================================
// 4) 服务注册
// ============================================================
builder.Services
    .AddRagInfrastructure(conns, features)
    .AddRagApi()
    .AddRagBusinessServices(features)
    .AddRagAiServices(config, conns, features)
    .AddRagObservability(config)
    .AddRagHealthChecks(features);

// ============================================================
// 5) 启动初始化器
// ============================================================
builder.Services.AddTransient<DatabaseInitializer>();
builder.Services.AddTransient<SeaweedBucketInitializer>();
builder.Services.AddTransient<WeaviateSchemaInitializer>();
builder.Services.AddTransient<GraphSchemaInitializer>();

// ============================================================
// 6) Swagger / OpenAPI
// ============================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ============================================================
// 构建
// ============================================================
var app = builder.Build();

// ---------- 启动初始化 ----------
await StartupInitializers.RunAsync(app);

// ---------- 中间件 ----------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ---------- Hangfire Dashboard ----------
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization  = new[] { new HangfireDashboardAuthFilter() },
    DashboardTitle = "MAFRagServer 作业面板"
});

// ---------- 业务路由 ----------
app.MapControllers();

// ---------- 健康检查 ----------
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ready")
});

app.MapHealthChecks("/health");

// ============================================================
// ★ 临时验证（Phase 1）
//   仅日志，不引入额外 using / 属性，编译零风险。
// ============================================================
using (var scope = app.Services.CreateScope())
{
    var testAgent = scope.ServiceProvider
        .GetKeyedService<Microsoft.Agents.AI.AIAgent>("RagTestAgent");

    Console.WriteLine(
        "[MAF-VERIFY] RagTestAgent resolved: {0}, Name: {1}",
        testAgent is not null ? "OK" : "NULL",
        testAgent?.Name ?? "<null>");
}

app.Run();

public partial class Program;