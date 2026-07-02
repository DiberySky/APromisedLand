using Aspire.Elastic.Clients.Elasticsearch; 
using Elastic.Clients.Elasticsearch;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Wolverine;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.AddServiceDefaults();

// 注册 Elasticsearch 客户端（使用单例）
builder.AddElasticsearchClient("Elasticsearch");

// ---------- OpenTelemetry（可选） ----------
builder.Services.AddOpenTelemetry().WithTracing(traceProviderBuilder =>
{
    traceProviderBuilder.SetResourceBuilder(ResourceBuilder.CreateDefault()
            .AddService(builder.Environment.ApplicationName))
        .AddSource("Wolverine");
});

// ---------- Wolverine 消息总线配置 ----------
builder.Host.UseWolverine(opts =>
{
    // 使用命名的 RabbitMQ 连接（与 AppHost 中的 "Messaging" 对应）
    opts.UseRabbitMqUsingNamedConnection("Messaging").AutoProvision();

    // 监听专属队列，绑定到 "questions" 交换器
    opts.ListenToRabbitQueue("questions.elasticsearch", cfg =>
    {
        cfg.BindExchange("questions");
    });

    // 如果消息处理类需要依赖注入（如 IElasticClient），启用服务定位
    opts.CodeGeneration.AlwaysUseServiceLocationFor<ElasticsearchClient>();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapControllers();

app.MapDefaultEndpoints();

app.Run();