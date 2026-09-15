namespace FileStorageApi.Storage;

public sealed record ObjectStoragePutResult(string Key, long Size, string? ETag);

/// <summary>
/// GetAsync 的返回：包含流 + 响应元数据。
/// Controller 需要 ContentLength / TotalLength / ContentRange 来正确设置
/// HTTP 206 响应头（Content-Range / Content-Length）。
/// </summary>
public sealed record ObjectStorageGetResult(
    Stream Content,
    long? ContentLength,     // 本次响应的字节数（partial 时为 range 长度）
    long? TotalLength,       // 对象总长度
    string? ContentRange);   // S3 返回的 "bytes N-M/Total"（若有）

public interface IObjectStorage
{
    Task<ObjectStoragePutResult> PutAsync(
        string key, Stream content, long? contentLength,
        string contentType, CancellationToken ct = default);

    /// <summary>
    /// 打开对象流。start/end 为闭区间；两者为 null 表示全量下载。
    /// Range 越界时抛 <see cref="RangeNotSatisfiableException"/>。
    /// </summary>
    Task<ObjectStorageGetResult> GetAsync(
        string key, long? start = null, long? end = null,
        CancellationToken ct = default);

    Task<bool> DeleteAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}