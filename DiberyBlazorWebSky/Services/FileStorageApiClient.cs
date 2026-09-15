using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using DiberyBlazorWebSky.Models;

namespace DiberyBlazorWebSky.Services;

/// <summary>
/// FileStorageApi 的 HttpClient 包装。
///
/// 与后端端点对应关系：
///   GET    /files                          → ListAsync
///   GET    /files/{id}                     → GetByIdAsync
///   GET    /files/{id}/download            → DownloadStreamAsync
///   DELETE /files/{id}                     → DeleteAsync
///   POST   /uploads/initiate               → InitiateAsync（内部）
///   PUT    /uploads/{id}/chunks/{index}    → UploadChunkAsync（内部）
///   GET    /uploads/{id}/status            → GetUploadStatusAsync
///   POST   /uploads/{id}/complete          → CompleteAsync（内部）
///   DELETE /uploads/{id}                   → CancelAsync（内部）
///
/// 对上层提供两个高层方法：
///   - UploadFileAsync：整个文件的 initiate → 逐块 PUT → complete
///   - DownloadStreamAsync：返回内容流，调用方负责 Dispose
/// </summary>
public sealed class FileStorageApiClient(
    HttpClient http,
    ILogger<FileStorageApiClient> logger)
{
    private const int  DefaultChunkSize = 4 * 1024 * 1024;
    private const long MaxFileSize      = 2L * 1024 * 1024 * 1024;

    // ═══════════════════════════════════════════════════════
    // 元数据
    // ═══════════════════════════════════════════════════════

    public async Task<IReadOnlyList<FileMetadataDto>> ListAsync(
        int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var url = $"/files?skip={skip}&take={take}";
        var response = await http.GetAsync(url, ct);
        await EnsureSuccessAsync(response, ct);

        return await response.Content
            .ReadFromJsonAsync<List<FileMetadataDto>>(ct) ?? [];
    }

    public async Task<FileMetadataDto?> GetByIdAsync(
        Guid id, CancellationToken ct = default)
    {
        var response = await http.GetAsync($"/files/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, ct);

        return await response.Content.ReadFromJsonAsync<FileMetadataDto>(ct);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"/files/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        await EnsureSuccessAsync(response, ct);
        return true;
    }

    /// <summary>
    /// 流式下载。返回的 Stream Dispose 时，对应的 HttpResponseMessage 也会释放。
    /// 资源不存在返回 null。
    /// </summary>
    public async Task<Stream?> DownloadStreamAsync(
        Guid id, CancellationToken ct = default)
    {
        var response = await http.GetAsync(
            $"/files/{id}/download",
            HttpCompletionOption.ResponseHeadersRead,
            ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            response.Dispose();
            return null;
        }

        await EnsureSuccessAsync(response, ct);

        // 用包装流把 response 生命周期绑到返回的流上
        var stream = await response.Content.ReadAsStreamAsync(ct);
        return new ResponseStream(response, stream);
    }

    // ═══════════════════════════════════════════════════════
    // 高层上传：initiate → 逐块 PUT → complete
    // ═══════════════════════════════════════════════════════

    public async Task<CompleteUploadResponse> UploadFileAsync(
        string fileName,
        string contentType,
        Stream fileStream,
        long totalSize,
        string? docId = null,
        IProgress<(long uploaded, long total)>? progress = null,
        CancellationToken ct = default)
    {
        if (totalSize <= 0 || totalSize > MaxFileSize)
            throw new ArgumentOutOfRangeException(
                nameof(totalSize),
                $"文件大小必须在 1 至 {MaxFileSize} 字节之间。");

        // ★ 与后端 P0-2 呼应：Fingerprint 用于会话恢复
        //   SHA256(FileName:TotalSize) 的前 32 个十六进制字符
        var fingerprint = ComputeFingerprint(fileName, totalSize);

        // ── 1. initiate ──
        var initResponse = await http.PostAsJsonAsync(
            "/uploads/initiate",
            new InitiateUploadRequest
            {
                FileName    = fileName,
                ContentType = string.IsNullOrWhiteSpace(contentType)
                                ? "application/octet-stream"
                                : contentType,
                TotalSize   = totalSize,
                ChunkSize   = DefaultChunkSize,
                DocId       = docId,
                Fingerprint = fingerprint,
            },
            ct);
        await EnsureSuccessAsync(initResponse, ct);

        var init = await initResponse.Content
            .ReadFromJsonAsync<InitiateUploadResponse>(ct)
            ?? throw new InvalidOperationException("initiate 响应为空。");

        logger.LogInformation(
            "上传会话 {UploadId}：ChunkSize={ChunkSize}, TotalChunks={TotalChunks}, Resumed={Resumed}",
            init.UploadId, init.ChunkSize, init.TotalChunks, init.Resumed);

        try
        {
            // ── 2. 逐块 PUT ──
            var buffer = new byte[init.ChunkSize];
            long uploaded = 0;

            for (int i = 0; i < init.TotalChunks; i++)
            {
                ct.ThrowIfCancellationRequested();

                var expected = (int)Math.Min(init.ChunkSize, totalSize - uploaded);
                var read = 0;
                while (read < expected)
                {
                    var n = await fileStream.ReadAsync(
                        buffer.AsMemory(read, expected - read), ct);
                    if (n == 0) break;
                    read += n;
                }
                if (read != expected)
                    throw new IOException(
                        $"读取第 {i} 块不完整：期望 {expected}，实际 {read}。");

                using var chunkContent = new ByteArrayContent(buffer, 0, read);
                chunkContent.Headers.ContentType =
                    new MediaTypeHeaderValue("application/octet-stream");
                chunkContent.Headers.ContentLength = read;

                // ★ 与后端 P1-2 呼应：客户端分块 SHA256（可选，防御性）
                var chunkSha = Convert.ToHexString(
                    SHA256.HashData(buffer.AsSpan(0, read)));
                chunkContent.Headers.Add("X-Chunk-Sha256", chunkSha);

                var chunkResponse = await http.PutAsync(
                    $"/uploads/{init.UploadId}/chunks/{i}",
                    chunkContent, ct);
                await EnsureSuccessAsync(chunkResponse, ct);

                uploaded += read;
                progress?.Report((uploaded, totalSize));
            }

            // ── 3. complete ──
            var completeResponse = await http.PostAsJsonAsync(
                $"/uploads/{init.UploadId}/complete",
                new CompleteUploadRequest(),
                ct);
            await EnsureSuccessAsync(completeResponse, ct);

            var result = await completeResponse.Content
                .ReadFromJsonAsync<CompleteUploadResponse>(ct)
                ?? throw new InvalidOperationException("complete 响应为空。");

            logger.LogInformation(
                "上传完成 {UploadId}：docId={DocId}, version={Version}, size={Size}",
                result.UploadId, result.DocId, result.Version, result.Size);

            return result;
        }
        catch
        {
            // 任何异常（含取消）尝试清理会话；失败不掩盖原异常
            try
            {
                await http.DeleteAsync(
                    $"/uploads/{init.UploadId}", CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "取消上传会话 {UploadId} 失败。", init.UploadId);
            }
            throw;
        }
    }

    // ═══════════════════════════════════════════════════════
    // 会话状态查询（可用于断点续传 UI）
    // ═══════════════════════════════════════════════════════

    public async Task<UploadStatusResponse?> GetUploadStatusAsync(
        Guid uploadId, CancellationToken ct = default)
    {
        var response = await http.GetAsync(
            $"/uploads/{uploadId}/status", ct);

        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, ct);

        return await response.Content
            .ReadFromJsonAsync<UploadStatusResponse>(ct);
    }

    // ═══════════════════════════════════════════════════════
    // 私有辅助
    // ═══════════════════════════════════════════════════════

    private static string ComputeFingerprint(string fileName, long totalSize)
    {
        var input = $"{fileName}:{totalSize}";
        var hash  = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash)[..32];
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync(ct);

        // ★ 与后端异常映射对齐：
        //   404 → UploadNotFoundException / 元数据不存在
        //   409 → UploadConflictException
        //   410 → UploadGoneException（会话过期）
        //   422 → UploadValidationException
        var message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "FileStorageApi 返回 401（未授权）",
            HttpStatusCode.Forbidden    => "FileStorageApi 返回 403（禁止访问）",
            HttpStatusCode.NotFound     => "资源不存在（404）",
            HttpStatusCode.Conflict     => "会话状态冲突（409），请刷新后重试",
            HttpStatusCode.Gone         => "上传会话已过期（410），请重新 initiate",
            (HttpStatusCode)422          => "请求参数不合法（422）",
            _ => $"FileStorageApi 返回 {(int)response.StatusCode} {response.ReasonPhrase}"
        };

        if (!string.IsNullOrWhiteSpace(body))
            message += $"：{body}";

        throw new HttpRequestException(message, null, response.StatusCode);
    }

    /// <summary>
    /// 把 HttpResponseMessage 的生命周期绑定到内部 Stream：
    /// Dispose/DisposeAsync 时一并释放 response。
    /// </summary>
    private sealed class ResponseStream(HttpResponseMessage response, Stream inner)
        : Stream
    {
        public override bool CanRead  => inner.CanRead;
        public override bool CanSeek  => false;
        public override bool CanWrite => false;
        public override long Length   => inner.Length;
        public override long Position
        {
            get => inner.Position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
            => inner.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer, CancellationToken ct = default)
            => inner.ReadAsync(buffer, ct);

        public override void Flush() { }
        public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException();
        public override void SetLength(long v) => throw new NotSupportedException();
        public override void Write(byte[] b, int o, int c) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) { inner.Dispose(); response.Dispose(); }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync();
            response.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}