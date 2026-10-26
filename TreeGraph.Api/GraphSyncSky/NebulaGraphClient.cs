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
    [property: JsonPropertyName("rank")] long Rank,
    [property: JsonPropertyName("properties")] Dictionary<string, object?>? Properties
);

/// <summary>
/// TreeGraph.NebulaFastApi REST 客户端（幂等写入 NebulaGraph）。
/// 多图空间对齐：所有方法的 space 参数 = Nebula 空间名
/// （PG 空间 Id 经 <see cref="GraphNaming.ToNebulaSpace"/> 映射后传入）。
/// </summary>
public sealed class NebulaGraphClient
{
    private readonly HttpClient _http;
    private readonly ILogger<NebulaGraphClient> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public NebulaGraphClient(HttpClient http, ILogger<NebulaGraphClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <summary>执行 nGQL（space 之外的语句，如 CREATE/DROP SPACE，传 null）。失败抛 InvalidOperationException（交由 Wolverine 重试）。</summary>
    public async Task<NebulaQueryResult> QueryAsync(string ngql, CancellationToken ct)
        => await QueryAsync(null, ngql, ct);

    /// <summary>在指定图空间执行 nGQL；失败抛 InvalidOperationException（交由 Wolverine 重试）。</summary>
    public async Task<NebulaQueryResult> QueryAsync(string? space, string ngql, CancellationToken ct)
    {
        using var resp = await _http.PostAsJsonAsync(
            "api/v1/graph/query",
            new { space, query = ngql }, JsonOpts, ct);
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadFromJsonAsync<NebulaQueryResult>(JsonOpts, ct);
        if (result is null || !result.Success)
            throw new InvalidOperationException($"nGQL 执行失败: {result?.ErrorMsg} | {ngql}");
        return result;
    }

    /// <summary>UPSERT 顶点（INSERT 覆盖语义，幂等）。</summary>
    public async Task UpsertVertexAsync(
        string space, string vid, string tag, IReadOnlyDictionary<string, object?> props, CancellationToken ct)
    {
        var payload = new VertexItem(vid, [tag], CleanProps(props));
        using var resp = await _http.PostAsJsonAsync(
            $"api/v1/graph/vertices?space={space}", payload, JsonOpts, ct);
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadFromJsonAsync<NebulaQueryResult>(JsonOpts, ct);
        if (result is not { Success: true })
            throw new InvalidOperationException($"顶点写入失败: {result?.ErrorMsg} | space={space} vid={vid}");
    }

    /// <summary>删除顶点（连带所有关联边）。</summary>
    public async Task DeleteVertexAsync(string space, string vid, CancellationToken ct)
    {
        using var resp = await _http.DeleteAsync(
            $"api/v1/graph/vertices/{Uri.EscapeDataString(vid)}?space={space}&with_edge=true", ct);
        resp.EnsureSuccessStatusCode();
    }

    /// <summary>UPSERT 边（INSERT 覆盖语义，幂等；rank 默认 0，区分同起终点同类型多边）。</summary>
    public async Task UpsertEdgeAsync(
        string space, string edgeType, string srcVid, string dstVid, long rank,
        IReadOnlyDictionary<string, object?>? props, CancellationToken ct)
    {
        var payload = new EdgeItem(srcVid, dstVid, edgeType, rank, CleanProps(props));
        using var resp = await _http.PostAsJsonAsync(
            $"api/v1/graph/edges?space={space}", payload, JsonOpts, ct);
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadFromJsonAsync<NebulaQueryResult>(JsonOpts, ct);
        if (result is not { Success: true })
            throw new InvalidOperationException(
                $"边写入失败: {result?.ErrorMsg} | space={space} {edgeType} {srcVid}->{dstVid}");
    }

    /// <summary>按四元组删除指定边。</summary>
    public async Task DeleteEdgeAsync(string space, string edgeType, string srcVid, string dstVid, long rank, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Delete, "api/v1/graph/edges")
        {
            Content = JsonContent.Create(
                new { edge_type = edgeType, src_vid = srcVid, dst_vid = dstVid, rank, space },
                options: JsonOpts)
        };
        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
    }

    /// <summary>批量写入（全量重建用，按空间分批调用）。</summary>
    public async Task<BulkResult> BulkAsync(
        string space,
        IReadOnlyList<VertexItem> vertices,
        IReadOnlyList<EdgeItem> edges,
        int batchSize,
        CancellationToken ct)
    {
        using var resp = await _http.PostAsJsonAsync(
            "api/v1/graph/bulk",
            new { space, vertices, edges, batch_size = batchSize }, JsonOpts, ct);
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
