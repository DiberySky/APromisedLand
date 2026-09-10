using Amazon.S3;
using Amazon.S3.Model;
using MAFRagService.Models;

namespace MAFRagService.Services;

public class DocumentStorageService
{
    private const string DefaultBucket = "rag-documents";

    private readonly IAmazonS3 _s3Client;
    private readonly IConfiguration _config;
    private readonly ILogger<DocumentStorageService> _logger;

    public DocumentStorageService(
        IAmazonS3 s3Client,
        IConfiguration config,
        ILogger<DocumentStorageService> logger)
    {
        _s3Client = s3Client;
        _config   = config;
        _logger   = logger;
    }

    public async Task<DocumentBlobInfo> UploadAsync(
        Stream fileStream,
        string docId,
        string version,
        string fileName,
        string tenant,
        CancellationToken ct)
    {
        var bucket = _config["SeaweedFS:Bucket"] ?? DefaultBucket;
        var key    = $"{tenant}/{docId}/{version}/{fileName}";

        var request = new PutObjectRequest
        {
            BucketName      = bucket,
            Key             = key,
            InputStream     = fileStream,
            // ★ 改为 false：流由调用方（ASP.NET Core）管理，避免 SDK 提前 dispose
            AutoCloseStream = false,
            ContentType     = "application/octet-stream"
        };

        var response = await _s3Client.PutObjectAsync(request, ct);
        _logger.LogInformation(
            "Uploaded {Key} to bucket {Bucket}, ETag: {ETag}",
            key, bucket, response.ETag);

        // ============================================================
        // ★ BlobUri 是返回给客户端的「对外地址」，不是服务端使用的
        //   ServiceURL（容器内 / 内网地址）。
        //
        //   优先级：
        //     1) SeaweedFS:PublicUrl       —— 显式配置的公共网关地址
        //     2) IAmazonS3.Config.ServiceURL —— AppHost 注入的地址
        //                                      （dev 场景下对浏览器通常可达）
        //   都没有 → 抛异常，绝不 fallback 到硬编码 localhost:8333。
        //
        //   与 Weaviate 的教训完全一致：
        //   暗 fallback 到 localhost 只会把"配置没注入"伪装成"文件访问 404"。
        // ============================================================
        var publicUrl = _config["SeaweedFS:PublicUrl"];
        if (string.IsNullOrWhiteSpace(publicUrl))
            publicUrl = _s3Client.Config?.ServiceURL;

        if (string.IsNullOrWhiteSpace(publicUrl))
            throw new InvalidOperationException(
                "无法确定 SeaweedFS 对外地址。请配置 SeaweedFS:PublicUrl，" +
                "或确保 IAmazonS3.Config.ServiceURL 已由 AppHost 注入。");

        var blobUri = $"{publicUrl.TrimEnd('/')}/{bucket}/{key}";
        return new DocumentBlobInfo { BlobUri = blobUri, BlobName = key };
    }
}