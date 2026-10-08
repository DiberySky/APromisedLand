using TreeGraph.Shared.FileStorageSky.Contracts;

namespace TreeGraph.Blazor.Shared.FileStorageSky.Services;

/// <summary>
/// 文件存储 API 客户端。
/// 对应 TreeGraph.FileStorageApi 的 UploadsController + FilesController。
/// </summary>
public interface IFileStorageClient
{
    // ════════════════════════════════════════
    // 上传会话（Uploads）
    // ════════════════════════════════════════

    /// <summary>
    /// 发起分块上传。若 request.Fingerprint 命中既有活跃会话，返回该会话（Resumed=true）。
    /// </summary>
    Task<InitiateUploadResponse> InitiateUploadAsync(
        InitiateUploadRequest request, CancellationToken ct = default);

    /// <summary>
    /// 上传单个分块（octet-stream body）。
    /// expectedSha256 非空时与服务端计算值大小写不敏感比对，不一致抛 HttpRequestException。
    /// </summary>
    Task<UploadChunkResponse> UploadChunkAsync(
        Guid uploadId, int chunkIndex,
        Stream content, long contentLength,
        string? expectedSha256 = null, CancellationToken ct = default);

    /// <summary>查询上传会话状态（含已收/缺失分块列表）。</summary>
    Task<UploadStatusResponse> GetUploadStatusAsync(
        Guid uploadId, CancellationToken ct = default);

    /// <summary>
    /// 完成上传，触发服务端合并 + 落盘 + 写元数据。
    /// 允许 failed 状态且分块完整时重试。
    /// </summary>
    Task<CompleteUploadResponse> CompleteUploadAsync(
        Guid uploadId, CompleteUploadRequest? request = null,
        CancellationToken ct = default);

    /// <summary>会话续期（heartbeat），返回是否成功续期。</summary>
    Task<bool> HeartbeatAsync(Guid uploadId, CancellationToken ct = default);

    /// <summary>取消上传会话（DELETE Uploads/{id}），返回是否成功。</summary>
    Task<bool> CancelUploadAsync(Guid uploadId, CancellationToken ct = default);

    // ════════════════════════════════════════
    // 文件元数据 + 下载（Files）
    // ════════════════════════════════════════

    /// <summary>列出文件元数据（分页）。</summary>
    Task<List<FileMetadataDto>> ListFilesAsync(
        int skip = 0, int take = 50, CancellationToken ct = default);

    /// <summary>按主键 Id 查询文件元数据。不存在返回 null。</summary>
    Task<FileMetadataDto?> GetFileByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>按业务 DocId + 可选版本查询文件元数据。不存在返回 null。</summary>
    Task<FileMetadataDto?> GetFileByDocIdAsync(
        string docId, int? version = null, CancellationToken ct = default);

    /// <summary>
    /// 下载文件流。支持 Range 断点续传（rangeStart/rangeEnd 可选）。
    /// 文件不存在返回 null。
    /// </summary>
    Task<Stream?> DownloadFileAsync(
        Guid id, long? rangeStart = null, long? rangeEnd = null,
        CancellationToken ct = default);

    /// <summary>删除文件（同时清理对象存储）。返回是否成功。</summary>
    Task<bool> DeleteFileAsync(Guid id, CancellationToken ct = default);
}
