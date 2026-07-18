using APromisedLand.Api.MessageContracts;
using Elastic.Clients.Elasticsearch;

namespace QuestionService.Services;

public static class QuestionIndexService
{
    // private readonly IEmbeddingGenerator embedder = embedder;
    // private readonly string _indexName = "questions";

    public static async Task InitializeIndexAsync(ElasticsearchClient elastic)
    {
        var existsResponse = await elastic.Indices.ExistsAsync("questions");
        bool indexExists = existsResponse.Exists;

        if (indexExists)
        {
            // var mappingResponse = await elastic.Indices.GetMappingAsync<ElasticQuestion>(idx => idx.Index(_indexName));
            // var sourceExcludes = mappingResponse.Indices[_indexName].Mappings?.Source?.Excludes;
            //
            // if (sourceExcludes != null && 
            //     (sourceExcludes.Contains("titleVector") || sourceExcludes.Contains("contentVector")))
            // {
            //     logger.LogWarning("检测到旧索引映射排除了向量字段，正在删除重建...");
            //     await elastic.Indices.DeleteAsync(_indexName);
            //     indexExists = false;
            // }
        }

        if (!indexExists)
        {
            var createResponse = await elastic.Indices.CreateAsync<ElasticQuestion>("questions", c => c
                .Mappings(m => m
                    .Properties(ps => ps
                        .Text(t => t.Title)
                        .Text(t => t.Content)
                        .Keyword(k => k.Tags)
                        .Date(d => d.CreatedAt)
                        .Boolean(b => b.HasAcceptedAnswer)
                        .IntegerNumber(i => i.AnswerCount)
                        .DenseVector(dv => dv.TitleVector!.Index())
                        .DenseVector(dv => dv.ContentVector!.Index())
                    )
                )
            );

            if (!createResponse.IsValidResponse)
                throw new Exception($"索引创建失败: {createResponse.DebugInformation}");

            Console.WriteLine("索引创建成功，向量将存储于 _source", "questions");
        }
        else
        {
            Console.WriteLine("索引已存在且映射正确", "questions");
        }
    }
}