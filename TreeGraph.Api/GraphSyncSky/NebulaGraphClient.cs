using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace TreeGraph.Api.GraphSyncSky;

/// <summary>TreeGraph.NebulaFastApi REST 调用结果（nGQL 原生返回）。</summary>
public sealed class NebulaQueryResult
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("error_code")] public int ErrorCode { get; set; }
    [JsonPropertyName("error_msg")] public string? ErrorMsg { get; set; }
    [JsonPropertyName("data")] public List<Dictionary<string, JsonElement>>? Data { get; set; }
    [JsonPropertyName("columns")] public List<string>? Columns { get; set; }
    [JsonPropertyName("row_count")] public int RowCount { get; set; }
}

/// <summary>TreeGraph.NebulaFastApi /bulk 调用结果。</summary>
public sealed class BulkResult
{
    [JsonPropertyName("vertices_total")] public int VerticesTotal { get; set; }
    [JsonPropertyName("vertices_success")] public int VerticesSuccess { get; set; }
    [JsonPropertyName("edges_total")] public int EdgesTotal { get; set; }
    [JsonPropertyName("edges_success")] public int EdgesSuccess { get; set; }
    [JsonPropertyName("vertices_errors")] public List<Dictionary<string, JsonElement>>? VerticesErrors { get; set; }
    [JsonPropertyName("edges_errors")] public List<Dictionary<string, JsonElement>>? EdgesErrors { get; set; }
}

public sealed record VertexItem(
    [property: JsonPropertyName("vid")] string Vid,
    [property: JsonPropertyName("tags")] string[] Tags,
    [property: JsonPropertyName("properties")] Dictionary<string, object?>? Properties
);

public sealed record EdgeItem(
    [property: JsonPropertyName("src_vid")] string SrcVid,
    [property: JsonPropertyName("dst_vid")] string DstVid,
    [property: JsonPropertyName("edge_type")] string EdgeType,
    [property: JsonPropertyName("rank")] int Rank,
    [property: JsonPropertyName("properties")] Dictionary<string, object?>? Properties
);

/// <summary>TreeGraph.NebulaFastApi REST 客户端（幂等写入 NebulaGraph）。</summary>
public sealed class NebulaGraphClient
{
    private readonly HttpClient _http;
    private readonly GraphSyncOptions _options;
    private readonly ILogger<NebulaGraphClient> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public NebulaGraphClient(HttpClient http, IOptions<GraphSyncOptions> options, ILogger<NebulaGraphClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public string Space => _options.Space;

    /// <summary>执行 nGQL；失败抛 InvalidOperationException（交由 MassTransit 重试）。</summary>
    public async Task<NebulaQueryResult> QueryAsync(string ngql, CancellationToken ct)
    {
        using var resp = await _http.PostAsJsonAsync(
            "api/v1/graph/query",
            new { space = _options.Space, query = ngql }, JsonOpts, ct);
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadFromJsonAsync<NebulaQueryResult>(JsonOpts, ct);
        if (result is null || !result.Success)
            throw new InvalidOperationException($"nGQL 执行失败: {result?.ErrorMsg} | {ngql}");
        return result;
    }

    /// <summary>UPSERT 顶点（INSERT 覆盖语义，幂等）。</summary>
    public async Task UpsertVertexAsync(
        string vid, string tag, IReadOnlyDictionary<string, object?> props, CancellationToken ct)
    {
        var payload = new VertexItem(vid, [tag], CleanProps(props));
        using var resp = await _http.PostAsJsonAsync(
            $"api/v1/graph/vertices?space={_options.Space}", payload, JsonOpts, ct);
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadFromJsonAsync<NebulaQueryResult>(JsonOpts, ct);
        if (result is not { Success: true })
            throw new InvalidOperationException($"顶点写入失败: {result?.ErrorMsg} | vid={vid}");
    }

    /// <summary>删除顶点（连带所有关联边）。</summary>
    public async Task DeleteVertexAsync(string vid, CancellationToken ct)
    {
        using var resp = await _http.DeleteAsync(
            $"api/v1/graph/vertices/{Uri.EscapeDataString(vid)}?space={_options.Space}&with_edge=true", ct);
        resp.EnsureSuccessStatusCode();
    }

    /// <summary>UPSERT 语义边（INSERT 覆盖语义，幂等）。</summary>
    public async Task UpsertEdgeAsync(
        string edgeType, string srcVid, string dstVid,
        IReadOnlyDictionary<string, object?>? props, CancellationToken ct)
    {
        var payload = new EdgeItem(srcVid, dstVid, edgeType, 0, CleanProps(props));
        using var resp = await _http.PostAsJsonAsync(
            $"api/v1/graph/edges?space={_options.Space}", payload, JsonOpts, ct);
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadFromJsonAsync<NebulaQueryResult>(JsonOpts, ct);
        if (result is not { Success: true })
            throw new InvalidOperationException(
                $"边写入失败: {result?.ErrorMsg} | {edgeType} {srcVid}->{dstVid}");
    }

    /// <summary>删除指定语义边。</summary>
    public async Task DeleteEdgeAsync(string edgeType, string srcVid, string dstVid, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Delete, "api/v1/graph/edges")
        {
            Content = JsonContent.Create(
                new { edge_type = edgeType, src_vid = srcVid, dst_vid = dstVid, rank = 0, space = _options.Space },
                options: JsonOpts)
        };
        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
    }

    /// <summary>批量写入（全量重建用）。</summary>
    public async Task<BulkResult> BulkAsync(
        IReadOnlyList<VertexItem> vertices,
        IReadOnlyList<EdgeItem> edges,
        int batchSize,
        CancellationToken ct)
    {
        using var resp = await _http.PostAsJsonAsync(
            "api/v1/graph/bulk",
            new { space = _options.Space, vertices, edges, batch_size = batchSize }, JsonOpts, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<BulkResult>(JsonOpts, ct) ?? new BulkResult();
    }

    private static Dictionary<string, object?>? CleanProps(IReadOnlyDictionary<string, object?>? props)
    {
        if (props is null || props.Count == 0) return null;
        var cleaned = new Dictionary<string, object?>(props.Count);
        foreach (var kv in props)
            if (kv.Value is not null)
                cleaned[kv.Key] = kv.Value;
        return cleaned;
    }
}
