using Aspire.Elastic.Clients.Elasticsearch; 
using Elastic.Clients.Elasticsearch;
using ElasticsearchService.Embeds;
using ElasticsearchService.Services;
using OllamaSharp;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Wolverine;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

// ElasticsearchService/Program.cs
// builder.Services.AddHostedService<ElasticIndexInitializer>();
builder.Services.AddHostedService<ElasticsearchIndexInitializer>();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.AddServiceDefaults();

// 注册 Elasticsearch 客户端（使用单例）
builder.AddElasticsearchClient("Elasticsearch");

// 注册 Ollama 客户端（指向 Ollama 容器地址，假设容器内名称为 "Ollama"，端口 11434）
// builder.Services.AddSingleton<IOllamaApiClient>(sp => 
//     new OllamaApiClient(new Uri("http://Ollama:11434")));

// 从 Aspire 注入的连接字符串中读取 Ollama 地址
// var ollamaEndpoint = builder.Configuration.GetConnectionString("Ollama")
//                      ?? throw new InvalidOperationException("Missing connection string for 'Ollama'");
//
// builder.Services.AddSingleton<IOllamaApiClient>(sp =>
//     new OllamaApiClient(new Uri(ollamaEndpoint)));

// 注册 Ollama 客户端（利用 Aspire 服务发现）
builder.AddOllamaApiClient();

builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();

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
    opts.UseRabbitMqUsingNamedConnection("RabbitMQ").AutoProvision();

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