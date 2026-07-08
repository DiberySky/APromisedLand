using OllamaSharp;
using QuestionService.Nats.Consumers.Elasticsearch;
using QuestionService.Nats.Consumers.Typesense;
using QuestionService.Services;
using QuestionService.Services.Nats;
using QuestionService.Services.Nats.Publishers;

namespace QuestionService.Configs;

public static class NatsPublishConfig
{
    public static void AddNatsService(this WebApplicationBuilder builder)
    {
        // 添加 NATS 连接（使用 Aspire 的扩展，或手动注册）
        builder.AddNatsClient("Nats"); // 假设存在扩展方法，或直接注册

        // 注册 Ollama 客户端（利用 Aspire 服务发现）
        builder.AddOllamaApiClient();

        builder.Services.AddScoped<IOllamaEmbeddingService, OllamaEmbeddingService>();

        // 注册发布服务
        builder.Services.AddScoped<IQuestionPublisher, QuestionPublisher>();
        
        // 注册后台消费者（托管服务）
        builder.Services.AddHostedService<NatsQuestionTypesensConsumer>();
        builder.Services.AddHostedService<NatsQuestionElasticsearchConsumer>();
    }
    
    private static void AddOllamaApiClient(
        this IHostApplicationBuilder builder)
    {
        var endpoint = builder.Configuration["OLLAMA_URI"];
        if (endpoint is null)
            throw new Exception($"OLLAMA_URI 对应的配置缺失。");

        try
        {
            builder.Services.AddSingleton<IOllamaApiClient>(sp =>
                new OllamaApiClient(new Uri(endpoint)));
        }
        catch (Exception e)
        {
            throw new InvalidOperationException(e.Message, e);
        }
    }
}