using System.Collections.Specialized;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LiteGraph.Sdk;
using MAFWorkFlowApi.Models.Graph;
using MAFWorkFlowApi.Infrastructure;

namespace MAFWorkFlowApi.Services;

/// <summary>
/// 图数据的批量导入/导出。用 LiteGraphSdk 直连 LiteGraph REST。
/// </summary>
public sealed class GraphExportService
{
    private static readonly Guid DefaultTenant = Guid.Empty;
    private const int MaxResults = 1000;

    private static readonly JsonSerializerOptions ExportJson = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly LiteGraphSdk _sdk;
    private readonly ILogger<GraphExportService> _logger;

    public GraphExportService(LiteGraphSdk sdk, ILogger<GraphExportService> logger)
    {
        _sdk = sdk;
        _logger = logger;
    }

    // ══════════════════════════════════════════════════════
    // 导出
    // ══════════════════════════════════════════════════════

    public async Task<string> ExportToJsonAsync(Guid graphGuid, CancellationToken ct = default)
    {
        // ★ 用命名参数 token，避免与 bool 参数冲突
        var graph = await _sdk.Graph.ReadByGuid(DefaultTenant, graphGuid, token: ct);

        var nodes = await LoadNodesAsync(graphGuid, ct);
        var edges = await LoadEdgesAsync(graphGuid, ct);
        var nodeMap = nodes.ToDictionary(n => n.GUID, n => n.Name);

        // ★ 手动构建 Tags 字典（避免 nullable key 问题）
        Dictionary<string, string>? tagsDict = null;
        if (graph?.Tags is { Count: > 0 } tags)
        {
            tagsDict = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var key in tags.AllKeys)
            {
                if (!string.IsNullOrEmpty(key))
                    tagsDict[key] = tags[key] ?? "";
            }
        }

        var dto = new GraphExportDto
        {
            Version = "1.0",
            GraphName = graph?.Name ?? "graph",
            ExportedAt = DateTime.UtcNow,
            GraphData = graph?.Data,          // ★ object?
            GraphLabels = graph?.Labels,
            GraphTags = tagsDict,
            Nodes = nodes.Select(n => new ExportNodeDto
            {
                Name = n.Name,
                Labels = n.Labels,
                Data = n.Data                 // ★ object?
            }).ToList(),
            Edges = edges.Select(e => new ExportEdgeDto
            {
                From = nodeMap.TryGetValue(e.From, out var f) ? f : e.From.ToString(),
                To = nodeMap.TryGetValue(e.To, out var t) ? t : e.To.ToString(),
                Name = e.Name,
                Data = e.Data                 // ★ object?
            }).ToList()
        };

        return JsonSerializer.Serialize(dto, ExportJson);
    }

    public async Task<string> ExportNodesToCsvAsync(Guid graphGuid, CancellationToken ct = default)
    {
        var nodes = await LoadNodesAsync(graphGuid, ct);
        var sb = new StringBuilder();
        sb.AppendLine("name,labels");
        foreach (var n in nodes)
        {
            var labels = n.Labels is { Count: > 0 } ? string.Join(";", n.Labels) : "";
            sb.AppendLine($"{EscapeCsv(n.Name)},{EscapeCsv(labels)}");
        }
        return sb.ToString();
    }

    public async Task<string> ExportEdgesToCsvAsync(Guid graphGuid, CancellationToken ct = default)
    {
        var nodes = await LoadNodesAsync(graphGuid, ct);
        var edges = await LoadEdgesAsync(graphGuid, ct);
        var nodeMap = nodes.ToDictionary(n => n.GUID, n => n.Name);

        var sb = new StringBuilder();
        sb.AppendLine("from,to,name");
        foreach (var e in edges)
        {
            var fromName = nodeMap.TryGetValue(e.From, out var f) ? f : e.From.ToString();
            var toName = nodeMap.TryGetValue(e.To, out var t) ? t : e.To.ToString();
            sb.AppendLine($"{EscapeCsv(fromName)},{EscapeCsv(toName)},{EscapeCsv(e.Name)}");
        }
        return sb.ToString();
    }

    // ══════════════════════════════════════════════════════
    // 导入
    // ══════════════════════════════════════════════════════

    public async Task<ImportResult> ImportFromJsonAsync(
        Guid graphGuid, string jsonContent, bool clearExisting, CancellationToken ct = default)
    {
        var result = new ImportResult();

        GraphExportDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<GraphExportDto>(jsonContent, ExportJson);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"JSON 解析失败：{ex.Message}");
            return result;
        }

        if (dto == null)
        {
            result.Errors.Add("JSON 内容为空。");
            return result;
        }

        return await ImportInternalAsync(
            graphGuid,
            dto.Nodes.Select(n => n.Name).ToList(),
            dto.Edges.Select(e => (e.From, e.To, e.Name)).ToList(),
            clearExisting, result, ct);
    }

    public async Task<ImportResult> ImportFromCsvAsync(
        Guid graphGuid, string nodesCsv, string? edgesCsv, bool clearExisting,
        CancellationToken ct = default)
    {
        var result = new ImportResult();

        List<string> nodeNames = new();
        try
        {
            var rows = ParseCsv(nodesCsv);
            if (rows.Count == 0)
            {
                result.Errors.Add("节点 CSV 为空。");
                return result;
            }
            int startIdx = IsHeaderRow(rows[0], "name") ? 1 : 0;
            for (int i = startIdx; i < rows.Count; i++)
            {
                if (rows[i].Length < 1 || string.IsNullOrWhiteSpace(rows[i][0])) continue;
                nodeNames.Add(rows[i][0].Trim());
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"节点 CSV 解析失败：{ex.Message}");
            return result;
        }

        List<(string From, string To, string Name)> edges = new();
        if (!string.IsNullOrWhiteSpace(edgesCsv))
        {
            try
            {
                var rows = ParseCsv(edgesCsv);
                int startIdx = rows.Count > 0 && IsHeaderRow(rows[0], "from") ? 1 : 0;
                for (int i = startIdx; i < rows.Count; i++)
                {
                    if (rows[i].Length < 2) continue;
                    var from = rows[i][0].Trim();
                    var to = rows[i][1].Trim();
                    var name = rows[i].Length >= 3 && !string.IsNullOrWhiteSpace(rows[i][2])
                        ? rows[i][2].Trim() : "RELATED_TO";
                    if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) continue;
                    edges.Add((from, to, name));
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"边 CSV 解析失败：{ex.Message}");
            }
        }

        return await ImportInternalAsync(graphGuid, nodeNames, edges, clearExisting, result, ct);
    }

    /// <summary>把 JSON 导入为新图。</summary>
    public async Task<(Guid? NewGraphGuid, ImportResult Result)> ImportAsNewGraphAsync(
        string jsonContent, string newGraphName, CancellationToken ct = default)
    {
        var result = new ImportResult();

        GraphExportDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<GraphExportDto>(jsonContent, ExportJson);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"JSON 解析失败：{ex.Message}");
            return (null, result);
        }

        if (dto == null)
        {
            result.Errors.Add("JSON 内容为空。");
            return (null, result);
        }

        Guid newGraphGuid;
        try
        {
            var newGraph = new Graph
            {
                TenantGUID = DefaultTenant,
                Name = string.IsNullOrWhiteSpace(newGraphName)
                    ? $"{dto.GraphName} (imported)"
                    : newGraphName.Trim(),
                Data = dto.GraphData,
                Labels = dto.GraphLabels,
                // ★ 手动构建 NameValueCollection
                Tags = BuildTags(dto.GraphTags)
            };
            var created = await _sdk.Graph.Create(newGraph, ct);
            newGraphGuid = created.GUID;
            _logger.LogInformation("已创建新图 {Name} ({Guid})", created.Name, created.GUID);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"创建新图失败：{ex.Message}");
            return (null, result);
        }

        var importResult = await ImportInternalAsync(
            newGraphGuid,
            dto.Nodes.Select(n => n.Name).ToList(),
            dto.Edges.Select(e => (e.From, e.To, e.Name)).ToList(),
            clearExisting: false, result, ct);

        return (newGraphGuid, importResult);
    }

    // ══════════════════════════════════════════════════════
    // 内部实现
    // ══════════════════════════════════════════════════════

    private async Task<ImportResult> ImportInternalAsync(
        Guid graphGuid,
        List<string> nodeNames,
        List<(string From, string To, string Name)> edgeSpecs,
        bool clearExisting,
        ImportResult result,
        CancellationToken ct)
    {
        if (clearExisting)
        {
            try
            {
                var existingNodes = await LoadNodesAsync(graphGuid, ct);
                var existingEdges = await LoadEdgesAsync(graphGuid, ct);

                foreach (var e in existingEdges)
                    await _sdk.Edge.DeleteByGuid(DefaultTenant, graphGuid, e.GUID, ct);
                foreach (var n in existingNodes)
                    await _sdk.Node.DeleteByGuid(DefaultTenant, graphGuid, n.GUID, ct);

                _logger.LogInformation("清空图 {Guid}：删除 {N} 节点 / {E} 边",
                    graphGuid, existingNodes.Count, existingEdges.Count);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"清空失败：{ex.Message}");
                return result;
            }
        }

        var nameToGuid = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var existing = await LoadNodesAsync(graphGuid, ct);
        foreach (var n in existing)
        {
            if (!nameToGuid.ContainsKey(n.Name))
                nameToGuid[n.Name] = n.GUID;
        }

        foreach (var name in nodeNames)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (nameToGuid.ContainsKey(name)) { result.NodesSkipped++; continue; }

            try
            {
                var node = new Node
                {
                    TenantGUID = DefaultTenant,
                    GraphGUID = graphGuid,
                    Name = name
                };
                var created = await _sdk.Node.Create(node, ct);
                nameToGuid[name] = created.GUID;
                result.NodesCreated++;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"创建节点 \"{name}\" 失败：{ex.Message}");
            }
        }

        foreach (var (fromName, toName, edgeName) in edgeSpecs)
        {
            if (!nameToGuid.TryGetValue(fromName, out var fromGuid))
            {
                result.Errors.Add($"边跳过：源节点 \"{fromName}\" 不存在");
                result.EdgesSkipped++;
                continue;
            }
            if (!nameToGuid.TryGetValue(toName, out var toGuid))
            {
                result.Errors.Add($"边跳过：目标节点 \"{toName}\" 不存在");
                result.EdgesSkipped++;
                continue;
            }

            try
            {
                var edge = new Edge
                {
                    TenantGUID = DefaultTenant,
                    GraphGUID = graphGuid,
                    From = fromGuid,
                    To = toGuid,
                    Name = edgeName
                };
                await _sdk.Edge.Create(edge, ct);
                result.EdgesCreated++;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"创建边 {fromName} → {toName} 失败：{ex.Message}");
                result.EdgesSkipped++;
            }
        }

        return result;
    }

    private async Task<List<Node>> LoadNodesAsync(Guid graphGuid, CancellationToken ct)
    {
        var query = new EnumerationRequest
        {
            TenantGUID = DefaultTenant,
            GraphGUID = graphGuid,
            MaxResults = MaxResults
        };
        var result = await _sdk.Node.Enumerate(query, ct);
        return result.Objects ?? new List<Node>();
    }

    private async Task<List<Edge>> LoadEdgesAsync(Guid graphGuid, CancellationToken ct)
    {
        var query = new EnumerationRequest
        {
            TenantGUID = DefaultTenant,
            GraphGUID = graphGuid,
            MaxResults = MaxResults
        };
        var result = await _sdk.Edge.Enumerate(query, ct);
        return result.Objects ?? new List<Edge>();
    }

    // ─── 辅助：构建 NameValueCollection ──────────────

    /// <summary>把 Dictionary 转为 NameValueCollection（跳过 null key）。</summary>
    private static NameValueCollection? BuildTags(Dictionary<string, string>? source)
    {
        if (source is null || source.Count == 0) return null;

        var result = new NameValueCollection();
        foreach (var kv in source)
        {
            if (!string.IsNullOrEmpty(kv.Key))
                result[kv.Key] = kv.Value ?? "";
        }
        return result;
    }

    // ─── CSV 工具 ────────────────────────────────────

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    private static bool IsHeaderRow(string[] row, string firstColumnName)
        => row.Length > 0 && row[0].Trim().Equals(firstColumnName, StringComparison.OrdinalIgnoreCase);

    private static List<string[]> ParseCsv(string content)
    {
        var rows = new List<string[]>();
        if (string.IsNullOrEmpty(content)) return rows;

        var current = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;
        int i = 0;

        while (i < content.Length)
        {
            char c = content[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"') { field.Append('"'); i += 2; continue; }
                    inQuotes = false; i++; continue;
                }
                field.Append(c); i++;
            }
            else
            {
                if (c == '"') { inQuotes = true; i++; }
                else if (c == ',') { current.Add(field.ToString()); field.Clear(); i++; }
                else if (c == '\r') { i++; }
                else if (c == '\n')
                {
                    current.Add(field.ToString()); field.Clear();
                    rows.Add(current.ToArray()); current.Clear(); i++;
                }
                else { field.Append(c); i++; }
            }
        }

        if (field.Length > 0 || current.Count > 0)
        {
            current.Add(field.ToString());
            rows.Add(current.ToArray());
        }

        return rows;
    }
}