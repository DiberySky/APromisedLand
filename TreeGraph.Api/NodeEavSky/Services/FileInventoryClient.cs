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
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // HttpClient 超时抛出 TaskCanceledException（非 HttpRequestException），
            // 当作查询失败返回空集，避免连坐整轮孤儿清理。
            _logger.LogWarning("查询 FileStorageApi all-doc-ids 超时，返回空集");
            return new HashSet<string>();
        }
    }
}
