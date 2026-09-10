// using Hangfire;
// using MAFRagService.Initializers;
// using MAFRagService.Startup.Configuration;
// using MAFRagService.Startup.Extensions;
// using MAFRagService.Tools;
//
// var builder = WebApplication.CreateBuilder(args);
// var config  = builder.Configuration;
// var env     = builder.Environment;
//
// // ============================================================
// // 1) 绑定强类型配置
// // ============================================================
// builder.Services.Configure<OllamaOptions>(
//     config.GetSection(OllamaOptions.SectionName));
// builder.Services.Configure<SeaweedFsOptions>(
//     config.GetSection(SeaweedFsOptions.SectionName));
// builder.Services.Configure<WeaviateOptions>(
//     config.GetSection(WeaviateOptions.SectionName));
// builder.Services.Configure<JwtOptions>(
//     config.GetSection(JwtOptions.SectionName));
//
// // ============================================================
// // 2) 一次性解析连接串
// // ============================================================
// var conns = ConnectionStrings.Resolve(config, env, warn: Console.WriteLine);
//
// // ============================================================
// // 3) 服务注册（按关注点拆分）
// // ============================================================
// builder.Services
//     .AddRagInfrastructure(conns)
//     .AddRagAuth(config, env)
//     .AddRagBusinessServices()
//     .AddRagAiServices(config, conns)
//     .AddRagObservability(config)
//     .AddRagHealthChecks();
//
// // 启动初始化器
// builder.Services.AddTransient<DatabaseInitializer>();
// builder.Services.AddTransient<GraphSchemaInitializer>();
// builder.Services.AddTransient<WeaviateSchemaInitializer>();
// builder.Services.AddTransient<SeaweedBucketInitializer>();
//
// // API 文档
// builder.Services.AddEndpointsApiExplorer();
// builder.Services.AddSwaggerGen();
//
// // ============================================================
// // 构建
// // ============================================================
// var app = builder.Build();
//
// // ---------- 启动初始化（并行） ----------
// await app.RunAsync();
//
// // ---------- 中间件 ----------
// if (app.Environment.IsDevelopment())
// {
//     app.UseSwagger();
//     app.UseSwaggerUI();
// }
//
// app.UseAuthentication();
// app.UseAuthorization();
//
// app.UseHangfireDashboard("/hangfire", new DashboardOptions
// {
//     Authorization  = new[] { new HangfireDashboardAuthFilter() },
//     DashboardTitle = "MAFRagServer 作业面板"
// });
//
// app.MapControllers();
// app.MapHealthChecks("/health");
//
// app.Run();