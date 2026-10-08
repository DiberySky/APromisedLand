namespace TreeGraph.Shared.FileStorageSky.Contracts;

/// <summary>
/// 完成上传的响应体。
/// 注意：原 FromSession 工厂方法已移至 UploadResponseFactory（依赖 UploadSessionEntity）。
/// </summary>
public sealed class CompleteUploadResponse
{
    /// <summary>上传会话 ID。</summary>
    public Guid UploadId { get; init; }

    /// <summary>最终业务文档 ID。</summary>
    public string DocId { get; init; } = string.Empty;

    /// <summary>同一 (Tenant, DocId) 下的版本号，从 1 开始。</summary>
    public int Version { get; init; }

    /// <summary>对象存储键。</summary>
    public string ObjectKey { get; init; } = string.Empty;

    /// <summary>文件总字节数。</summary>
    public long Size { get; init; }

    /// <summary>文件 SHA256（十六进制，64 字符）。</summary>
    public string? Sha256 { get; init; }

    public CompleteUploadResponse() { }

    public CompleteUploadResponse(
        Guid uploadId, string docId, int version,
        string objectKey, long size, string? sha256)
    {
        UploadId  = uploadId;
        DocId     = docId;
        Version   = version;
        ObjectKey = objectKey;
        Size      = size;
        Sha256    = sha256;
    }
}
