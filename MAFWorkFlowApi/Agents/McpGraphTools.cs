using System.ComponentModel;
using ModelContextProtocol.Server;

namespace MAFWorkFlowApi.Agents;

/// <summary>
/// MCP 工具适配器：把 GraphTools 的 8 个方法包装为 MCP 工具，
/// 暴露给 Cherry Studio / Claude Desktop / Cursor 等外部 AI 客户端。
/// </summary>
[McpServerToolType]
public sealed class McpGraphTools
{
    private readonly GraphTools _tools;
    private readonly ILogger<McpGraphTools> _logger;

    public McpGraphTools(GraphTools tools, ILogger<McpGraphTools> logger)
    {
        _tools = tools;
        _logger = logger;
    }

    [McpServerTool(Name = "get_neighbors")]
    [Description("获取指定节点的所有邻居节点（出边+入边）。用户问'X 有哪些邻居'时使用。")]
    public async Task<string> GetNeighbors(
        [Description("节点的完整名称，必须一字不差地传递。" +
                     "如果用户提到'根节点C'，你必须传 '根节点C'（含全部前缀），不能只传 'C'。" +
                     "如果用户提到'PostgreSQL'，就传 'PostgreSQL'。")]
        string nodeName,
        [Description("图名称，如 'TestGraph'")]
        string graphName,
        CancellationToken ct = default)
    {
        _logger.LogInformation("[MCP] get_neighbors({Node}, {Graph})", nodeName, graphName);
        return await _tools.GetNeighborsAsync(nodeName, graphName, ct);
    }

    [McpServerTool(Name = "get_relations")]
    [Description("查询两个节点之间的直接关系。用户问'A 和 B 是什么关系'时使用。")]
    public async Task<string> GetRelations(
        [Description("第一个节点的完整名称，必须一字不差。例如 '根节点C'，不能只传 'C'。")]
        string nodeA,
        [Description("第二个节点的完整名称，必须一字不差。例如 '根节点B'，不能只传 'B'。")]
        string nodeB,
        [Description("图名称，如 'TestGraph'")]
        string graphName,
        CancellationToken ct = default)
    {
        _logger.LogInformation("[MCP] get_relations({A}, {B}, {Graph})", nodeA, nodeB, graphName);
        return await _tools.GetRelationsAsync(nodeA, nodeB, graphName, ct);
    }

    [McpServerTool(Name = "list_all_nodes")]
    [Description("列出图中所有节点和边。用户问'有哪些节点'、'列出全部'时使用。")]
    public async Task<string> ListAllNodes(
        [Description("图名称，如 'TestGraph'")]
        string graphName,
        CancellationToken ct = default)
    {
        _logger.LogInformation("[MCP] list_all_nodes({Graph})", graphName);
        return await _tools.ListAllNodesAsync(graphName, ct);
    }

    [McpServerTool(Name = "get_graph_size")]
    [Description("获取图的规模（节点数+边数）。用户问'图有多大'时使用。")]
    public async Task<string> GetGraphSize(
        [Description("图名称，如 'TestGraph'")]
        string graphName,
        CancellationToken ct = default)
    {
        _logger.LogInformation("[MCP] get_graph_size({Graph})", graphName);
        return await _tools.GetGraphSizeAsync(graphName, ct);
    }

    [McpServerTool(Name = "find_path")]
    [Description("查找两个节点之间的最短路径。用户问'从 A 到 B 怎么走'时使用。")]
    public async Task<string> FindPath(
        [Description("起始节点的完整名称，必须一字不差。例如 '根节点C'，不能只传 'C'。")]
        string fromNode,
        [Description("目标节点的完整名称，必须一字不差。例如 'TestNode'，不能只传 'Node'。")]
        string toNode,
        [Description("图名称，如 'TestGraph'")]
        string graphName,
        CancellationToken ct = default)
    {
        _logger.LogInformation("[MCP] find_path({From}, {To}, {Graph})", fromNode, toNode, graphName);
        return await _tools.FindPathAsync(fromNode, toNode, graphName, ct);
    }

    [McpServerTool(Name = "search_nodes")]
    [Description("按关键词搜索名称包含该关键词的节点。用户不确定节点全名时使用。")]
    public async Task<string> SearchNodes(
        [Description("搜索关键词，例如 'docker'、'数据库'")]
        string keyword,
        [Description("图名称，如 'TestGraph'")]
        string graphName,
        CancellationToken ct = default)
    {
        _logger.LogInformation("[MCP] search_nodes({Keyword}, {Graph})", keyword, graphName);
        return await _tools.SearchNodesAsync(keyword, graphName, ct);
    }

    [McpServerTool(Name = "get_subgraph")]
    [Description("从起始节点出发 N 跳内的子图（1-3 跳）。用户想探索周边时使用。")]
    public async Task<string> GetSubgraph(
        [Description("起始节点的完整名称，必须一字不差。例如 'AI' 或 '根节点C'。")]
        string startNode,
        [Description("跳数（1-3 之间的整数）")]
        int hops,
        [Description("图名称，如 'TestGraph'")]
        string graphName,
        CancellationToken ct = default)
    {
        _logger.LogInformation("[MCP] get_subgraph({Start}, hops={Hops}, {Graph})",
            startNode, hops, graphName);
        return await _tools.GetSubgraphAsync(startNode, hops, graphName, ct);
    }

    [McpServerTool(Name = "get_edges_of_node")]
    [Description("查询节点的出边和入边（区分方向）。用户问'X 指向谁'、'谁指向 X'时使用。")]
    public async Task<string> GetEdgesOfNode(
        [Description("节点的完整名称，必须一字不差。例如 'AI'，不能只传 'A'。")]
        string nodeName,
        [Description("图名称，如 'TestGraph'")]
        string graphName,
        CancellationToken ct = default)
    {
        _logger.LogInformation("[MCP] get_edges_of_node({Node}, {Graph})", nodeName, graphName);
        return await _tools.GetEdgesOfNodeAsync(nodeName, graphName, ct);
    }
}