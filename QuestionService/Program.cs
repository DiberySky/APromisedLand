using Elastic.Clients.Elasticsearch;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using QuestionService.Configs;
using QuestionService.Data;
using QuestionService.Services;
using Typesense.Setup;
using Wolverine;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.AddServiceDefaults();

builder.Services.AddMemoryCache();
builder.AddRedisClient(connectionName: "redis");

builder.Services.AddScoped<TagService>();

// var keycloakUrl = builder.Configuration["services:Keycloak:https:0"];
// if (string.IsNullOrEmpty(keycloakUrl))
//     throw new InvalidOperationException("配置中未找到 Keycloak URI。");
// //string? Authority = Environment.GetEnvironmentVariable("Keycloak__Authority");
//
// var authority = $"{keycloakUrl}/realms/{ProjectService.Realm}";
//
// builder.Services.AddAuthentication()
//     .AddKeycloakJwtBearer(
//         serviceName:  "keycloak",
//         realm:  "apromisedland",
//         options =>
//         {
//             options.TokenValidationParameters.ValidateIssuer = true;
//             options.Authority = authority; 
//             options.Audience = "diberysky";
//             if (builder.Environment.IsDevelopment())
//             {
//                 options.RequireHttpsMetadata = false;
//             }
//         });

builder.AddKeycloakService();

builder.AddNpgsqlDbContext<QuestionDbContext>("questionDb");

// 注册 Elasticsearch 客户端（使用单例）
// builder.AddElasticsearchClient("Elasticsearch");
builder.AddElasticsearchService();
    
builder.AddTypesensService();

// builder.AddWolverineToTypesenseService();

builder.AddNatsService();

// builder.AddWolverineToElasticsearchService();

// // ---------- Typesense 配置 ----------
// var typesenseUri = builder.Configuration["services:typesense:typesense:0"];
// if (string.IsNullOrEmpty(typesenseUri))
//     throw new InvalidOperationException("配置中未找到 Typesense URI。");
//
// var typesenseApiKey = builder.Configuration["typesense-api-key"];
// if (string.IsNullOrEmpty(typesenseApiKey))
//     throw new InvalidOperationException("配置中未找到 Typesense API 密钥");
//
// var uri = new Uri(typesenseUri);
// builder.Services.AddTypesenseClient(config =>
// {
//     config.ApiKey = typesenseApiKey;
//     config.Nodes = new List<Node>
//     {
//         new(uri.Host, uri.Port.ToString(), uri.Scheme)
//     };
// });

// builder.Services.AddOpenTelemetry().WithTracing(traceProviderBuilder =>
// {
//     traceProviderBuilder.SetResourceBuilder(ResourceBuilder.CreateDefault()
//             .AddService(builder.Environment.ApplicationName))
//         .AddSource("Wolverine");
// });
//
// builder.Host.UseWolverine(opts =>
// {
//     opts.UseRabbitMqUsingNamedConnection("RabbitMQ").AutoProvision();
//     opts.PublishAllMessages().ToRabbitExchange("questions");
// });

// // 添加 NATS 连接（使用 Aspire 的扩展，或手动注册）
// builder.AddNatsClient("Nats"); // 假设存在扩展方法，或直接注册
// // 注册发布服务
// builder.Services.AddScoped<IQuestionPublisher, QuestionPublisher>();
//
// // 注册后台消费者（托管服务）
// builder.Services.AddHostedService<NatsQuestionConsumer>();

var app = builder.Build();

// ... 管道配置
using (var initScope = app.Services.CreateScope())
{
    var client = initScope.ServiceProvider.GetRequiredService<ElasticsearchClient>();
    // await ElasticIndexInitializer.EnsureIndexAsync(client);
    await QuestionIndexService.InitializeIndexAsync(client);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapControllers();

app.MapDefaultEndpoints();

using var scope = app.Services.CreateScope();
var services = scope.ServiceProvider;
try
{
    var context = services.GetRequiredService<QuestionDbContext>();
    await context.Database.MigrateAsync();
}
catch (Exception e)
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    logger.LogError(e, "在迁移或初始化数据库时出现了错误。");
}

app.Run();