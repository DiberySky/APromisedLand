namespace FileStorageApi.Uploads;

/// <summary>完成上传的响应体（class 版本）。</summary>
public sealed class CompleteUploadResponse
{
    /// <summary>上传会话 ID。</summary>
    public Guid UploadId { get; init; }

    /// <summary>最终业务文档 ID。</summary>
    public string DocId { get; init; } = string.Empty;

    /// <summary>同一 (Tenant, DocId) 下的版本号，从 1 开始。</summary>
    public int Version { get; init; }

    /// <summary>SeaweedFS S3 对象键。</summary>
    public string ObjectKey { get; init; } = string.Empty;

    /// <summary>文件总字节数。</summary>
    public long Size { get; init; }

    /// <summary>文件 SHA256（十六进制，64 字符）。</summary>
    public string? Sha256 { get; init; }

    /// <summary>无参构造，供序列化器使用。</summary>
    public CompleteUploadResponse() { }

    /// <summary>便利构造。</summary>
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

    /// <summary>
    /// 从 UploadSessionEntity 构造响应（用于幂等返回路径）。
    /// 会话必须已完成，且 DocId / Version / ObjectKey 均已回填。
    /// </summary>
    public static CompleteUploadResponse FromSession(
        Entities.UploadSessionEntity session)
    {
        if (session.Status != "completed")
            throw new InvalidOperationException(
                $"会话状态为 '{session.Status}'，无法构造完成响应。");

        if (string.IsNullOrEmpty(session.DocId) ||
            session.Version is null ||
            string.IsNullOrEmpty(session.ObjectKey))
            throw new InvalidOperationException(
                "会话缺少 DocId / Version / ObjectKey，无法构造完成响应。");

        return new CompleteUploadResponse(
            session.Id,
            session.DocId,
            session.Version.Value,
            session.ObjectKey,
            session.TotalSize,
            session.Sha256);
    }
}