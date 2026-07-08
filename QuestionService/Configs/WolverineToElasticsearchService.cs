using Elastic.Clients.Elasticsearch;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Wolverine;
using Wolverine.RabbitMQ;

namespace QuestionService.Configs;

public static class WolverineToElasticsearchService
{
    public static void AddWolverineToElasticsearchService(this WebApplicationBuilder builder)
    {
        
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

    }
}