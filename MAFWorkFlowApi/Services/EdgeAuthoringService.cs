using System.Collections.Specialized;
using LiteGraph.Sdk;
using MAFWorkFlowApi.Infrastructure;
using MAFWorkFlowApi.Models;
using Microsoft.Extensions.Options;

namespace MAFWorkFlowApi.Services;

public sealed class EdgeAuthoringService(
    LiteGraphRestClient liteGraph,
    IOptions<LiteGraphOptions> options,
    ILogger<EdgeAuthoringService> logger)
{
    private readonly Guid _tenantGuid = liteGraph.TenantGuid;

    public async Task<EdgeUpsertResult> UpsertAsync(
        Guid graphGuid, EdgeUpsertRequest request,
        CancellationToken ct = default)
    {
        // ① 自环
        if (request.From == request.To)
            return EdgeUpsertResult.Fail("self_loop", "起点和终点不能相同");

        // ② 两端存在性
        var fromNode = await ReadNodeAsync(graphGuid, request.From, ct);
        if (fromNode is null)
            return EdgeUpsertResult.Fail("from_not_found", $"起点不存在：{request.From}");

        var toNode = await ReadNodeAsync(graphGuid, request.To, ct);
        if (toNode is null)
            return EdgeUpsertResult.Fail("to_not_found", $"终点不存在：{request.To}");

        // ③ 构造 Edge
        var edge = new Edge
        {
            TenantGUID = _tenantGuid,
            GraphGUID = graphGuid,
            GUID = request.Guid ?? Guid.NewGuid(),
            From = request.From,
            To = request.To,
            Name = request.Type,
            Labels = [request.Type],
            Cost = (int)(request.Cost ?? 0),
            Tags = new NameValueCollection
            {
                { "directed", request.Undirected ? "false" : "true" }
            },
            Data = request.Attributes
                .ToDictionary(kv => kv.Key, kv => kv.Value!)
        };

        var isNew = request.Guid is null;
        if (isNew)
            await liteGraph.PutAsync(EdgesPath(graphGuid), ToPayload(edge), ct);
        else
            await liteGraph.PutAsync(EdgePath(graphGuid, edge.GUID), ToPayload(edge), ct);

        logger.LogInformation(
            "Edge {Action}: {From} --[{Type}]--> {To}",
            isNew ? "created" : "updated",
            fromNode.Name, edge.Name, toNode.Name);

        return EdgeUpsertResult.Ok(edge, isNew);
    }

    public async Task DeleteAsync(
        Guid graphGuid, Guid edgeGuid,
        CancellationToken ct = default)
    {
        await liteGraph.DeleteAsync(EdgePath(graphGuid, edgeGuid), ct);
        logger.LogInformation("Edge deleted: {Guid}", edgeGuid);
    }

    public async Task<List<Edge>> CreateBatchAsync(
        Guid graphGuid, List<EdgeUpsertRequest> requests,
        CancellationToken ct = default)
    {
        var edges = new List<Edge>();
        foreach (var req in requests)
        {
            var edge = new Edge
            {
                TenantGUID = _tenantGuid,
                GraphGUID = graphGuid,
                GUID = Guid.NewGuid(),
                From = req.From,
                To = req.To,
                Name = req.Type,
                Labels = [req.Type],
                Cost = (int)(req.Cost ?? 0),
                Tags = new NameValueCollection
                {
                    { "directed", req.Undirected ? "false" : "true" }
                },
                Data = req.Attributes
                    .ToDictionary(kv => kv.Key, kv => kv.Value!)
            };
            await liteGraph.PutAsync(EdgesPath(graphGuid), ToPayload(edge), ct);
            edges.Add(edge);
        }

        logger.LogInformation(
            "Batch created {Count} edges in graph {Graph}",
            edges.Count, graphGuid);
        return edges;
    }

    private async Task<Node?> ReadNodeAsync(Guid graphGuid, Guid nodeGuid, CancellationToken ct)
    {
        var path = $"/v1.0/tenants/{_tenantGuid}/graphs/{graphGuid}/nodes/{nodeGuid}"
                 + "?includeData=true&includeSubordinates=false";
        var json = await liteGraph.GetAsync(path, ct);
        if (json is null) return null;

        var target = json.Value;
        if (target.TryGetProperty("Objects", out var objs)
            && objs.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var o in objs.EnumerateArray()) { target = o; break; }
        }

        return new Node
        {
            GUID = target.TryGetProperty("GUID", out var g) ? g.GetGuid() : Guid.Empty,
            Name = target.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "",
        };
    }

    private static object ToPayload(Edge edge)
    {
        return new
        {
            GUID = edge.GUID,
            TenantGUID = edge.TenantGUID,
            GraphGUID = edge.GraphGUID,
            From = edge.From,
            To = edge.To,
            Name = edge.Name,
            Labels = edge.Labels,
            Cost = edge.Cost,
            Tags = edge.Tags?.AllKeys
                .ToDictionary(k => k, k => edge.Tags[k]),
            Data = edge.Data,
        };
    }

    private string EdgesPath(Guid graphGuid)
        => $"/v1.0/tenants/{_tenantGuid}/graphs/{graphGuid}/edges";

    private string EdgePath(Guid graphGuid, Guid edgeGuid)
        => $"/v1.0/tenants/{_tenantGuid}/graphs/{graphGuid}/edges/{edgeGuid}";
}