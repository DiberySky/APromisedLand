using MAFRagService.Services;
using MAFRagService.Stubs.MAF;

namespace MAFRagService.Tools;

[Tool("GraphQuery")]
public class GraphQueryTool : ITool
{
    private readonly KnowledgeGraphService _graph;

    public GraphQueryTool(KnowledgeGraphService graph) => _graph = graph;

    [ToolFunction("Execute graph query")]
    public async Task<object> QueryAsync(
        [Parameter(Description = "Cypher-style query (nGQL)")] string query,
        [Parameter(Description = "Tenant")] string tenant)
    {
        return await _graph.ExecuteQueryAsync(query, tenant);
    }
}
