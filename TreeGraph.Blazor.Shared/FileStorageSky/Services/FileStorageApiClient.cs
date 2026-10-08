using System.Net.Http.Headers;
using System.Net.Http.Json;
using TreeGraph.Shared.FileStorageSky.Contracts;

namespace TreeGraph.Blazor.Shared.FileStorageSky.Services;

/// <summary>
/// 文件存储 HTTP 客户端实现。
/// 走 TreeGraph.FileStorageApi 的 /Uploads + /Files 路由。
///
/// 用法：
///   1. Program.cs 中注册 AddHttpClient&lt;IFileStorageClient, FileStorageApiClient&gt;(...)
///   2. BaseAddress 指向 FileStorageApi 服务（Aspire 服务发现 treegraphfilestorageapi）
///   3. 注入 IFileStorageClient 调用上传/下载/查询/删除
/// </summary>
public class FileStorageApiClient : IFileStorageClient
{
    private readonly HttpClient _http;
    private readonly FileStorageSkyOptions _options;

    public FileStorageApiClient(HttpClient http, FileStorageSkyOptions options)
    {
        _http = http;
        _options = options;
    }

    private string UploadsBase => _options.UploadsPath.TrimEnd('/');
    private string FilesBase   => _options.FilesPath.TrimEnd('/');

    // ════════════════════════════════════════
    // 上传会话
    // ════════════════════════════════════════

    public async Task<InitiateUploadResponse> InitiateUploadAsync(
        InitiateUploadRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync($"{UploadsBase}/initiate", request, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Initiate 失败 ({(int)resp.StatusCode}): {body}");
        }
        return (await resp.Content.ReadFromJsonAsync<InitiateUploadResponse>(ct))!;
    }

    public async Task<UploadChunkResponse> UploadChunkAsync(
        Guid uploadId, int chunkIndex,
        Stream content, long contentLength,
        string? expectedSha256 = null, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Put,
            $"{UploadsBase}/{uploadId}/chunks/{chunkIndex}")
        {
            Content = new StreamContent(content)
        };

        req.Content.Headers.ContentType =
            new MediaTypeHeaderValue("application/octet-stream");
        req.Content.Headers.ContentLength = contentLength;

        if (!string.IsNullOrEmpty(expectedSha256))
            req.Headers.Add("X-Chunk-Sha256", expectedSha256);

        var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"UploadChunk 失败 ({(int)resp.StatusCode}): {body}");
        }
        return (await resp.Content.ReadFromJsonAsync<UploadChunkResponse>(ct))!;
    }

    public async Task<UploadStatusResponse> GetUploadStatusAsync(
        Guid uploadId, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"{UploadsBase}/{uploadId}/status", ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"GetStatus 失败 ({(int)resp.StatusCode}): {body}");
        }
        return (await resp.Content.ReadFromJsonAsync<UploadStatusResponse>(ct))!;
    }

    public async Task<CompleteUploadResponse> CompleteUploadAsync(
        Guid uploadId, CompleteUploadRequest? request = null,
        CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync(
            $"{UploadsBase}/{uploadId}/complete", request ?? new CompleteUploadRequest(), ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Complete 失败 ({(int)resp.StatusCode}): {body}");
        }
        return (await resp.Content.ReadFromJsonAsync<CompleteUploadResponse>(ct))!;
    }

    public async Task<bool> HeartbeatAsync(Guid uploadId, CancellationToken ct = default)
    {
        var resp = await _http.PostAsync($"{UploadsBase}/{uploadId}/heartbeat", null, ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> CancelUploadAsync(Guid uploadId, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"{UploadsBase}/{uploadId}", ct);
        return resp.IsSuccessStatusCode;
    }

    // ════════════════════════════════════════
    // 文件元数据 + 下载
    // ════════════════════════════════════════

    public async Task<List<FileMetadataDto>> ListFilesAsync(
        int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var resp = await _http.GetFromJsonAsync<List<FileMetadataDto>>(
            $"{FilesBase}?skip={skip}&take={take}", ct);
        return resp ?? new List<FileMetadataDto>();
    }

    public async Task<FileMetadataDto?> GetFileByIdAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"{FilesBase}/{id}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<FileMetadataDto>(ct);
    }

    public async Task<FileMetadataDto?> GetFileByDocIdAsync(
        string docId, int? version = null, CancellationToken ct = default)
    {
        var url = $"{FilesBase}/by-doc/{Uri.EscapeDataString(docId)}";
        if (version.HasValue) url += $"?version={version.Value}";
        var resp = await _http.GetAsync(url, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<FileMetadataDto>(ct);
    }

    public async Task<Stream?> DownloadFileAsync(
        Guid id, long? rangeStart = null, long? rangeEnd = null,
        CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, $"{FilesBase}/{id}/download");

        if (rangeStart.HasValue || rangeEnd.HasValue)
        {
            var start = rangeStart ?? 0;
            var endPart = rangeEnd.HasValue ? rangeEnd.Value.ToString() : "";
            req.Headers.Range = new RangeHeaderValue(start, string.IsNullOrEmpty(endPart) ? null : long.Parse(endPart));
        }

        var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStreamAsync(ct);
    }

    public async Task<bool> DeleteFileAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"{FilesBase}/{id}", ct);
        return resp.IsSuccessStatusCode;
    }
}
