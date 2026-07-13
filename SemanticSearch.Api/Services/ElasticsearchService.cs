using APromisedLand.Shared.Services;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;
using SemanticSearch.Api.Models;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.QueryDsl;
using SolutionService = APromisedLand.Shared.Services.Solution.SolutionService;

namespace SemanticSearch.Api.Services;

public class ElasticsearchService(ElasticsearchClient client, 
    EmbeddingService embeddingService)
{
    private const string IndexName = "documents";

    // 创建索引映射（含 dense_vector）
    private async Task CreateIndexAsync()
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
                        .Index()
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
    private async Task IndexSampleDataAsync(IEnumerable<Document> documents)
    {
        // 生成向量（若未提供）
        var docsWithEmbedding = new List<Document>();
        foreach (var doc in documents)
        {
            if (doc.Embedding.Length == 0)
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
        var countResponse = await client.CountAsync(c => c.Indices(IndexName));
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
            .Indices(IndexName)
            .Size(size)
            .MinScore(SolutionService.MinScore)  // 只返回相似度 ≥ 0.80 的文档
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
            .Where(x => x.Source != null)
            .Select(hit => new SearchResult
            {
                Id = hit.Source!.Id,
                Title = hit.Source.Title,
                Content = hit.Source.Content,
                Score = (float)(hit.Score ?? 0)
            })
            .ToList();
    }
    
    // 分词搜索（全文检索）
    public async Task<List<SearchResult>> TextSearchAsync(string query, int size = 5)
    {
        var searchResponse = await client.SearchAsync<Document>(s => s
            .Indices(IndexName)
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
            .Where(x => x.Source != null)
            .Select(hit => new SearchResult
            {
                Id = hit.Source!.Id,
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
            .Indices(IndexName)
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
            .MinScore(SolutionService.MinScore) // 可选：保留与语义搜索一致的最低分数
            .Source(new SourceFilter
            {
                Includes = new[] { "id", "title", "content" }
            })
        );

        return searchResponse.Hits
            .Where(x => x.Source != null)
            .Select(hit => new SearchResult
            {
                Id = hit.Source!.Id,
                Title = hit.Source.Title,
                Content = hit.Source.Content,
                Score = (float)(hit.Score ?? 0)
            })
            .ToList();
    }

    // 带过滤条件的 KNN 搜索
    public async Task<List<SearchResult>> KnnWithFilterAsync(string query, int size = 5,
        string? titleFilter = null, float? minScore = null)
    {
        var queryVector = await embeddingService.GetEmbeddingAsync(query);

        // 构建过滤条件列表
        var filterQueries = new List<Query>();
        
        // 标题过滤（前缀匹配）
        if (!string.IsNullOrEmpty(titleFilter))
        {
            filterQueries.Add(new PrefixQuery 
            { 
                Field = "title", 
                Value = titleFilter 
            });
        }

        var searchResponse = await client.SearchAsync<Document>(s => s
            .Indices(IndexName)
            .Size(size)
            .MinScore(minScore ?? SolutionService.MinScore)
            .Query(q => q
                .Bool(b => b
                    .Filter(filterQueries)
                    .Must(m => m
                        .Knn(k => k
                            .Field(f => f.Embedding)
                            .QueryVector(queryVector)
                            .K(size)
                            .NumCandidates(100)
                        )
                    )
                )
            )
            .Source(new SourceFilter
            {
                Includes = new[] { "id", "title", "content" }
            })
        );

        return searchResponse.Hits
            .Where(x => x.Source != null)
            .Select(hit => new SearchResult
            {
                Id = hit.Source!.Id,
                Title = hit.Source.Title,
                Content = hit.Source.Content,
                Score = (float)(hit.Score ?? 0)
            })
            .ToList();
    }

    // 自定义参数的 KNN 搜索
    public async Task<List<SearchResult>> KnnWithCustomParamsAsync(string query, int size = 5,
        int numCandidates = 100, float boost = 1.0f, float? minScore = null)
    {
        var queryVector = await embeddingService.GetEmbeddingAsync(query);

        var searchResponse = await client.SearchAsync<Document>(s => s
            .Indices(IndexName)
            .Size(size)
            .MinScore(minScore ?? SolutionService.MinScore)
            .Query(q => q
                .Knn(k => k
                    .Field(f => f.Embedding)
                    .QueryVector(queryVector)
                    .K(size)
                    .NumCandidates(numCandidates)
                    .Boost(boost)
                )
            )
            .Source(new SourceFilter
            {
                Includes = new[] { "id", "title", "content" }
            })
        );

        return searchResponse.Hits
            .Where(x => x.Source != null)
            .Select(hit => new SearchResult
            {
                Id = hit.Source!.Id,
                Title = hit.Source.Title,
                Content = hit.Source.Content,
                Score = (float)(hit.Score ?? 0)
            })
            .ToList();
    }

    // 向量相似度范围查询
    public async Task<List<SearchResult>> KnnRangeSearchAsync(string query, float minSimilarity,
        float maxSimilarity = 1.0f, int size = 5)
    {
        var queryVector = await embeddingService.GetEmbeddingAsync(query);

        // 使用 MinScore 过滤最低相似度，在内存中过滤最高相似度
        var searchResponse = await client.SearchAsync<Document>(s => s
            .Indices(IndexName)
            .Size(size * 10)
            .MinScore(minSimilarity)
            .Query(q => q
                .Knn(k => k
                    .Field(f => f.Embedding)
                    .QueryVector(queryVector)
                    .K(size * 10)
                    .NumCandidates(200)
                )
            )
            .Source(new SourceFilter
            {
                Includes = new[] { "id", "title", "content" }
            })
        );

        // 在内存中过滤分数范围并排序
        return searchResponse.Hits
            .Where(x => x.Source != null && (x.Score ?? 0) <= maxSimilarity)
            .Select(hit => new SearchResult
            {
                Id = hit.Source!.Id,
                Title = hit.Source.Title,
                Content = hit.Source.Content,
                Score = (float)(hit.Score ?? 0)
            })
            .OrderByDescending(r => r.Score)
            .Take(size)
            .ToList();
    }

    // 多查询向量的 KNN 搜索
    public async Task<List<SearchResult>> MultiVectorKnnSearchAsync(List<string> queries, int size = 5)
    {
        var queryVectors = new List<float[]>();
        foreach (var query in queries)
        {
            var vector = await embeddingService.GetEmbeddingAsync(query);
            queryVectors.Add(vector);
        }

        var allResults = new Dictionary<int, SearchResult>();
        
        foreach (var vector in queryVectors)
        {
            var searchResponse = await client.SearchAsync<Document>(s => s
                .Indices(IndexName)
                .Size(size * 2)
                .Query(q => q
                    .Knn(k => k
                        .Field(f => f.Embedding)
                        .QueryVector(vector)
                        .K(size * 2)
                        .NumCandidates(100)
                    )
                )
                .Source(new SourceFilter
                {
                    Includes = new[] { "id", "title", "content" }
                })
            );

            foreach (var hit in searchResponse.Hits.Where(h => h.Source != null))
            {
                var existing = allResults.TryGetValue(hit.Source!.Id, out var result);
                if (existing && result != null)
                {
                    result.Score = (result.Score + (float)(hit.Score ?? 0)) / 2;
                }
                else
                {
                    allResults[hit.Source.Id] = new SearchResult
                    {
                        Id = hit.Source.Id,
                        Title = hit.Source.Title,
                        Content = hit.Source.Content,
                        Score = (float)(hit.Score ?? 0)
                    };
                }
            }
        }

        return allResults.Values
            .OrderByDescending(r => r.Score)
            .Take(size)
            .ToList();
    }

    // 分页 KNN 搜索
    public async Task<List<SearchResult>> KnnSearchWithPaginationAsync(string query, 
        int page = 1, int pageSize = 10)
    {
        var queryVector = await embeddingService.GetEmbeddingAsync(query);

        var searchResponse = await client.SearchAsync<Document>(s => s
            .Indices(IndexName)
            .Size(pageSize)
            .From((page - 1) * pageSize)
            .MinScore(SolutionService.MinScore)
            .Query(q => q
                .Knn(k => k
                    .Field(f => f.Embedding)
                    .QueryVector(queryVector)
                    .K(pageSize)
                    .NumCandidates(100)
                )
            )
            .Source(new SourceFilter
            {
                Includes = new[] { "id", "title", "content" }
            })
        );

        return searchResponse.Hits
            .Where(x => x.Source != null)
            .Select(hit => new SearchResult
            {
                Id = hit.Source!.Id,
                Title = hit.Source.Title,
                Content = hit.Source.Content,
                Score = (float)(hit.Score ?? 0)
            })
            .ToList();
    }
    
    // 混合搜索+分页（BM25 + kNN + 分页）
    public async Task<List<SearchResult>> HybridSearchWithPaginationAsync(string query,
        int page = 1, int pageSize = 10, float knnBoost = 1.0f, float bm25Boost = 1.0f)
    {
        var queryVector = await embeddingService.GetEmbeddingAsync(query);

        var searchResponse = await client.SearchAsync<Document>(s => s
            .Indices(IndexName)
            .Size(pageSize)
            .From((page - 1) * pageSize)
            .Query(q => q
                .MultiMatch(mm => mm
                    .Fields(new[] { "title^2", "content" })
                    .Query(query)
                    .Analyzer("ik_max_word")
                    .Operator(Operator.Or)
                    .Boost(bm25Boost)
                )
            )
            .Knn(k => k
                .Field(f => f.Embedding)
                .QueryVector(queryVector)
                .K(pageSize * 2)
                .NumCandidates(100)
                .Boost(knnBoost)
            )
            .MinScore(SolutionService.MinScore)
            .Source(new SourceFilter { Includes = new[] { "id", "title", "content" } })
        );

        return searchResponse.Hits
            .Where(x => x.Source != null)
            .Select(hit => new SearchResult
            {
                Id = hit.Source!.Id,
                Title = hit.Source.Title,
                Content = hit.Source.Content,
                Score = (float)(hit.Score ?? 0)
            })
            .ToList();
    }
}