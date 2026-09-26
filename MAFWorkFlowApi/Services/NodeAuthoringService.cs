using System.Collections.Specialized;
using System.Text.Json;
using LiteGraph.Sdk;   // 只借用 Node/Edge 数据模型
using MAFWorkFlowApi.Infrastructure;
using MAFWorkFlowApi.Models;

namespace MAFWorkFlowApi.Services;

public sealed class NodeAuthoringService(
    LiteGraphRestClient liteGraph,
    ILogger<NodeAuthoringService> logger)
{
    private readonly Guid _tenantGuid = liteGraph.TenantGuid;

    // ─── 单条录入 ──────────────────────────────────────

    public async Task<NodeUpsertResult> UpsertAsync(
        Guid graphGuid, NodeUpsertRequest request,
        CancellationToken ct = default)
    {
        // ① 解析父节点
        Node? parent = null;
        if (request.ParentGuid is { } pg)
        {
            parent = await ReadNodeAsync(graphGuid, pg, ct);
            if (parent is null)
                return NodeUpsertResult.Fail(
                    "parent_not_found", $"父节点不存在：{pg}");
        }

        // ② 全称
        var parentPath = parent?.Tags?["path_name"];
        var pathName = BuildPathName(parentPath, request.DisplayName);

        // ③ 同级重名校验（用确定性 GUID 探测）
        var candidateGuid = StableGuid.ForNode(graphGuid, pathName);
        var existingByPath = await ReadNodeAsync(graphGuid, candidateGuid, ct);
        if (existingByPath is not null && existingByPath.GUID != request.Guid)
            return NodeUpsertResult.Fail(
                "duplicate_sibling",
                $"同级已存在 '{request.DisplayName}'");

        var guid = request.Guid ?? candidateGuid;

        // ④ 构造 Node
        var labels = new List<string> { request.Type };
        if (request.ExtraLabels is { Count: > 0 })
            labels.AddRange(request.ExtraLabels);

        var node = new Node
        {
            TenantGUID = _tenantGuid,
            GraphGUID = graphGuid,
            GUID = guid,
            Name = request.DisplayName,
            Labels = labels,
            Tags = new NameValueCollection
            {
                { "path_name", pathName },
                { "display_name", request.DisplayName }
            },
            Data = request.Attributes
                .ToDictionary(kv => kv.Key, kv => kv.Value!)
        };

        // ⑤ 写入
        var isNew = request.Guid is null;
        if (isNew)
        {
            await liteGraph.PutAsync(
                NodesPath(graphGuid), ToLiteGraphPayload(node), ct);
        }
        else
        {
            await liteGraph.PutAsync(
                NodePath(graphGuid, guid), ToLiteGraphPayload(node), ct);
        }

        logger.LogInformation(
            "Node {Action}: {Name} ({Guid}), path = {Path}",
            isNew ? "created" : "updated",
            node.Name, node.GUID, pathName);

        return NodeUpsertResult.Ok(node, isNew, pathName);
    }

    // ─── 单条删除（级联边）─────────────────────────────

    public async Task DeleteAsync(
        Guid graphGuid, Guid nodeGuid,
        CancellationToken ct = default)
    {
        // ① 删除出边
        var outEdges = await ReadEdgesFromNodeAsync(graphGuid, nodeGuid, ct);
        foreach (var e in outEdges)
            await liteGraph.DeleteAsync(EdgePath(graphGuid, e.GUID), ct);

        // ② 删除入边
        var inEdges = await ReadEdgesToNodeAsync(graphGuid, nodeGuid, ct);
        foreach (var e in inEdges)
            await liteGraph.DeleteAsync(EdgePath(graphGuid, e.GUID), ct);

        // ③ 删除顶点
        await liteGraph.DeleteAsync(NodePath(graphGuid, nodeGuid), ct);

        logger.LogInformation(
            "Node deleted: {Guid}, removed {Out}+{In} edges",
            nodeGuid, outEdges.Count, inEdges.Count);
    }

    // ─── 批量 ─────────────────────────────────────────

    public async Task<List<Node>> CreateBatchAsync(
        Guid graphGuid, List<NodeUpsertRequest> requests,
        CancellationToken ct = default)
    {
        var nodes = new List<Node>();

        foreach (var req in requests)
        {
            var parentPath = req.ParentGuid is { } pg
                ? (await ReadNodeAsync(graphGuid, pg, ct))?.Tags?["path_name"]
                : null;

            var pathName = BuildPathName(parentPath, req.DisplayName);

            var node = new Node
            {
                TenantGUID = _tenantGuid,
                GraphGUID = graphGuid,
                GUID = StableGuid.ForNode(graphGuid, pathName),
                Name = req.DisplayName,
                Labels = [req.Type, .. req.ExtraLabels ?? []],
                Tags = new NameValueCollection
                {
                    { "path_name", pathName },
                    { "display_name", req.DisplayName }
                },
                Data = req.Attributes
                    .ToDictionary(kv => kv.Key, kv => kv.Value!)
            };

            await liteGraph.PutAsync(
                NodesPath(graphGuid), ToLiteGraphPayload(node), ct);
            nodes.Add(node);
        }

        logger.LogInformation(
            "Batch created {Count} nodes in graph {Graph}",
            nodes.Count, graphGuid);

        return nodes;
    }

    // ─── 向量搜索 ──────────────────────────────────────

    public Task<List<Node>> SearchByVectorAsync(
        Guid graphGuid, List<float> queryVector,
        string model, int topK, double? minScore,
        CancellationToken ct = default)
    {
        throw new NotSupportedException(
            "向量搜索请通过 LiteGraph REST 端点的 /vector 子路径实现，"
            + "或升级到支持向量检索的 LiteGraph 版本。");
    }

    // ─── REST 辅助 ────────────────────────────────────

    private async Task<Node?> ReadNodeAsync(Guid graphGuid, Guid nodeGuid, CancellationToken ct)
    {
        var path = $"/v1.0/tenants/{_tenantGuid}/graphs/{graphGuid}/nodes/{nodeGuid}"
                 + "?includeData=true&includeSubordinates=false";
        var json = await liteGraph.GetAsync(path, ct);
        return json is null ? null : DeserializeNode(json.Value);
    }

    private async Task<List<Edge>> ReadEdgesFromNodeAsync(Guid graphGuid, Guid nodeGuid, CancellationToken ct)
    {
        var path = $"/v1.0/tenants/{_tenantGuid}/graphs/{graphGuid}/nodes/{nodeGuid}/edges/from";
        return await ReadEdgeListAsync(path, ct);
    }

    private async Task<List<Edge>> ReadEdgesToNodeAsync(Guid graphGuid, Guid nodeGuid, CancellationToken ct)
    {
        var path = $"/v1.0/tenants/{_tenantGuid}/graphs/{graphGuid}/nodes/{nodeGuid}/edges/to";
        return await ReadEdgeListAsync(path, ct);
    }

    private async Task<List<Edge>> ReadEdgeListAsync(string path, CancellationToken ct)
    {
        var json = await liteGraph.GetAsync(path, ct);
        if (json is null) return [];

        // LiteGraph 分页响应形如：{ Objects: [...], ... }
        if (!json.Value.TryGetProperty("Objects", out var objs) ||
            objs.ValueKind != JsonValueKind.Array)
            return [];

        var list = new List<Edge>();
        foreach (var e in objs.EnumerateArray())
            list.Add(DeserializeEdge(e));
        return list;
    }

    private static Node DeserializeNode(JsonElement json)
    {
        // 若响应包 { Objects: [...] } 或直接是 node 对象
        var target = json;
        if (json.TryGetProperty("Objects", out var objs)
            && objs.ValueKind == JsonValueKind.Array)
        {
            foreach (var o in objs.EnumerateArray())
            {
                target = o;
                break;
            }
        }

        var node = new Node
        {
            GUID = target.TryGetProperty("GUID", out var g) ? g.GetGuid() : Guid.Empty,
            TenantGUID = target.TryGetProperty("TenantGUID", out var tg) ? tg.GetGuid() : Guid.Empty,
            GraphGUID = target.TryGetProperty("GraphGUID", out var gg) ? gg.GetGuid() : Guid.Empty,
            Name = target.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "",
            Tags = new NameValueCollection()
        };

        if (target.TryGetProperty("Tags", out var tags)
            && tags.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in tags.EnumerateObject())
                node.Tags[p.Name] = p.Value.GetString();
        }

        return node;
    }

    private static Edge DeserializeEdge(JsonElement e)
    {
        var edge = new Edge
        {
            GUID = e.TryGetProperty("GUID", out var g) ? g.GetGuid() : Guid.Empty,
            TenantGUID = e.TryGetProperty("TenantGUID", out var tg) ? tg.GetGuid() : Guid.Empty,
            GraphGUID = e.TryGetProperty("GraphGUID", out var gg) ? gg.GetGuid() : Guid.Empty,
            From = e.TryGetProperty("From", out var f) ? f.GetGuid() : Guid.Empty,
            To = e.TryGetProperty("To", out var t) ? t.GetGuid() : Guid.Empty,
            Name = e.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "",
        };
        return edge;
    }

    private static object ToLiteGraphPayload(Node node)
    {
        // REST API 需要纯 JSON 对象（不含 SDK 特有字段）
        return new
        {
            GUID = node.GUID,
            TenantGUID = node.TenantGUID,
            GraphGUID = node.GraphGUID,
            Name = node.Name,
            Labels = node.Labels,
            Tags = node.Tags?.AllKeys
                .ToDictionary(k => k!, k => node.Tags[k]!),
            Data = node.Data,
        };
    }

    private string NodesPath(Guid graphGuid)
        => $"/v1.0/tenants/{_tenantGuid}/graphs/{graphGuid}/nodes";

    private string NodePath(Guid graphGuid, Guid nodeGuid)
        => $"/v1.0/tenants/{_tenantGuid}/graphs/{graphGuid}/nodes/{nodeGuid}";

    private string EdgePath(Guid graphGuid, Guid edgeGuid)
        => $"/v1.0/tenants/{_tenantGuid}/graphs/{graphGuid}/edges/{edgeGuid}";

    private static string BuildPathName(string? parentPath, string displayName)
        => string.IsNullOrEmpty(parentPath)
            ? displayName
            : $"{parentPath}/{displayName}";
}