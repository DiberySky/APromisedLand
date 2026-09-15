namespace FileStorageApi.Entities;

public class UploadSessionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Tenant { get; set; } = "default";
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long TotalSize { get; set; }
    public int ChunkSize { get; set; }
    public int TotalChunks { get; set; }

    /// <summary>pending / uploading / merging / completed / failed / expired</summary>
    public string Status { get; set; } = "pending";

    public string? ErrorMessage { get; set; }

    /// <summary>
    /// ★ 客户端会话指纹。用于"客户端丢失 uploadId 后重建会话"。
    /// 客户端推荐：SHA256(FileName + TotalSize + 文件首末 1 MB 内容) 的十六进制。
    /// 也可以直接传 Guid("N")——只要客户端能持久化它即可。
    /// 服务端按 (Tenant, Fingerprint) 在未完成会话中唯一。
    /// </summary>
    public string? Fingerprint { get; set; }

    public string? DocId { get; set; }
    public int? Version { get; set; }
    public string? ObjectKey { get; set; }
    public string? Sha256 { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public List<UploadChunkEntity> Chunks { get; set; } = new();
}