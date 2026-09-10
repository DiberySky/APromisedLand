using Hangfire;
using MAFRagService.Initializers;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using MAFRagService.Startup.Configuration;
using MAFRagService.Startup.Extensions;   // ★ 关键：AddRagHealthChecks / AddRagOptions 等
using MAFRagService.Startup;
using MAFRagService.Tools; // ★ 关键：StartupInitializers 所在命名空间

var builder = WebApplication.CreateBuilder(args);
var config  = builder.Configuration;
var env     = builder.Environment;

// ============================================================
// 1) Options —— 绑定 + 校验
// ============================================================
builder.Services.AddRagOptions(config);

// ============================================================
// 2) 连接串 —— 集中解析 + 脱敏日志
// ============================================================
var conns = ConnectionStrings.Resolve(config, env, warn: Console.WriteLine);

// 回写配置，让 NebulaHealthCheck 等下游能从 IConfiguration 读到回退值
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
// 4) 服务注册
// ============================================================
builder.Services
    .AddRagInfrastructure(conns, features)      // Hangfire / EF / Redis / Nebula / S3
    .AddRagAuth(config, env)                    // JWT + MVC
    .AddRagBusinessServices(features)           // 业务服务
    .AddRagAiServices(config, conns)            // ★ 修正 1：去掉 features，只传 2 参
    .AddRagObservability(config)                // OpenTelemetry
    .AddRagHealthChecks(features);              // ★ 修正 2：确保已 using 对应命名空间

// ============================================================
// 5) 启动初始化器（Transient）
// ============================================================
builder.Services.AddTransient<DatabaseInitializer>();
builder.Services.AddTransient<GraphSchemaInitializer>();
builder.Services.AddTransient<WeaviateSchemaInitializer>();
builder.Services.AddTransient<SeaweedBucketInitializer>();

// ============================================================
// 6) Swagger / OpenAPI
// ============================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ============================================================
// 构建
// ============================================================
var app = builder.Build();

// ---------- 启动初始化（★ 修正 3）----------
// StartupInitializers 里定义的方法是 RunAsync(this WebApplication)
// 为避免与内置 WebApplication.RunAsync() 冲突，用静态调用形式：
await StartupInitializers.RunAsync(app);

// 如果你已把方法改名为 RunStartupInitializersAsync，则写：
// await app.RunStartupInitializersAsync();

// ---------- 中间件 ----------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// ---------- Hangfire Dashboard ----------
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization  = new[] { new HangfireDashboardAuthFilter() },
    DashboardTitle = "MAFRagServer 作业面板"
});

// ---------- 业务路由 ----------
app.MapControllers();

// ---------- 健康检查：三分端点 ----------
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ready")
});

app.MapHealthChecks("/health");

app.Run();