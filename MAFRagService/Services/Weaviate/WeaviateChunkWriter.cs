using System.Net;
using MAFRagService.Startup.Extensions;

namespace MAFRagService.Services.Weaviate;

public sealed class WeaviateChunkWriter : IWeaviateChunkWriter
{
    private const string ClassName = "Document";

    private readonly IHttpClientFactory _factory;
    private readonly ILogger<WeaviateChunkWriter> _logger;

    public WeaviateChunkWriter(
        IHttpClientFactory factory,
        ILogger<WeaviateChunkWriter> logger)
    {
        _factory = factory;
        _logger  = logger;
    }

    public async Task DeleteByDocAsync(
        string docId, string version, string tenant, CancellationToken ct)
    {
        var http = _factory.CreateClient(HttpClientNames.Weaviate);

        // Weaviate 1.26 批量删除：POST /v1/batch/objects/delete
        var body = new
        {
            match = new
            {
                @class = ClassName,
                where  = new
                {
                    @operator = "And",
                    operands = new object[]
                    {
                        new { path = new[] { "docId"   }, @operator = "Equal", valueString = docId },
                        new { path = new[] { "version" }, @operator = "Equal", valueString = version },
                        new { path = new[] { "tenant"  }, @operator = "Equal", valueString = tenant }
                    }
                }
            },
            output = "minimal"
        };

        using var resp = await http.PostAsJsonAsync("/v1/batch/objects/delete", body, ct);

        if (!resp.IsSuccessStatusCode && resp.StatusCode != HttpStatusCode.NotFound)
        {
            var txt = await resp.Content.ReadAsStringAsync(ct);
            _logger.LogWarning(
                "Weaviate delete 返回 {Status}: {Body}", (int)resp.StatusCode, txt);
        }
        else
        {
            _logger.LogInformation(
                "Weaviate 清理旧 chunk: docId={DocId}, version={Version}, tenant={Tenant}",
                docId, version, tenant);
        }
    }

    public async Task UpsertBatchAsync(IReadOnlyList<ChunkRecord> records, CancellationToken ct)
    {
        if (records.Count == 0) return;

        var http = _factory.CreateClient(HttpClientNames.Weaviate);

        var objects = records.Select(r => new
        {
            @class = ClassName,
            properties = new
            {
                docId      = r.DocId,
                tenant     = r.Tenant,
                version    = r.Version,
                chunkIndex = r.ChunkIndex,
                content    = r.Content,
                filePath   = r.FilePath
            },
            vector = r.Vector
        }).ToArray();

        using var resp = await http.PostAsJsonAsync("/v1/batch/objects", new { objects }, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Weaviate batch upsert 失败: {(int)resp.StatusCode}. Body: {body}");
        }

        _logger.LogInformation("Weaviate 写入 {Count} 个 chunk", records.Count);
    }
}