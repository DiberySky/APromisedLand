using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DiberyBlazorWebSky.Models.Graph;
using LiteGraph.Sdk;

namespace DiberyBlazorWebSky.Services;

/// <summary>
/// 图数据的批量导入/导出。
/// 支持 JSON 和 CSV 格式；节点以 name 引用，便于跨环境迁移。
/// </summary>
public class GraphImportExportService
{
    private readonly LiteGraphSdk _sdk;
    private readonly ILogger<GraphImportExportService> _logger;

    private static readonly Guid DefaultTenant = Guid.Empty;
    private const int MaxResults = 1000;

    private static readonly JsonSerializerOptions ExportJson = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public GraphImportExportService(
        LiteGraphSdk sdk,
        ILogger<GraphImportExportService> logger)
    {
        _sdk = sdk;
        _logger = logger;
    }

    // ══════════════════════════════════════════════════════
    // 导出
    // ══════════════════════════════════════════════════════

    /// <summary>导出当前图为 JSON 字符串。</summary>
    public async Task<string> ExportToJsonAsync(Guid graphGuid, string graphName)
    {
        var nodes = await LoadNodesAsync(graphGuid);
        var edges = await LoadEdgesAsync(graphGuid);
        var nodeMap = nodes.ToDictionary(n => n.GUID, n => n.Name);

        var dto = new GraphExportDto
        {
            GraphName = graphName,
            ExportedAt = DateTime.UtcNow,
            Nodes = nodes.Select(n => new ExportNodeDto
            {
                Name = n.Name,
                Labels = n.Labels
            }).ToList(),
            Edges = edges.Select(e => new ExportEdgeDto
            {
                From = nodeMap.TryGetValue(e.From, out var f) ? f : e.From.ToString(),
                To = nodeMap.TryGetValue(e.To, out var t) ? t : e.To.ToString(),
                Name = e.Name
            }).ToList()
        };

        return JsonSerializer.Serialize(dto, ExportJson);
    }

    /// <summary>导出节点为 CSV（name,labels）。</summary>
    public async Task<string> ExportNodesToCsvAsync(Guid graphGuid)
    {
        var nodes = await LoadNodesAsync(graphGuid);

        var sb = new StringBuilder();
        sb.AppendLine("name,labels");

        foreach (var n in nodes)
        {
            var labels = n.Labels is { Count: > 0 }
                ? string.Join(";", n.Labels)
                : "";
            sb.AppendLine($"{EscapeCsv(n.Name)},{EscapeCsv(labels)}");
        }

        return sb.ToString();
    }

    /// <summary>导出边为 CSV（from,to,name）。</summary>
    public async Task<string> ExportEdgesToCsvAsync(Guid graphGuid)
    {
        var nodes = await LoadNodesAsync(graphGuid);
        var edges = await LoadEdgesAsync(graphGuid);
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

    /// <summary>
    /// 从 JSON 导入。
    /// </summary>
    /// <param name="clearExisting">是否在导入前清空图中所有节点和边</param>
    public async Task<ImportResult> ImportFromJsonAsync(
        Guid graphGuid, string jsonContent, bool clearExisting)
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
            clearExisting,
            result);
    }

    /// <summary>
    /// 从 CSV 导入（节点 CSV + 可选边 CSV）。
    /// </summary>
    public async Task<ImportResult> ImportFromCsvAsync(
        Guid graphGuid, string nodesCsv, string? edgesCsv, bool clearExisting)
    {
        var result = new ImportResult();

        // 解析节点 CSV
        List<string> nodeNames = new();
        try
        {
            var rows = ParseCsv(nodesCsv);
            if (rows.Count == 0)
            {
                result.Errors.Add("节点 CSV 为空。");
                return result;
            }

            // 跳过表头
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

        // 解析边 CSV（可选）
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
                        ? rows[i][2].Trim()
                        : "RELATED_TO";
                    if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) continue;
                    edges.Add((from, to, name));
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"边 CSV 解析失败：{ex.Message}");
            }
        }

        return await ImportInternalAsync(graphGuid, nodeNames, edges, clearExisting, result);
    }

    // ══════════════════════════════════════════════════════
    // 内部实现
    // ══════════════════════════════════════════════════════

    private async Task<ImportResult> ImportInternalAsync(
        Guid graphGuid,
        List<string> nodeNames,
        List<(string From, string To, string Name)> edgeSpecs,
        bool clearExisting,
        ImportResult result)
    {
        // 1. 可选：清空现有数据
        if (clearExisting)
        {
            try
            {
                var existingNodes = await LoadNodesAsync(graphGuid);
                var existingEdges = await LoadEdgesAsync(graphGuid);

                foreach (var e in existingEdges)
                    await _sdk.Edge.DeleteByGuid(DefaultTenant, graphGuid, e.GUID);
                foreach (var n in existingNodes)
                    await _sdk.Node.DeleteByGuid(DefaultTenant, graphGuid, n.GUID);

                _logger.LogInformation(
                    "清空图 {GraphGuid}：删除 {N} 节点 / {E} 边",
                    graphGuid, existingNodes.Count, existingEdges.Count);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"清空失败：{ex.Message}");
                return result;
            }
        }

        // 2. 建立 name → GUID 映射（包括图中现有节点）
        var nameToGuid = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var existing = await LoadNodesAsync(graphGuid);
        foreach (var n in existing)
        {
            if (!nameToGuid.ContainsKey(n.Name))
                nameToGuid[n.Name] = n.GUID;
        }

        // 3. 逐个创建节点（去重）
        foreach (var name in nodeNames)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;

            if (nameToGuid.ContainsKey(name))
            {
                result.NodesSkipped++;
                continue;
            }

            try
            {
                var node = new Node
                {
                    TenantGUID = DefaultTenant,
                    GraphGUID = graphGuid,
                    Name = name
                };
                var created = await _sdk.Node.Create(node);
                nameToGuid[name] = created.GUID;
                result.NodesCreated++;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"创建节点 \"{name}\" 失败：{ex.Message}");
            }
        }

        // 4. 创建边
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
                await _sdk.Edge.Create(edge);
                result.EdgesCreated++;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"创建边 {fromName} → {toName} 失败：{ex.Message}");
                result.EdgesSkipped++;
            }
        }

        _logger.LogInformation(
            "导入完成：节点 {NC}新增/{NS}跳过，边 {EC}新增/{ES}跳过",
            result.NodesCreated, result.NodesSkipped,
            result.EdgesCreated, result.EdgesSkipped);

        return result;
    }

    private async Task<List<Node>> LoadNodesAsync(Guid graphGuid)
    {
        var query = new EnumerationRequest
        {
            TenantGUID = DefaultTenant,
            GraphGUID = graphGuid,
            MaxResults = MaxResults
        };
        var result = await _sdk.Node.Enumerate(query);
        return result.Objects ?? new List<Node>();
    }

    private async Task<List<Edge>> LoadEdgesAsync(Guid graphGuid)
    {
        var query = new EnumerationRequest
        {
            TenantGUID = DefaultTenant,
            GraphGUID = graphGuid,
            MaxResults = MaxResults
        };
        var result = await _sdk.Edge.Enumerate(query);
        return result.Objects ?? new List<Edge>();
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
    {
        return row.Length > 0 &&
               row[0].Trim().Equals(firstColumnName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>简单 CSV 解析器：支持引号包裹、逗号转义、双引号转义。</summary>
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
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        field.Append('"');
                        i += 2;
                        continue;
                    }
                    inQuotes = false;
                    i++;
                    continue;
                }
                field.Append(c);
                i++;
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                    i++;
                }
                else if (c == ',')
                {
                    current.Add(field.ToString());
                    field.Clear();
                    i++;
                }
                else if (c == '\r')
                {
                    i++;
                }
                else if (c == '\n')
                {
                    current.Add(field.ToString());
                    field.Clear();
                    rows.Add(current.ToArray());
                    current.Clear();
                    i++;
                }
                else
                {
                    field.Append(c);
                    i++;
                }
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