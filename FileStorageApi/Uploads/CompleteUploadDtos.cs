using System.ComponentModel.DataAnnotations;

namespace FileStorageApi.Uploads;

/// <summary>
/// 完成上传的请求体。所有字段可选。
///
/// 优先级：
///   DocId       : request.DocId → session.DocId → 自动生成 GUID("N")
///   TagsJson    : request.TagsJson（为空则不写标签）
///   MetadataJson: request.MetadataJson（为空则不写扩展元数据）
///   Sha256      : request.Sha256（为空则使用服务端合并计算结果）
///
/// ★ 已移除 IdempotencyKey：原字段未实现，为避免 API 契约与实现不一致而删除。
///    幂等性由"会话已 completed 直接返回既有结果"保证。
/// </summary>
public sealed class CompleteUploadRequest
{
    [StringLength(128)]
    public string? DocId { get; set; }

    [StringLength(4096)]
    public string? TagsJson { get; set; }

    [StringLength(16384)]
    public string? MetadataJson { get; set; }

    [StringLength(64, MinimumLength = 64)]
    [RegularExpression("^[0-9a-fA-F]{64}$",
        ErrorMessage = "Sha256 必须是 64 位十六进制字符串。")]
    public string? Sha256 { get; set; }
}