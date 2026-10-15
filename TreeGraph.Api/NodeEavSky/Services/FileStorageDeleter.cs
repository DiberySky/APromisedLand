using System.Net;
using System.Net.Http.Json;

namespace TreeGraph.Api.NodeEavSky.Services;

/// <summary>
/// IFileStorageDeleter 的 HTTP 实现。
/// 通过 AddHttpClient 注入 HttpClient（ServiceName = "FileStorageApi"）。
/// </summary>
public class FileStorageDeleter : IFileStorageDeleter
{
    private readonly HttpClient _http;
    private readonly ILogger<FileStorageDeleter> _logger;

    public FileStorageDeleter(HttpClient http, ILogger<FileStorageDeleter> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<bool> DeleteByDocIdAsync(
        string docId, int? version = null, CancellationToken ct = default)
    {
        var url = $"Files/by-doc/{Uri.EscapeDataString(docId)}";
        if (version.HasValue) url += $"?version={version.Value}";

        try
        {
            var resp = await _http.DeleteAsync(url, ct);
            if (resp.StatusCode == HttpStatusCode.NotFound) return false;
            resp.EnsureSuccessStatusCode();
            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex,
                "级联删除文件失败：docId={DocId}, version={Version}", docId, version);
            return false;
        }
    }

    public async Task DeleteBatchAsync(
        IEnumerable<(string DocId, int? Version)> items, CancellationToken ct = default)
    {
        foreach (var (docId, version) in items)
        {
            try
            {
                await DeleteByDocIdAsync(docId, version, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "批量级联删除中跳过：docId={DocId}, version={Version}", docId, version);
            }
        }
    }
}
