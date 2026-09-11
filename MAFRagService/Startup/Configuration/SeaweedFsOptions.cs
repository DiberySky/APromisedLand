using System.ComponentModel.DataAnnotations;

namespace MAFRagService.Startup.Configuration;

public sealed class SeaweedFsOptions
{
    public const string SectionName = "SeaweedFS";

    [Required] public string AccessKey { get; set; } = "dummy";
    [Required] public string SecretKey { get; set; } = "dummy";
    [Required, MinLength(3)] public string Region { get; set; } = "us-east-1";

    /// <summary>默认 bucket 名。缺失时回退到 "rag-documents"。</summary>
    [MinLength(3)]
    public string Bucket { get; set; } = "rag-documents";

    /// <summary>
    /// 返回给客户端的对外网关地址（反向代理 / CDN / 固定域名）。
    /// 缺失时：
    ///   · Development 环境回退到 http://localhost:8333（AppHost 固定端口）
    ///   · 其他环境抛异常，拒绝把内网容器地址泄露给客户端
    /// </summary>
    public string? PublicUrl { get; set; }
}