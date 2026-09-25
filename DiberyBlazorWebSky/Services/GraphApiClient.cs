using System.Net;
using System.Text.Json;
using DiberyBlazorWebSky.Models.Graph;

namespace DiberyBlazorWebSky.Services;

/// <summary>
/// 通过 Aspire 服务发现调用 MAFWorkFlowApi 的图数据接口。
/// 与后端 GraphController 的对应关系：
///   GET    /api/graph                                 → ListGraphsAsync
///   POST   /api/graph                                 → CreateGraphAsync
///   GET    /api/graph/{g}                             → GetGraphAsync
///   GET    /api/graph/{g}/nodes                       → ListNodesAsync
///   POST   /api/graph/{g}/nodes                       → UpsertNodeAsync
///   GET    /api/graph/{g}/nodes/{n}                   → GetNodeAsync
///   DELETE /api/graph/{g}/nodes/{n}                   → DeleteNodeAsync
///   GET    /api/graph/{g}/edges                       → ListEdgesAsync
///   POST   /api/graph/{g}/edges                       → UpsertEdgeAsync
///   GET    /api/graph/{g}/edges/{e}                   → GetEdgeAsync
///   DELETE /api/graph/{g}/edges/{e}                   → DeleteEdgeAsync
/// </summary>
public sealed class GraphApiClient(HttpClient http, ILogger<GraphApiClient> logger)
{
    // ★ Web 默认：camelCase 序列化 + 大小写不敏感反序列化
    //   —— 与服务端 ASP.NET Core 的默认行为一致
    private static readonly JsonSerializerOptions JsonOpts =
        new(JsonSerializerDefaults.Web);

    private const string Base = "/api/graph";

    // ═══════════════════════════════════════════════════════
    // 图 (Graph)
    // ═══════════════════════════════════════════════════════

    public async Task<IReadOnlyList<GraphDto>> ListGraphsAsync(
        CancellationToken ct = default)
    {
        var response = await http.GetAsync(Base, ct);
        await EnsureSuccessAsync(response, ct);

        var wrapper = await response.Content
            .ReadFromJsonAsync<LiteGraphListResponse<GraphDto>>(JsonOpts, ct);

        return wrapper?.Objects ?? [];
    }

    public async Task<GraphDto?> GetGraphAsync(
        Guid graphGuid, CancellationToken ct = default)
    {
        var response = await http.GetAsync($"{Base}/{graphGuid}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, ct);

        return await response.Content
            .ReadFromJsonAsync<GraphDto>(JsonOpts, ct);
    }

    public async Task<GraphDto?> CreateGraphAsync(
        string name, CancellationToken ct = default)
    {
        logger.LogInformation("创建图：{Name}", name);

        var response = await http.PostAsJsonAsync(
            Base,
            new CreateGraphRequest { Name = name },
            JsonOpts, ct);

        await EnsureSuccessAsync(response, ct);

        return await response.Content
            .ReadFromJsonAsync<GraphDto>(JsonOpts, ct);
    }

    // ═══════════════════════════════════════════════════════
    // 节点 (Node)
    // ═══════════════════════════════════════════════════════

    public async Task<IReadOnlyList<NodeDto>> ListNodesAsync(
        Guid graphGuid, CancellationToken ct = default)
    {
        var response = await http.GetAsync(
            $"{Base}/{graphGuid}/nodes", ct);
        await EnsureSuccessAsync(response, ct);

        var wrapper = await response.Content
            .ReadFromJsonAsync<LiteGraphListResponse<NodeDto>>(JsonOpts, ct);

        return wrapper?.Objects ?? [];
    }

    public async Task<NodeDto?> GetNodeAsync(
        Guid graphGuid, Guid nodeGuid, CancellationToken ct = default)
    {
        var response = await http.GetAsync(
            $"{Base}/{graphGuid}/nodes/{nodeGuid}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, ct);

        return await response.Content.ReadFromJsonAsync<NodeDto>(JsonOpts, ct);
    }

    public async Task<NodeUpsertResult> UpsertNodeAsync(
        Guid graphGuid,
        NodeUpsertRequest request,
        CancellationToken ct = default)
    {
        logger.LogDebug(
            "UpsertNode：graph={GraphGuid}, name={Name}, type={Type}",
            graphGuid, request.DisplayName, request.Type);

        var response = await http.PostAsJsonAsync(
            $"{Base}/{graphGuid}/nodes", request, JsonOpts, ct);

        // 服务端 400 时也返回 NodeUpsertResult（业务错误），需读取
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);

            var failure = TryDeserialize<NodeUpsertResult>(body, JsonOpts);
            if (failure is not null) return failure;

            throw new HttpRequestException(
                $"API 返回 {(int)response.StatusCode}：{body}",
                null, response.StatusCode);
        }

        return await response.Content
            .ReadFromJsonAsync<NodeUpsertResult>(JsonOpts, ct)
            ?? new NodeUpsertResult
            {
                Success = false,
                ErrorCode = "empty_response",
                ErrorMessage = "服务端返回空响应"
            };
    }

    public async Task<bool> DeleteNodeAsync(
        Guid graphGuid, Guid nodeGuid, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync(
            $"{Base}/{graphGuid}/nodes/{nodeGuid}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        await EnsureSuccessAsync(response, ct);
        return true;
    }

    // ═══════════════════════════════════════════════════════
    // 边 (Edge)
    // ═══════════════════════════════════════════════════════

    public async Task<IReadOnlyList<EdgeDto>> ListEdgesAsync(
        Guid graphGuid, CancellationToken ct = default)
    {
        var response = await http.GetAsync(
            $"{Base}/{graphGuid}/edges", ct);
        await EnsureSuccessAsync(response, ct);

        var wrapper = await response.Content
            .ReadFromJsonAsync<LiteGraphListResponse<EdgeDto>>(JsonOpts, ct);

        return wrapper?.Objects ?? [];
    }

    public async Task<EdgeDto?> GetEdgeAsync(
        Guid graphGuid, Guid edgeGuid, CancellationToken ct = default)
    {
        var response = await http.GetAsync(
            $"{Base}/{graphGuid}/edges/{edgeGuid}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, ct);

        return await response.Content.ReadFromJsonAsync<EdgeDto>(JsonOpts, ct);
    }

    public async Task<EdgeUpsertResult> UpsertEdgeAsync(
        Guid graphGuid,
        EdgeUpsertRequest request,
        CancellationToken ct = default)
    {
        logger.LogDebug(
            "UpsertEdge：graph={GraphGuid}, from={From}, to={To}, type={Type}",
            graphGuid, request.From, request.To, request.Type);

        var response = await http.PostAsJsonAsync(
            $"{Base}/{graphGuid}/edges", request, JsonOpts, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);

            var failure = TryDeserialize<EdgeUpsertResult>(body, JsonOpts);
            if (failure is not null) return failure;

            throw new HttpRequestException(
                $"API 返回 {(int)response.StatusCode}：{body}",
                null, response.StatusCode);
        }

        return await response.Content
            .ReadFromJsonAsync<EdgeUpsertResult>(JsonOpts, ct)
            ?? new EdgeUpsertResult
            {
                Success = false,
                ErrorCode = "empty_response",
                ErrorMessage = "服务端返回空响应"
            };
    }

    public async Task<bool> DeleteEdgeAsync(
        Guid graphGuid, Guid edgeGuid, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync(
            $"{Base}/{graphGuid}/edges/{edgeGuid}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        await EnsureSuccessAsync(response, ct);
        return true;
    }

    // ═══════════════════════════════════════════════════════
    // 私有辅助
    // ═══════════════════════════════════════════════════════

    private static T? TryDeserialize<T>(string json, JsonSerializerOptions opts)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json, opts); }
        catch (JsonException) { return default; }
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync(ct);

        var message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "API 返回 401（未授权）",
            HttpStatusCode.Forbidden    => "API 返回 403（禁止访问）",
            HttpStatusCode.NotFound     => "API 返回 404（端点或资源不存在）",
            HttpStatusCode.Conflict     => "API 返回 409（冲突）",
            _ => $"API 返回 {(int)response.StatusCode} {response.ReasonPhrase}"
        };

        if (!string.IsNullOrWhiteSpace(body))
            message += $"：{body}";

        throw new HttpRequestException(message, null, response.StatusCode);
    }
    
    // ── 追加到 GraphApiClient ──
    public async Task<EnumerateResponse<GraphNodeDto>?> EnumerateNodesAsync(
        Guid graphGuid, int maxResults = 1000, Guid? continuationToken = null,
        CancellationToken ct = default)
        => await PostEnumerateAsync<GraphNodeDto>(
            $"api/graph/{graphGuid}/nodes/enumerate", maxResults, continuationToken, ct);

    public async Task<EnumerateResponse<GraphEdgeDto>?> EnumerateEdgesAsync(
        Guid graphGuid, int maxResults = 1000, Guid? continuationToken = null,
        CancellationToken ct = default)
        => await PostEnumerateAsync<GraphEdgeDto>(
            $"api/graph/{graphGuid}/edges/enumerate", maxResults, continuationToken, ct);

    public async Task<EnumerateResponse<GraphVectorDto>?> EnumerateVectorsAsync(
        Guid graphGuid, int maxResults = 1000, Guid? continuationToken = null,
        CancellationToken ct = default)
        => await PostEnumerateAsync<GraphVectorDto>(
            $"api/graph/{graphGuid}/vectors/enumerate", maxResults, continuationToken, ct);

    private async Task<EnumerateResponse<T>?> PostEnumerateAsync<T>(
        string url, int maxResults, Guid? continuationToken, CancellationToken ct)
    {
        var body = new Dictionary<string, object?>
        {
            ["MaxResults"] = Math.Clamp(maxResults, 1, 1000)
        };
        if (continuationToken.HasValue)
            body["ContinuationToken"] = continuationToken.Value;

        using var resp = await http.PostAsJsonAsync(url, body, ct);
        if (!resp.IsSuccessStatusCode) return null;

        return await resp.Content.ReadFromJsonAsync<EnumerateResponse<T>>(
            cancellationToken: ct);
    }
}