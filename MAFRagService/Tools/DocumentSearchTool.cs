using MAFRagService.Models;
using MAFRagService.Services;
using MAFRagService.Stubs.MAF;

namespace MAFRagService.Tools;

[Tool("DocumentSearch")]
public class DocumentSearchTool : ITool
{
    private readonly RagService _rag;

    public DocumentSearchTool(RagService rag) => _rag = rag;

    [ToolFunction("Search documents by query")]
    public async Task<List<Source>> SearchAsync(
        [Parameter(Description = "Search query")] string query,
        [Parameter(Description = "Tenant")] string tenant,
        [Parameter(Description = "Top K")] int topK = 5)
    {
        return await _rag.SearchAsync(query, tenant, topK, "latest", CancellationToken.None);
    }
}
