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

builder.AddKeycloakService();

builder.AddNpgsqlDbContext<QuestionDbContext>("questionDb");

// 注册 Elasticsearch 客户端（使用单例）
// builder.AddElasticsearchClient("Elasticsearch");
// builder.AddElasticsearchService();
    
// builder.AddTypesensService();

// builder.AddWolverineToTypesenseService();

// builder.AddNatsService();

// builder.AddWolverineToElasticsearchService();

var app = builder.Build();

// ... 管道配置
// using (var initScope = app.Services.CreateScope())
// {
//     var client = initScope.ServiceProvider.GetRequiredService<ElasticsearchClient>();
//     // await ElasticIndexInitializer.EnsureIndexAsync(client);
//     await QuestionIndexService.InitializeIndexAsync(client);
// }

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