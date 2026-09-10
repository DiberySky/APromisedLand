
using MAFRagService.Models;
using MAFRagService.Services;
using MAFRagService.Stubs.MAF;

namespace MAFRagService.Agents;


public class RetrieverAgent : Agent
{
    private readonly RagService _ragService;

    public RetrieverAgent(RagService ragService) => _ragService = ragService;

    [AgentFunction("RetrieveDocuments")]
    public async Task<List<Source>> RetrieveAsync(
        [Parameter(Description = "Query intent")] QueryIntent intent,
        [Parameter(Description = "Tenant")] string tenant,
        [Parameter(Description = "Version")] string? version,
        [Parameter(Description = "Limit")] int limit,
        CancellationToken ct)
    {
        return await _ragService.SearchAsync(intent.Query, tenant, limit, version ?? "latest", ct);
    }
}
