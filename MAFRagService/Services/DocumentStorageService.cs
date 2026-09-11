using Amazon.S3;
using Amazon.S3.Model;
using MAFRagService.Models;
using MAFRagService.Startup.Configuration;
using Microsoft.Extensions.Options;

namespace MAFRagService.Services;

public class DocumentStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly SeaweedFsOptions _options;
    private readonly IHostEnvironment _env;
    private readonly ILogger<DocumentStorageService> _logger;

    public DocumentStorageService(
        IAmazonS3 s3Client,
        IOptions<SeaweedFsOptions> options,
        IHostEnvironment env,
        ILogger<DocumentStorageService> logger)
    {
        _s3Client = s3Client;
        _options  = options.Value;
        _env      = env;
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
        var bucket = _options.Bucket;
        var key    = $"{tenant}/{docId}/{version}/{fileName}";

        var request = new PutObjectRequest
        {
            BucketName      = bucket,
            Key             = key,
            InputStream     = fileStream,
            AutoCloseStream = false,
            ContentType     = "application/octet-stream"
        };

        var response = await _s3Client.PutObjectAsync(request, ct);
        _logger.LogInformation(
            "Uploaded {Key} to bucket {Bucket}, ETag: {ETag}",
            key, bucket, response.ETag);

        var publicUrl = ResolvePublicUrl();
        var blobUri = $"{publicUrl.TrimEnd('/')}/{bucket}/{key}";
        return new DocumentBlobInfo { BlobUri = blobUri, BlobName = key };
    }

    public async Task<Stream> DownloadAsync(string blobName, CancellationToken ct)
    {
        using var resp = await _s3Client.GetObjectAsync(new GetObjectRequest
        {
            BucketName = _options.Bucket,
            Key        = blobName
        }, ct);

        var ms = new MemoryStream();
        await resp.ResponseStream.CopyToAsync(ms, ct);
        ms.Position = 0;
        return ms;
    }

    public async Task DeleteAsync(string blobName, CancellationToken ct)
    {
        await _s3Client.DeleteObjectAsync(_options.Bucket, blobName, ct);
        _logger.LogInformation("Deleted {Key} from bucket {Bucket}", blobName, _options.Bucket);
    }

    // ------------------------------------------------------------
    // PublicUrl 解析：显式配置 → Development 回退 → 抛异常
    // ------------------------------------------------------------
    private string ResolvePublicUrl()
    {
        // 1) 显式配置优先
        if (!string.IsNullOrWhiteSpace(_options.PublicUrl))
            return _options.PublicUrl.TrimEnd('/');

        // 2) Development 回退到 AppHost 固定的宿主机端口
        if (_env.IsDevelopment())
        {
            const string devFallback = "http://localhost:8333";
            _logger.LogWarning(
                "SeaweedFS:PublicUrl 未配置，Development 环境回退到 {Fallback}。" +
                "生产环境必须显式配置对外网关地址。",
                devFallback);
            return devFallback;
        }

        // 3) 其他环境：拒绝把内网容器地址泄露给客户端
        throw new InvalidOperationException(
            "缺少 SeaweedFS:PublicUrl。生产环境必须显式配置对外网关地址（反向代理 / CDN / 固定域名）。" +
            "不要依赖 IAmazonS3.Config.ServiceURL —— 那是服务端内网地址，客户端不可达。");
    }
}