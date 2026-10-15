using FileStorageApi.Entities;
using FileStorageApi.Models;

/// <summary>
/// 下载结果：包含内容流、元数据、以及 Range 响应的响应头信息。
/// Controller 需要后者来正确设置 206 响应头。
/// </summary>
public sealed record DownloadResult(
    Stream Content,
    FileMetadataDto Metadata,
    long? ContentLength,
    long? TotalLength,
    string? ContentRange);

public interface IFileMetadataService
{
    Task<IReadOnlyList<FileMetadataDto>> ListAsync(
        int skip, int take, CancellationToken ct);

    Task<FileMetadataDto?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<FileMetadataDto?> GetByDocIdAsync(
        string docId, int? version, CancellationToken ct);

    /// <summary>
    /// 下载（支持 Range 断点续传）。
    /// Range 越界时抛 <see cref="FileStorageApi.Storage.RangeNotSatisfiableException"/>。
    /// 对象不存在或不属于当前租户返回 null。
    /// </summary>
    Task<DownloadResult?> DownloadAsync(
        Guid id, long? rangeStart, long? rangeEnd, CancellationToken ct);

    Task<bool> DeleteAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// 按 docId + 可选 version 删除。EAV 级联删除时调用——
    /// EAV 的 value_file_meta 里存了 docId + version，不需要先查 metadata Id。
    /// version 为 null 时删该 docId 的最新版本。
    /// </summary>
    Task<bool> DeleteByDocIdAsync(string docId, int? version, CancellationToken ct);

    /// <summary>
    /// 列出所有 status=delete_pending 且 UpdatedAt 早于 cutoff 的文档元数据。
    /// 供 DeletePendingRetryService 后台重试真正的 S3 删除。
    /// </summary>
    Task<List<DocumentMetadataEntity>> ListDeletePendingAsync(
        DateTimeOffset cutoff, CancellationToken ct);

    /// <summary>
    /// 列出所有文档的 docId。
    /// 供 TreeGraph.Api 的 OrphanedFileCleanupService 做差集对比（EAV ↔ FileStorageApi）。
    /// </summary>
    Task<List<string>> GetAllDocIdsAsync(CancellationToken ct);
}