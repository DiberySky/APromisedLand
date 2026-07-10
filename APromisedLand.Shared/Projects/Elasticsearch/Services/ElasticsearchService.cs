using APromisedLand.Shared.Helper;
using APromisedLand.Shared.Projects.Elasticsearch.Models;
using APromisedLand.Shared.Projects.Nats.Models;
using APromisedLand.Shared.Services;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Clients.Elasticsearch.Core.Search;

namespace APromisedLand.Shared.Projects.Elasticsearch.Services;

public class ElasticsearchService(ElasticsearchClient client, 
    EmbeddingService embeddingService)
{
    private const string IndexName = "DiberyDocs";

    // 创建索引映射（含 dense_vector）
    private async Task CreateIndexAsync()
    {
        var existsResponse = await client.Indices.ExistsAsync(IndexName);
        if (existsResponse.Exists)
            return;

        var createResponse = await client.Indices.CreateAsync(IndexName, c => c
            .Mappings(m => m
                .Properties<DocumentData>(p => p
                    .Text(n => n.Id, i => i.Index(false))
                    .Text(t => t.Title, txt => txt.Analyzer("ik_max_word"))
                    .Text(t => t.Content, txt => txt.Analyzer("ik_max_word"))
                    .DenseVector(d => d.TitleVector, dv => dv
                        .Dims(1024)
                        .Index()
                        .Similarity(DenseVectorSimilarity.Cosine)
                        .IndexOptions(opt => opt
                            .Type(DenseVectorIndexOptionsType.Hnsw)
                            .M(16)
                            .EfConstruction(100)
                        )
                    )
                    .DenseVector(d => d.ContentVector, dv => dv
                        .Dims(1024)
                        .Index()
                        .Similarity(DenseVectorSimilarity.Cosine)
                        .IndexOptions(opt => opt
                            .Type(DenseVectorIndexOptionsType.Hnsw)
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
    private async Task IndexSampleDataAsync(IEnumerable<DocumentData> documents)
    {
        var docsWithEmbedding = new List<DocumentData>();
        foreach (var doc in documents)
        {
            if (doc.TitleVector.Length == 0)
            {
                doc.TitleVector = await embeddingService.GetEmbeddingAsync(doc.Title);
            }
            if (doc.ContentVector.Length == 0)
            {
                doc.ContentVector = await embeddingService.GetEmbeddingAsync(doc.Content);
            }
            docsWithEmbedding.Add(doc);
        }

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
        var countResponse = await client.CountAsync(c => c.Indices(IndexName));
        if (countResponse.Count == 0)
        {
            var samples = SampleData.GetDocuments();
            await IndexSampleDataAsync(samples);
        }
    }

    // 语义搜索（kNN）
    public async Task<List<ElasticsearchResult>> SemanticSearchAsync(string query, int size = 5,
        float titleBoost = SolutionHelper.TitleBoost, float contentBoost = SolutionHelper.ContentBoost)
    {
        var queryVector = await embeddingService.GetEmbeddingAsync(query);

        var searchResponse = await client.SearchAsync<DocumentData>(s => s
            .Indices(IndexName)
            .Size(size)
            .MinScore(SolutionService.MinScore)
            .Query(q => q
                .Bool(b => b
                    .Should(
                        qs => qs.Knn(k => k
                            .Field(f => f.TitleVector)
                            .QueryVector(queryVector)
                            .K(size)
                            .NumCandidates(100)
                            .Boost(titleBoost)
                        ),
                        qs => qs.Knn(k => k
                            .Field(f => f.ContentVector)
                            .QueryVector(queryVector)
                            .K(size)
                            .NumCandidates(100)
                            .Boost(contentBoost)
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
            .Select(hit => new ElasticsearchResult
            {
                Id = hit.Source!.Id,
                Title = hit.Source.Title,
                Content = hit.Source.Content,
                Score = (float)(hit.Score ?? 0)
            })
            .ToList();
    }
    
    // 分词搜索（全文检索）
    public async Task<List<ElasticsearchResult>> TextSearchAsync(string query, int size = 5,
        float titleBoost = SolutionHelper.TitleBoost, float contentBoost = SolutionHelper.ContentBoost)
    {
        var bm25Fields = new[] { $"title^{titleBoost}", $"content^{contentBoost}" };
        
        var searchResponse = await client.SearchAsync<DocumentData>(s => s
            .Indices(IndexName)
            .Size(size)
            .Query(q => q
                .MultiMatch(mm => mm
                        .Fields(bm25Fields)
                        .Query(query)
                        .Analyzer("ik_max_word")
                        .Operator(Operator.Or)
                )
            )
            .Source(new SourceFilter
            {
                Includes = new[] { "id", "title", "content" }
            })
        );

        return searchResponse.Hits
            .Where(x => x.Source != null)
            .Select(hit => new ElasticsearchResult
            {
                Id = hit.Source!.Id,
                Title = hit.Source.Title,
                Content = hit.Source.Content,
                Score = (float)(hit.Score ?? 0)
            })
            .ToList();
    }
    
    // 混合搜索（BM25 + kNN）
    public async Task<List<ElasticsearchResult>> HybridSearchAsync(string query, int size = 5, 
        float bm25Boost = SolutionHelper.Bm25Boost, float knnBoost = SolutionHelper.KnnBoost, 
        float titleBoost = SolutionHelper.TitleBoost, float contentBoost = SolutionHelper.ContentBoost)
    {
        var queryVector = await embeddingService.GetEmbeddingAsync(query);
        var bm25Fields = new[] { $"title^{titleBoost}", $"content^{contentBoost}" };

        var searchResponse = await client.SearchAsync<DocumentData>(s => s
                .Indices(IndexName)
                .Size(size)
                .Query(q => q
                    .MultiMatch(mm => mm
                            .Fields(bm25Fields)
                            .Query(query)
                            .Analyzer("ik_max_word")
                            .Operator(Operator.Or)
                            .Boost(bm25Boost)
                    )
                )
                .Knn(k => k
                        .Field(f => f.TitleVector)
                        .Field(f => f.ContentVector)
                        .QueryVector(queryVector)
                        .K(size * 2)
                        .NumCandidates(100)
                        .Boost(knnBoost)
                )
                .MinScore(SolutionService.MinScore)
                .Source(new SourceFilter
                {
                    Includes = new[] { "id", "title", "content" }
                })
        );

        return searchResponse.Hits
            .Where(x => x.Source != null)
            .Select(hit => new ElasticsearchResult
            {
                Id = hit.Source!.Id,
                Title = hit.Source.Title,
                Content = hit.Source.Content,
                Score = (float)(hit.Score ?? 0)
            })
            .ToList();
    }

    // 带过滤条件的 KNN 搜索
    public async Task<List<ElasticsearchResult>> KnnWithFilterAsync(string query, int size = 5,
        string? titleFilter = null, float? minScore = null,
        float titleBoost = SolutionHelper.TitleBoost, float contentBoost = SolutionHelper.ContentBoost)
    {
        var queryVector = await embeddingService.GetEmbeddingAsync(query);

        var filterQueries = new List<Query>();
        if (!string.IsNullOrEmpty(titleFilter))
        {
            filterQueries.Add(new PrefixQuery 
            { 
                Field = "title", 
                Value = titleFilter 
            });
        }

        var searchResponse = await client.SearchAsync<DocumentData>(s => s
            .Indices(IndexName)
            .Size(size)
            .MinScore(minScore ?? SolutionService.MinScore)
            .Query(q => q
                .Bool(b => b
                    .Filter(filterQueries)
                    .Must(m => m
                        .Bool(b2 => b2
                            .Should(
                                qs => qs.Knn(k => k
                                    .Field(f => f.TitleVector)
                                    .QueryVector(queryVector)
                                    .K(size)
                                    .NumCandidates(100)
                                    .Boost(titleBoost)
                                ),
                                qs => qs.Knn(k => k
                                    .Field(f => f.ContentVector)
                                    .QueryVector(queryVector)
                                    .K(size)
                                    .NumCandidates(100)
                                    .Boost(contentBoost)
                                )
                            )
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
            .Select(hit => new ElasticsearchResult
            {
                Id = hit.Source!.Id,
                Title = hit.Source.Title,
                Content = hit.Source.Content,
                Score = (float)(hit.Score ?? 0)
            })
            .ToList();
    }

    // 自定义参数的 KNN 搜索
    public async Task<List<ElasticsearchResult>> KnnWithCustomParamsAsync(string query, int size = 5,
        int numCandidates = 100, float boost = SolutionHelper.KnnBoost, float? minScore = null,
        float titleBoost = SolutionHelper.TitleBoost, float contentBoost = SolutionHelper.ContentBoost)
    {
        var queryVector = await embeddingService.GetEmbeddingAsync(query);

        var searchResponse = await client.SearchAsync<DocumentData>(s => s
            .Indices(IndexName)
            .Size(size)
            .MinScore(minScore ?? SolutionService.MinScore)
            .Query(q => q
                .Bool(b => b
                    .Should(
                        qs => qs.Knn(k => k
                            .Field(f => f.TitleVector)
                            .QueryVector(queryVector)
                            .K(size)
                            .NumCandidates(numCandidates)
                            .Boost(boost * titleBoost)
                        ),
                        qs => qs.Knn(k => k
                            .Field(f => f.ContentVector)
                            .QueryVector(queryVector)
                            .K(size)
                            .NumCandidates(numCandidates)
                            .Boost(boost * contentBoost)
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
            .Select(hit => new ElasticsearchResult
            {
                Id = hit.Source!.Id,
                Title = hit.Source.Title,
                Content = hit.Source.Content,
                Score = (float)(hit.Score ?? 0)
            })
            .ToList();
    }

    // 向量相似度范围查询
    public async Task<List<ElasticsearchResult>> KnnRangeSearchAsync(string query, float minSimilarity,
        float maxSimilarity = 1.0f, int size = 5,
        float titleBoost = SolutionHelper.TitleBoost, float contentBoost = SolutionHelper.ContentBoost)
    {
        var queryVector = await embeddingService.GetEmbeddingAsync(query);

        var searchResponse = await client.SearchAsync<DocumentData>(s => s
            .Indices(IndexName)
            .Size(size * 10)
            .MinScore(minSimilarity)
            .Query(q => q
                .Bool(b => b
                    .Should(
                        qs => qs.Knn(k => k
                            .Field(f => f.TitleVector)
                            .QueryVector(queryVector)
                            .K(size * 10)
                            .NumCandidates(200)
                            .Boost(titleBoost)
                        ),
                        qs => qs.Knn(k => k
                            .Field(f => f.ContentVector)
                            .QueryVector(queryVector)
                            .K(size * 10)
                            .NumCandidates(200)
                            .Boost(contentBoost)
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
            .Where(x => x.Source != null && (x.Score ?? 0) <= maxSimilarity)
            .Select(hit => new ElasticsearchResult
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
    public async Task<List<ElasticsearchResult>> MultiVectorKnnSearchAsync(List<string> queries, int size = 5,
        float titleBoost = SolutionHelper.TitleBoost, float contentBoost = SolutionHelper.ContentBoost)
    {
        var queryVectors = new List<float[]>();
        foreach (var query in queries)
        {
            var vector = await embeddingService.GetEmbeddingAsync(query);
            queryVectors.Add(vector);
        }

        var allResults = new Dictionary<string, ElasticsearchResult>();
        
        foreach (var vector in queryVectors)
        {
            var searchResponse = await client.SearchAsync<DocumentData>(s => s
                .Indices(IndexName)
                .Size(size * 2)
                .Query(q => q
                    .Bool(b => b
                        .Should(
                            qs => qs.Knn(k => k
                                .Field(f => f.TitleVector)
                                .QueryVector(vector)
                                .K(size * 2)
                                .NumCandidates(100)
                                .Boost(titleBoost)
                            ),
                            qs => qs.Knn(k => k
                                .Field(f => f.ContentVector)
                                .QueryVector(vector)
                                .K(size * 2)
                                .NumCandidates(100)
                                .Boost(contentBoost)
                            )
                        )
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
                    allResults[hit.Source.Id] = new ElasticsearchResult
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
    public async Task<List<ElasticsearchResult>> KnnSearchWithPaginationAsync(string query, 
        int page = 1, int pageSize = 10,
        float titleBoost = SolutionHelper.TitleBoost, float contentBoost = SolutionHelper.ContentBoost)
    {
        var queryVector = await embeddingService.GetEmbeddingAsync(query);

        var searchResponse = await client.SearchAsync<DocumentData>(s => s
            .Indices(IndexName)
            .Size(pageSize)
            .From((page - 1) * pageSize)
            .MinScore(SolutionService.MinScore)
            .Query(q => q
                .Bool(b => b
                    .Should(
                        qs => qs.Knn(k => k
                            .Field(f => f.TitleVector)
                            .QueryVector(queryVector)
                            .K(pageSize)
                            .NumCandidates(100)
                            .Boost(titleBoost)
                        ),
                        qs => qs.Knn(k => k
                            .Field(f => f.ContentVector)
                            .QueryVector(queryVector)
                            .K(pageSize)
                            .NumCandidates(100)
                            .Boost(contentBoost)
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
            .Select(hit => new ElasticsearchResult
            {
                Id = hit.Source!.Id,
                Title = hit.Source.Title,
                Content = hit.Source.Content,
                Score = (float)(hit.Score ?? 0)
            })
            .ToList();
    }
    
    // 混合搜索+分页（BM25 + kNN + 分页）
    public async Task<List<ElasticsearchResult>> HybridSearchWithPaginationAsync(string query,
        int page = 1, int pageSize = 10,
        float bm25Boost = SolutionHelper.Bm25Boost, float knnBoost = SolutionHelper.KnnBoost,
        float titleBoost = SolutionHelper.TitleBoost, float contentBoost = SolutionHelper.ContentBoost)
    {
        var queryVector = await embeddingService.GetEmbeddingAsync(query);
        var bm25Fields = new[] { $"title^{titleBoost}", $"content^{contentBoost}" };
        
        var searchResponse = await client.SearchAsync<DocumentData>(s => s
            .Indices(IndexName)
            .Size(pageSize)
            .From((page - 1) * pageSize)
            .Query(q => q
                .MultiMatch(mm => mm
                    .Fields(bm25Fields)
                    .Query(query)
                    .Analyzer("ik_max_word")
                    .Operator(Operator.Or)
                    .Boost(bm25Boost)
                )
            )
            .Knn(k => k
                .Field(f => f.TitleVector)
                .Field(f => f.ContentVector)
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
            .Select(hit => new ElasticsearchResult
            {
                Id = hit.Source!.Id,
                Title = hit.Source.Title,
                Content = hit.Source.Content,
                Score = (float)(hit.Score ?? 0)
            })
            .ToList();
    }
}