using MAFRagService.Services;
using MAFRagService.Stubs.MAF;

namespace MAFRagService.Tools;

[Tool("Storage")]
public class StorageTool : ITool
{
    private readonly DocumentStorageService _storage;

    public StorageTool(DocumentStorageService storage) => _storage = storage;

    [ToolFunction("Get document download URL")]
    public async Task<string> GetDownloadUrlAsync(
        [Parameter(Description = "Blob name")] string blobName,
        [Parameter(Description = "Tenant")] string tenant)
    {
        await Task.CompletedTask;
        return blobName;
    }
}
