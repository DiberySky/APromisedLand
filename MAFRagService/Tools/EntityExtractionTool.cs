using MAFRagService.Models;
using MAFRagService.Services;
using MAFRagService.Stubs.MAF;

namespace MAFRagService.Tools;

[Tool("EntityExtraction")]
public class EntityExtractionTool : ITool
{
    private readonly IEntityExtractionService _entityService;

    public EntityExtractionTool(IEntityExtractionService entityService) => _entityService = entityService;

    [ToolFunction("Extract entities from text")]
    public async Task<List<EntityInfo>> ExtractAsync(
        [Parameter(Description = "Text to extract entities from")] string text,
        [Parameter(Description = "Tenant")] string tenant)
    {
        return await _entityService.ExtractAsync(text, tenant, CancellationToken.None);
    }
}
