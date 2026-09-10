using MAFRagService.Models;
using MAFRagService.Services;
using MAFRagService.Stubs.MAF;

namespace MAFRagService.Agents;

public class GraphReasonerAgent : Agent
{
    private readonly KnowledgeGraphService _graphService;

    public GraphReasonerAgent(KnowledgeGraphService graphService) => _graphService = graphService;

    [AgentFunction("ReasonWithGraph")]
    public async Task<GraphContext> ReasonAsync(
        [Parameter(Description = "Query intent")] QueryIntent intent,
        [Parameter(Description = "Tenant")] string tenant,
        CancellationToken ct)
    {
        var entities = intent.Entities ?? new List<string>();
        return await _graphService.GetGraphContextAsync(entities, tenant);
    }
}
