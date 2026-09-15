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
}