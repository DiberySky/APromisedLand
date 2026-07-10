using APromisedLand.Api.MessageContracts;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.Analysis; 

namespace QuestionService.Services;

public static class ElasticIndexInitializer
{
    public static async Task EnsureIndexAsync(ElasticsearchClient client)
    {
        var existsResponse = await client.Indices.ExistsAsync("questions");
        if (existsResponse.Exists) return;

        var createResponse = await client.Indices.CreateAsync("questions", c => c
            .Settings(s => s
                .Analysis(a => a
                    .Analyzers(an => an
                        .Custom("ik_analyzer", ca => ca
                            .Tokenizer("ik_max_word")
                            .Filter("lowercase")
                        )
                    )
                )
            )
            .Mappings(m => m
                .Properties<ElasticQuestion>(p => p
                    .Text(t => t.Title, t => t.Analyzer("ik_analyzer"))
                    .Text(t => t.Content, t => t.Analyzer("ik_analyzer"))
                    .Keyword(t => t.Tags, k => k.IgnoreAbove(256))
                    .Date(t => t.CreatedAt)
                    .Boolean(t => t.HasAcceptedAnswer)
                    .IntegerNumber(t => t.AnswerCount)
                    // 新增向量字段，维度 384
                    .DenseVector(v => v.TitleVector, d => d.Dims(1024).Index(true).Similarity(DenseVectorSimilarity.Cosine))
                    .DenseVector(v => v.ContentVector, d => d.Dims(1024).Index(true).Similarity(DenseVectorSimilarity.Cosine))
                )
            )
        );

        if (!createResponse.IsValidResponse)
            throw new Exception($"创建索引失败: {createResponse.DebugInformation}");
    }
}