using System.Net.Http.Json;

namespace TreeGraph.Api.NodeEavSky.Services;

public class FileInventoryClient : IFileInventoryClient
{
    private readonly HttpClient _http;
    private readonly ILogger<FileInventoryClient> _logger;

    public FileInventoryClient(HttpClient http, ILogger<FileInventoryClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<HashSet<string>> GetAllDocIdsAsync(CancellationToken ct = default)
    {
        try
        {
            var ids = await _http.GetFromJsonAsync<List<string>>(
                "Files/all-doc-ids", ct);
            return ids?.ToHashSet() ?? new HashSet<string>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "查询 FileStorageApi all-doc-ids 失败，返回空集");
            return new HashSet<string>();
        }
    }
}
