using System.ComponentModel.DataAnnotations;

namespace FileStorageApi.Uploads;

public sealed class InitiateUploadRequest
{
    [Required]
    [StringLength(512, MinimumLength = 1)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [StringLength(256, MinimumLength = 1)]
    public string ContentType { get; set; } = "application/octet-stream";

    [Range(1, 2L * 1024 * 1024 * 1024)]
    public long TotalSize { get; set; }

    [Range(256 * 1024, 16 * 1024 * 1024)]
    public int? ChunkSize { get; set; }

    [StringLength(128)]
    public string? DocId { get; set; }

    /// <summary>
    /// ★ P0-2：客户端会话指纹。若提供且服务端存在 (Tenant, Fingerprint) 的活跃会话，
    /// 直接返回既有会话（Resumed=true），客户端可继续上传缺失分块，无需从头开始。
    ///
    /// 推荐算法：
    ///   SHA256(FileName + ":" + TotalSize + ":" + 前1MB + ":" + 后1MB) → hex
    /// 或直接使用客户端持久化的 Guid("N")。
    /// </summary>
    [StringLength(128)]
    public string? Fingerprint { get; set; }
}