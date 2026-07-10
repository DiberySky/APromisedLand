using APromisedLand.Api.MessageContracts;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace APromisedLand.Api.Projects.Elasticsearch.Services;

public class ElasticsearchIndexInitializer(ElasticsearchClient client, 
    ILogger<ElasticsearchIndexInitializer> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var indexName = "questions";
        var existsResponse = await client.Indices.ExistsAsync(indexName, cancellationToken);

        if (!existsResponse.IsValidResponse || !existsResponse.Exists)
        {
            var createResponse = await client.Indices.CreateAsync(indexName, c => c
                    .Settings(s => s
                        .NumberOfShards(1)
                        .NumberOfReplicas(0)
                        .Analysis(a => a
                            .Analyzers(an => an
                                .Custom("ik_smart", cus => cus
                                    .Tokenizer("ik_smart")
                                )
                            )
                        )
                    )
                    .Mappings(m => m
                        .Properties<ElasticQuestion>(p => p
                            // 字段名由 C# 属性名自动推断，无需再写 Name()
                            .Keyword(k => k.Id)
                            .Text(t => t.Title, txt => txt.Analyzer("ik_smart"))
                            .Text(t => t.Content, txt => txt.Analyzer("ik_smart"))
                            .Keyword(k => k.Tags)
                            .Date(d => d.CreatedAt)
                            .Boolean(b => b.HasAcceptedAnswer)
                            .IntegerNumber(n => n.AnswerCount)
                            // 向量字段：使用字符串参数，不再用枚举
                            .DenseVector(dv => new DenseVectorProperty
                            {
                                Dims = 1024,
                                Index = true,
                                IndexOptions = new DenseVectorIndexOptions{Type = 0},
                                Similarity = DenseVectorSimilarity.Cosine
                            })
                        )
                    )
                , cancellationToken);

            if (!createResponse.IsValidResponse)
                logger.LogError("创建索引失败: {DebugInfo}", createResponse.DebugInformation);
            else
                logger.LogInformation("索引 'questions' 创建成功（含向量字段）");
        }
        else
        {
            logger.LogInformation("索引 'questions' 已存在，跳过创建");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/* 中文分词搜索
public class ElasticsearchIndexInitializer(ElasticsearchClient client,
ILogger<ElasticsearchIndexInitializer> logger) : IHostedService
{
public async Task StartAsync(CancellationToken cancellationToken)
{
    try
    {
        await ElasticsearchIndexSetup.EnsureIndexAsync(client);
        logger.LogInformation("Elasticsearch 索引初始化完成");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Elasticsearch 索引初始化失败");
    }
}

public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
*/