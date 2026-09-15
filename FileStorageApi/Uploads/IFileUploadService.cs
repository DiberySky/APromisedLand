namespace FileStorageApi.Uploads;

public interface IFileUploadService
{
    /// <summary>
    /// 初始化上传会话。若 request.Fingerprint 命中既有活跃会话，则返回该会话
    /// （Resumed=true），客户端可续传。
    /// </summary>
    Task<InitiateUploadResponse> InitiateAsync(
        InitiateUploadRequest request, CancellationToken ct);

    /// <summary>
    /// 上传单个分块。expectedSha256 非空时与计算值做大小写不敏感比对，
    /// 不一致抛 UploadValidationException（HTTP 422）。
    /// </summary>
    Task<UploadChunkResponse> UploadChunkAsync(
        Guid uploadId, int chunkIndex, Stream data, long contentLength,
        string? expectedSha256, CancellationToken ct);

    Task<UploadStatusResponse> GetStatusAsync(Guid uploadId, CancellationToken ct);

    /// <summary>
    /// 完成上传。允许 failed 状态且分块完整时重试（P0-1）。
    /// </summary>
    Task<CompleteUploadResponse> CompleteAsync(
        Guid uploadId, CompleteUploadRequest request, CancellationToken ct);

    /// <summary>
    /// ★ P2-2：会话续期（heartbeat）。延长 ExpiresAt 至 now + 24h。
    /// 仅对 pending / uploading 状态有效。
    /// </summary>
    Task<bool> RenewAsync(Guid uploadId, CancellationToken ct);

    Task<bool> CancelAsync(Guid uploadId, CancellationToken ct);

    Task<int> CleanupExpiredAsync(CancellationToken ct);
}