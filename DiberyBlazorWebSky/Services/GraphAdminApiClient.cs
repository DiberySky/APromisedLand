using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DiberyBlazorWebSky.Models.Graph;

namespace DiberyBlazorWebSky.Services;

// ══════════════════════════════════════════════════════════
// DTO
// ══════════════════════════════════════════════════════════

public sealed class CreateGraphPayload
{
    public string Name { get; set; } = "";
    public Dictionary<string, object?>? Data { get; set; }
}

public sealed class UpdateGraphPayload
{
    public string Name { get; set; } = "";
    public Dictionary<string, object?>? Data { get; set; }
}

public sealed class EnumerateRequestPayload
{
    public int MaxResults { get; set; } = 20;
    public Guid? ContinuationToken { get; set; }
}

public sealed class CreateNodePayload
{
    public string DisplayName { get; set; } = "";
    public string Type { get; set; } = "Default";
    public Dictionary<string, object?> Attributes { get; set; } = new();
}

public sealed class UpdateNodePayload
{
    public string Name { get; set; } = "";
}

public sealed class CreateEdgePayload
{
    public Guid From { get; set; }
    public Guid To { get; set; }
    public string Type { get; set; } = "RELATED_TO";
    public Dictionary<string, object?> Attributes { get; set; } = new();
}

public sealed class UpdateEdgePayload
{
    public string Name { get; set; } = "";
}

public sealed class CreateVectorPayload
{
    public Guid NodeGuid { get; set; }

    /// <summary>
    /// ★ 保留字段以兼容旧调用方。服务端会忽略此字段，
    ///   使用服务端配置的 Embedding:Model。
    /// </summary>
    public string? Model { get; set; }

    public List<float> Vector { get; set; } = new();
}

public sealed class ImportJsonPayload
{
    public string JsonContent { get; set; } = "";
    public bool ClearExisting { get; set; }
}

public sealed class ImportCsvPayload
{
    public string NodesCsv { get; set; } = "";
    public string? EdgesCsv { get; set; }
    public bool ClearExisting { get; set; }
}

public sealed class ImportAsNewGraphPayload
{
    public string JsonContent { get; set; } = "";
    public string NewGraphName { get; set; } = "";
}

public sealed class ImportResultDto
{
    [JsonPropertyName("nodesCreated")] public int NodesCreated { get; set; }
    [JsonPropertyName("nodesSkipped")] public int NodesSkipped { get; set; }
    [JsonPropertyName("edgesCreated")] public int EdgesCreated { get; set; }
    [JsonPropertyName("edgesSkipped")] public int EdgesSkipped { get; set; }
    [JsonPropertyName("errors")] public List<string> Errors { get; set; } = new();
}

public sealed class ImportAsNewGraphResponseDto
{
    [JsonPropertyName("newGraphGuid")] public Guid? NewGraphGuid { get; set; }
    [JsonPropertyName("result")] public ImportResultDto Result { get; set; } = new();
}

public sealed class EnumerateResult<T>
{
    [JsonPropertyName("objects")] public List<T> Objects { get; set; } = new();

    [JsonPropertyName("continuationToken")]
    public Guid? ContinuationToken { get; set; }

    [JsonPropertyName("totalRecords")] public long TotalRecords { get; set; }
}

public sealed class VectorMetadataDto
{
    [JsonPropertyName("GUID")] public Guid Guid { get; set; }
    [JsonPropertyName("NodeGUID")] public Guid? NodeGuid { get; set; }
    [JsonPropertyName("Model")] public string? Model { get; set; }
    [JsonPropertyName("Dimensionality")] public int Dimensionality { get; set; }
    [JsonPropertyName("Vectors")] public List<float>? Vectors { get; set; }
}

// ══════════════════════════════════════════════════════════
// 客户端
// ══════════════════════════════════════════════════════════

public class GraphAdminApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<GraphAdminApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public GraphAdminApiClient(HttpClient http, ILogger<GraphAdminApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    // ══════════════════════════════════════════════════════
    // 图 CRUD
    // ══════════════════════════════════════════════════════

    public async Task<List<GraphDto>> ListGraphsAsync(CancellationToken ct = default)
    {
        try
        {
            var raw = await _http.GetFromJsonAsync<JsonElement>("/api/graph", JsonOpts, ct);
            if (raw.TryGetProperty("Objects", out var objs) && objs.ValueKind == JsonValueKind.Array)
            {
                return JsonSerializer.Deserialize<List<GraphDto>>(objs.GetRawText(), JsonOpts) ?? new();
            }

            return new List<GraphDto>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "加载图列表失败");
            return new List<GraphDto>();
        }
    }

    public async Task<bool> CreateGraphAsync(string name, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/graph",
                new CreateGraphPayload { Name = name }, JsonOpts, ct);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "创建图失败");
            return false;
        }
    }

    public async Task<bool> UpdateGraphAsync(Guid graphGuid, string newName, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PutAsJsonAsync($"/api/graph/{graphGuid}",
                new UpdateGraphPayload { Name = newName }, JsonOpts, ct);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "更新图失败");
            return false;
        }
    }

    public async Task<bool> DeleteGraphAsync(Guid graphGuid, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.DeleteAsync($"/api/graph/{graphGuid}", ct);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "删除图失败");
            return false;
        }
    }

    // ══════════════════════════════════════════════════════
    // 节点
    // ══════════════════════════════════════════════════════

    public async Task<EnumerateResult<NodeDto>> EnumerateNodesAsync(
        Guid graphGuid, int maxResults, Guid? continuationToken, CancellationToken ct = default)
    {
        try
        {
            var raw = await _http.GetFromJsonAsync<JsonElement>(
                $"/api/graph/{graphGuid}/nodes", JsonOpts, ct);

            var allNodes = new List<NodeDto>();
            if (raw.TryGetProperty("Objects", out var objs) && objs.ValueKind == JsonValueKind.Array)
            {
                allNodes = JsonSerializer.Deserialize<List<NodeDto>>(
                    objs.GetRawText(), JsonOpts) ?? new();
            }

            int startIndex = 0;
            if (continuationToken.HasValue)
            {
                var bytes = continuationToken.Value.ToByteArray();
                startIndex = Math.Max(0, BitConverter.ToInt32(bytes, 0));
            }

            var page = allNodes.Skip(startIndex).Take(maxResults).ToList();

            Guid? nextToken = null;
            if (startIndex + maxResults < allNodes.Count)
            {
                var bytes = new byte[16];
                BitConverter.GetBytes(startIndex + maxResults).CopyTo(bytes, 0);
                nextToken = new Guid(bytes);
            }

            return new EnumerateResult<NodeDto>
            {
                Objects = page,
                TotalRecords = allNodes.Count,
                ContinuationToken = nextToken
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "枚举节点失败");
            return new EnumerateResult<NodeDto>();
        }
    }

    public async Task<List<NodeDto>> ListAllNodesAsync(Guid graphGuid, CancellationToken ct = default)
    {
        var result = await EnumerateNodesAsync(graphGuid, 1000, null, ct);
        return result.Objects;
    }

    public async Task<bool> CreateNodeAsync(Guid graphGuid, string name, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync(
                $"/api/graph/{graphGuid}/nodes",
                new CreateNodePayload { DisplayName = name, Type = "Default" },
                JsonOpts, ct);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "创建节点失败");
            return false;
        }
    }

    public async Task<bool> UpdateNodeAsync(Guid graphGuid, Guid nodeGuid, string newName,
        CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PutAsJsonAsync(
                $"/api/graph/{graphGuid}/nodes/{nodeGuid}",
                new UpdateNodePayload { Name = newName },
                JsonOpts, ct);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "更新节点失败");
            return false;
        }
    }

    public async Task<bool> DeleteNodeAsync(Guid graphGuid, Guid nodeGuid, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.DeleteAsync($"/api/graph/{graphGuid}/nodes/{nodeGuid}", ct);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "删除节点失败");
            return false;
        }
    }

    // ══════════════════════════════════════════════════════
    // 边
    // ══════════════════════════════════════════════════════

    public async Task<EnumerateResult<EdgeDto>> EnumerateEdgesAsync(
        Guid graphGuid, int maxResults, Guid? continuationToken, CancellationToken ct = default)
    {
        try
        {
            var raw = await _http.GetFromJsonAsync<JsonElement>(
                $"/api/graph/{graphGuid}/edges", JsonOpts, ct);

            var allEdges = new List<EdgeDto>();
            if (raw.TryGetProperty("Objects", out var objs) && objs.ValueKind == JsonValueKind.Array)
            {
                allEdges = JsonSerializer.Deserialize<List<EdgeDto>>(
                    objs.GetRawText(), JsonOpts) ?? new();
            }

            int startIndex = 0;
            if (continuationToken.HasValue)
            {
                var bytes = continuationToken.Value.ToByteArray();
                startIndex = Math.Max(0, BitConverter.ToInt32(bytes, 0));
            }

            var page = allEdges.Skip(startIndex).Take(maxResults).ToList();

            Guid? nextToken = null;
            if (startIndex + maxResults < allEdges.Count)
            {
                var bytes = new byte[16];
                BitConverter.GetBytes(startIndex + maxResults).CopyTo(bytes, 0);
                nextToken = new Guid(bytes);
            }

            return new EnumerateResult<EdgeDto>
            {
                Objects = page,
                TotalRecords = allEdges.Count,
                ContinuationToken = nextToken
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "枚举边失败");
            return new EnumerateResult<EdgeDto>();
        }
    }

    public async Task<List<EdgeDto>> ListAllEdgesAsync(Guid graphGuid, CancellationToken ct = default)
    {
        var result = await EnumerateEdgesAsync(graphGuid, 1000, null, ct);
        return result.Objects;
    }

    public async Task<bool> CreateEdgeAsync(Guid graphGuid, Guid from, Guid to, string name,
        CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync(
                $"/api/graph/{graphGuid}/edges",
                new CreateEdgePayload { From = from, To = to, Type = name },
                JsonOpts, ct);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "创建边失败");
            return false;
        }
    }

    public async Task<bool> UpdateEdgeAsync(Guid graphGuid, Guid edgeGuid, string newName,
        CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PutAsJsonAsync(
                $"/api/graph/{graphGuid}/edges/{edgeGuid}",
                new UpdateEdgePayload { Name = newName },
                JsonOpts, ct);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "更新边失败");
            return false;
        }
    }

    public async Task<bool> DeleteEdgeAsync(Guid graphGuid, Guid edgeGuid, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.DeleteAsync($"/api/graph/{graphGuid}/edges/{edgeGuid}", ct);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "删除边失败");
            return false;
        }
    }

    // ══════════════════════════════════════════════════════
    // 向量
    // ══════════════════════════════════════════════════════

    public async Task<List<VectorMetadataDto>> ListAllVectorsAsync(Guid graphGuid, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync(
                $"/api/graph/{graphGuid}/vectors/enumerate",
                new EnumerateRequestPayload { MaxResults = 1000 },
                JsonOpts, ct);

            if (!resp.IsSuccessStatusCode) return new List<VectorMetadataDto>();

            var result = await resp.Content.ReadFromJsonAsync<EnumerateResult<VectorMetadataDto>>(JsonOpts, ct);
            return result?.Objects ?? new List<VectorMetadataDto>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "加载向量失败");
            return new List<VectorMetadataDto>();
        }
    }

    /// <summary>
    /// ★ 创建向量。model 参数已废弃，服务端会忽略它并使用服务端配置的模型名。
    ///   保留参数只是为了不破坏现有调用方。
    /// </summary>
    public async Task<bool> CreateVectorAsync(
        Guid graphGuid, Guid nodeGuid, string? model, List<float> vector,
        CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync(
                $"/api/graph/{graphGuid}/vectors",
                new CreateVectorPayload { NodeGuid = nodeGuid, Model = model, Vector = vector },
                JsonOpts, ct);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "创建向量失败");
            return false;
        }
    }

    public async Task<bool> DeleteVectorAsync(Guid graphGuid, Guid vectorGuid, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.DeleteAsync($"/api/graph/{graphGuid}/vectors/{vectorGuid}", ct);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "删除向量失败");
            return false;
        }
    }

    // ══════════════════════════════════════════════════════
    // 导出
    // ══════════════════════════════════════════════════════

    public async Task<string?> ExportGraphJsonAsync(Guid graphGuid, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetAsync($"/api/graph-admin/graphs/{graphGuid}/export/json", ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "导出图 JSON 失败");
            return null;
        }
    }

    public async Task<string?> ExportNodesCsvAsync(Guid graphGuid, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetAsync($"/api/graph-admin/graphs/{graphGuid}/export/nodes.csv", ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "导出节点 CSV 失败");
            return null;
        }
    }

    public async Task<string?> ExportEdgesCsvAsync(Guid graphGuid, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetAsync($"/api/graph-admin/graphs/{graphGuid}/export/edges.csv", ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "导出边 CSV 失败");
            return null;
        }
    }

    // ══════════════════════════════════════════════════════
    // 导入
    // ══════════════════════════════════════════════════════

    public async Task<ImportResultDto> ImportJsonAsync(
        Guid graphGuid, string jsonContent, bool clearExisting, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync(
                $"/api/graph-admin/graphs/{graphGuid}/import/json",
                new ImportJsonPayload { JsonContent = jsonContent, ClearExisting = clearExisting },
                JsonOpts, ct);

            if (!resp.IsSuccessStatusCode)
                return new ImportResultDto { Errors = { "服务返回异常" } };

            var result = await resp.Content.ReadFromJsonAsync<ImportResultDto>(JsonOpts, ct);
            return result ?? new ImportResultDto();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "导入 JSON 失败");
            return new ImportResultDto { Errors = { ex.Message } };
        }
    }

    public async Task<ImportResultDto> ImportCsvAsync(
        Guid graphGuid, string nodesCsv, string? edgesCsv, bool clearExisting, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync(
                $"/api/graph-admin/graphs/{graphGuid}/import/csv",
                new ImportCsvPayload { NodesCsv = nodesCsv, EdgesCsv = edgesCsv, ClearExisting = clearExisting },
                JsonOpts, ct);

            if (!resp.IsSuccessStatusCode)
                return new ImportResultDto { Errors = { "服务返回异常" } };

            var result = await resp.Content.ReadFromJsonAsync<ImportResultDto>(JsonOpts, ct);
            return result ?? new ImportResultDto();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "导入 CSV 失败");
            return new ImportResultDto { Errors = { ex.Message } };
        }
    }

    public async Task<ImportAsNewGraphResponseDto> ImportAsNewGraphAsync(
        string jsonContent, string newGraphName, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync(
                "/api/graph-admin/import-as-new-graph",
                new ImportAsNewGraphPayload { JsonContent = jsonContent, NewGraphName = newGraphName },
                JsonOpts, ct);

            if (!resp.IsSuccessStatusCode)
                return new ImportAsNewGraphResponseDto();

            var result = await resp.Content.ReadFromJsonAsync<ImportAsNewGraphResponseDto>(JsonOpts, ct);
            return result ?? new ImportAsNewGraphResponseDto();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "导入为新图失败");
            return new ImportAsNewGraphResponseDto();
        }
    }

    // ══════════════════════════════════════════════════════
    // Embedding
    // ══════════════════════════════════════════════════════

    /// <summary>把文本转为向量。失败返回 null。</summary>
    public async Task<List<float>?> GetEmbeddingAsync(string text, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync(
                "/api/embedding/embed",
                new { Text = text },
                JsonOpts, ct);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Embedding 返回 {Status}: {Body}", resp.StatusCode, body);
                return null;
            }

            var result = await resp.Content.ReadFromJsonAsync<EmbedResponseDto>(JsonOpts, ct);
            return result?.Vector;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "调用后端 embedding 失败");
            return null;
        }
    }

    // ══════════════════════════════════════════════════════
    // 边向量
    // ══════════════════════════════════════════════════════

    public async Task<bool> CreateEdgeVectorAsync(
        Guid graphGuid, Guid edgeGuid, string content, List<float> vector,
        CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync(
                $"/api/graph/{graphGuid}/edge-vectors",
                new CreateEdgeVectorPayload
                {
                    EdgeGuid = edgeGuid, Content = content, Vector = vector
                },
                JsonOpts, ct);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "创建边向量失败");
            return false;
        }
    }

    // ══════════════════════════════════════════════════════
    // 语义搜索（一站式）
    // ══════════════════════════════════════════════════════

    public async Task<SemanticSearchResponseDto?> SemanticSearchAsync(
        Guid graphGuid,
        string query,
        int topK = 10,
        string? directionOverride = null,   // ★ 新增
        bool useReranker = true,  
        CancellationToken ct = default)
    {
        try
        {
            var payload = new
            {
                Query = query,
                TopK = topK,
                DirectionOverride = directionOverride,
                UseReranker = useReranker                     // ★ 传递
            };

            var resp = await _http.PostAsJsonAsync(
                $"/api/graph/{graphGuid}/semantic-search",
                payload, JsonOpts, ct);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("语义搜索失败 {Status}: {Body}", resp.StatusCode, body);
                return null;
            }
            return await resp.Content.ReadFromJsonAsync<SemanticSearchResponseDto>(JsonOpts, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "语义搜索异常");
            return null;
        }
    }
}

// ══════════════════════════════════════════════════════
// ★ 边向量 + 语义搜索 DTO
// ══════════════════════════════════════════════════════

public sealed class CreateEdgeVectorPayload
{
    public Guid EdgeGuid { get; set; }
    public string Content { get; set; } = "";
    public List<float> Vector { get; set; } = [];
}

public sealed class IntentResultDto
{
    [JsonPropertyName("relation")] public string? Relation { get; set; }
    [JsonPropertyName("direction")] public string? Direction { get; set; }
    [JsonPropertyName("subjectName")] public string? SubjectName { get; set; }
    [JsonPropertyName("confidence")] public double Confidence { get; set; }
    [JsonPropertyName("reason")] public string? Reason { get; set; }
    [JsonPropertyName("strategy")] public string Strategy { get; set; } = "none";
    
    // ★ 阶段 4 新增
    [JsonPropertyName("hopCount")]        public int HopCount { get; set; } = 1;
    [JsonPropertyName("aggregationMode")] public string? AggregationMode { get; set; }
}

public sealed class SemanticSearchHitDto
{
    [JsonPropertyName("nodeGuid")]        public Guid NodeGuid { get; set; }
    [JsonPropertyName("nodeName")]        public string NodeName { get; set; } = "";
    [JsonPropertyName("score")]           public double Score { get; set; }
    [JsonPropertyName("vectorScore")]     public double? VectorScore { get; set; }   // ★
    [JsonPropertyName("bm25Score")]       public double? Bm25Score   { get; set; }   // ★
    [JsonPropertyName("viaEdgeName")]     public string? ViaEdgeName { get; set; }
    [JsonPropertyName("direction")]       public string? Direction { get; set; }
    [JsonPropertyName("matchedContent")]  public string? MatchedContent { get; set; }
    
    // ★ 阶段 4 新增
    public int? HopDistance { get; set; }   // 几跳（多跳结果才有值）
    public bool IsMultiHop  { get; set; }   // 是否多跳结果
}

public sealed class SuggestedRelationDto
{
    [JsonPropertyName("relation")]    public string Relation { get; set; } = "";
    [JsonPropertyName("direction")]   public string Direction { get; set; } = "";
    [JsonPropertyName("query")]       public string Query { get; set; } = "";
    [JsonPropertyName("count")]       public int Count { get; set; }
    [JsonPropertyName("sampleNodes")] public List<string> SampleNodes { get; set; } = new();
    
    // ★ 新增
    [JsonPropertyName("displayLabel")] public string DisplayLabel { get; set; } = "";
    [JsonPropertyName("tooltip")]      public string Tooltip { get; set; } = "";
}

public sealed class SemanticSearchResponseDto
{
    [JsonPropertyName("hits")]        public List<SemanticSearchHitDto> Hits { get; set; } = new();
    [JsonPropertyName("intent")]      public IntentResultDto Intent { get; set; } = new();
    [JsonPropertyName("hint")]        public string? Hint { get; set; }
    [JsonPropertyName("suggestions")] public List<SuggestedRelationDto> Suggestions { get; set; } = new();
}