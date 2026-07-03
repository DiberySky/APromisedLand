using Elastic.Clients.Elasticsearch;
using ElasticsearchService.Models;

namespace ElasticsearchService.Services;

public class ElasticIndexInitializer(ElasticsearchClient client, ILogger<ElasticIndexInitializer> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        const string indexName = "questions";

        // 检查索引是否存在
        var existsResponse = await client.Indices.ExistsAsync(indexName, cancellationToken);
        if (existsResponse.Exists)
        {
            logger.LogInformation("索引 '{Index}' 已存在，跳过创建", indexName);
            return;
        }

        // 创建索引，直接指定使用 IK 分析器（无需自定义 Settings）
        var createResponse = await client.Indices.CreateAsync(indexName, c => c
            .Mappings(m => m
                .Properties<ElasticQuestion>(p => p
                        .Keyword(e => e.Id)
                        .Text(e => e.Title, t => t
                                .Analyzer("ik_max_word")      // 索引时最大分词
                                .SearchAnalyzer("ik_smart")   // 搜索时智能分词
                        )
                        .Text(e => e.Content, t => t
                            .Analyzer("ik_max_word")
                            .SearchAnalyzer("ik_smart")
                        )
                        .Keyword(e => e.Tags)              // 标签精确匹配
                        .Date(e => e.CreatedAt)
                        .Boolean(e => e.HasAcceptedAnswer)
                        .IntegerNumber(e => e.AnswerCount) // 或 .Number(e => e.AnswerCount, n => n.Type(NumberType.Integer))
                )
            )
        );

        if (createResponse.IsValidResponse)
            logger.LogInformation("索引 '{Index}' 创建成功", indexName);
        else
            logger.LogError("创建索引失败: {Debug}", createResponse.DebugInformation);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}