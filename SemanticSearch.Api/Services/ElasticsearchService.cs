using APromisedLand.Shared.Services;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;
using SemanticSearch.Api.Models;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.QueryDsl;

namespace SemanticSearch.Api.Services;

public class ElasticsearchService(ElasticsearchClient client, 
    EmbeddingService embeddingService)
{
    private const string IndexName = "documents";

    // 创建索引映射（含 dense_vector）
    public async Task CreateIndexAsync()
    {
        var existsResponse = await client.Indices.ExistsAsync(IndexName);
        if (existsResponse.Exists)
            return;

        var createResponse = await client.Indices.CreateAsync(IndexName, c => c
            .Mappings(m => m
                .Properties<Document>(p => p           // 显式指定文档类型
                    .IntegerNumber(n => n.Id, i => i.Index(false))
                    .Text(t => t.Title, txt => txt.Analyzer("ik_max_word"))
                    .Text(t => t.Content, txt => txt.Analyzer("ik_max_word"))
                    .DenseVector(d => d.Embedding, dv => dv
                        .Dims(1024)
                        .Index(true)
                        .Similarity(DenseVectorSimilarity.Cosine)
                        .IndexOptions(opt => opt
                            .Type(DenseVectorIndexOptionsType.Hnsw)   // 必须显式设置
                            .M(16)
                            .EfConstruction(100)
                        )
                    )
                )
            )
        );

        if (!createResponse.IsValidResponse)
            throw new Exception($"创建索引失败: {createResponse.DebugInformation}");
    }

    // 批量索引示例数据
    public async Task IndexSampleDataAsync(IEnumerable<Document> documents)
    {
        // 生成向量（若未提供）
        var docsWithEmbedding = new List<Document>();
        foreach (var doc in documents)
        {
            if (doc.Embedding == null || doc.Embedding.Length == 0)
            {
                doc.Embedding = await embeddingService.GetEmbeddingAsync(doc.Content);
            }
            docsWithEmbedding.Add(doc);
        }

        // 使用 Bulk 索引
        var response = await client.BulkAsync(b => b
            .Index(IndexName)
            .IndexMany(docsWithEmbedding)
        );

        if (!response.IsValidResponse)
            throw new Exception($"批量索引失败: {response.DebugInformation}");
    }

    // 确保索引存在并填充示例数据
    public async Task EnsureIndexAndDataAsync()
    {
        await CreateIndexAsync();

        // 检查是否有数据，若无则插入示例数据
        var countResponse = await client.CountAsync(c => c.Index(IndexName));
        if (countResponse.Count == 0)
        {
            var samples = SampleData.GetDocuments();
            await IndexSampleDataAsync(samples);
        }
    }

    // 语义搜索（kNN）
    public async Task<List<SearchResult>> SemanticSearchAsync(string query, int size = 5)
    {
        var queryVector = await embeddingService.GetEmbeddingAsync(query);

        var searchResponse = await client.SearchAsync<Document>(s => s
            .Index(IndexName)
            .Size(size)
            .MinScore(ProjectService.MinScore)  // 只返回相似度 ≥ 0.80 的文档
            .Query(q => q
                .Knn(k => k
                    .Field(f => f.Embedding)
                    .QueryVector(queryVector)
                    .K(size)
                    .NumCandidates(100)
                )
            )
            .Source(new SourceFilter
            {
                Includes = new[] { "id", "title", "content" }
            })
        );
        
        return searchResponse.Hits
            .Select(hit => new SearchResult
            {
                Id = hit.Source.Id,
                Title = hit.Source.Title,
                Content = hit.Source.Content,
                Score = (float)(hit.Score ?? 0)
            })
            .ToList();
    }
    
    // 在 ElasticsearchService.cs 中添加以下方法

// 分词搜索（全文检索）
    public async Task<List<SearchResult>> TextSearchAsync(string query, int size = 5)
    {
        var searchResponse = await client.SearchAsync<Document>(s => s
            .Index(IndexName)
            .Size(size)
            .Query(q => q
                .MultiMatch(mm => mm
                        .Fields(new[] { "title^2", "content" })
                        .Query(query)
                        .Analyzer("ik_max_word")      // 显式使用ik分词器（若未指定会使用索引默认）
                        .Operator(Operator.Or)         // 默认OR，可根据需要调整
                )
            )
            .Source(new SourceFilter
            {
                Includes = new[] { "id", "title", "content" }
            })
        );

        return searchResponse.Hits
            .Select(hit => new SearchResult
            {
                Id = hit.Source.Id,
                Title = hit.Source.Title,
                Content = hit.Source.Content,
                Score = (float)(hit.Score ?? 0)
            })
            .ToList();
    }
    
    // 混合搜索（BM25 + kNN）
    public async Task<List<SearchResult>> HybridSearchAsync(string query, int size = 5, 
        float knnBoost = 1.0f, float bm25Boost = 1.0f)
    {
        var queryVector = await embeddingService.GetEmbeddingAsync(query);

        var searchResponse = await client.SearchAsync<Document>(s => s
            .Index(IndexName)
            .Size(size)
            .Query(q => q
                .MultiMatch(mm => mm
                        .Fields(new[] { "title^2", "content" })
                        .Query(query)
                        .Analyzer("ik_max_word")
                        .Operator(Operator.Or)
                        .Boost(bm25Boost)          // 文本部分权重
                )
            )
            .Knn(k => k
                    .Field(f => f.Embedding)
                    .QueryVector(queryVector)
                    .K(size * 2)                   // 召回更多候选（可调）
                    .NumCandidates(100)
                    .Boost(knnBoost)               // 向量部分权重
            )
            .MinScore(ProjectService.MinScore) // 可选：保留与语义搜索一致的最低分数
            .Source(new SourceFilter
            {
                Includes = new[] { "id", "title", "content" }
            })
        );

        return searchResponse.Hits
            .Select(hit => new SearchResult
            {
                Id = hit.Source.Id,
                Title = hit.Source.Title,
                Content = hit.Source.Content,
                Score = (float)(hit.Score ?? 0)
            })
            .ToList();
    }
}