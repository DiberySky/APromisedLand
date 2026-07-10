using QuestionService.Services;

namespace QuestionService.Configs;

public static class ElasticsearchConfig
{
    public static void AddElasticsearchService(this WebApplicationBuilder builder)
    {
        // 注册 Elasticsearch 客户端（使用单例）
        builder.AddElasticsearchClient("Elasticsearch");

        builder.Services.AddSingleton<IEmbeddingGenerator, EmbeddingGenerator>();
        
    }
}