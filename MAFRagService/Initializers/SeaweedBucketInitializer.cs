using Amazon.S3;
using Amazon.S3.Model;

namespace MAFRagService.Initializers;

public class SeaweedBucketInitializer
{
    /// <summary>
    /// 默认 bucket 名。生产环境可通过配置 <c>SeaweedFS:Bucket</c> 覆盖。
    /// </summary>
    private const string DefaultBucket = "rag-documents";

    private readonly IAmazonS3 _s3Client;
    private readonly IConfiguration _config;
    private readonly ILogger<SeaweedBucketInitializer> _logger;

    public SeaweedBucketInitializer(
        IAmazonS3 s3Client,
        IConfiguration config,
        ILogger<SeaweedBucketInitializer> logger)
    {
        _s3Client = s3Client;
        _config = config;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        var bucketName = _config["SeaweedFS:Bucket"] ?? DefaultBucket;

        // 打印真实服务地址，与 Weaviate 一致，便于排查
        _logger.LogInformation(
            "SeaweedFS endpoint = {Endpoint}, bucket = {Bucket}",
            _s3Client.Config?.ServiceURL ?? "<unset>",
            bucketName);

        try
        {
            await _s3Client.PutBucketAsync(
                new PutBucketRequest { BucketName = bucketName }, ct);

            _logger.LogInformation("Bucket '{Bucket}' created", bucketName);
        }
        catch (AmazonS3Exception ex) when (
            ex.ErrorCode == "BucketAlreadyOwnedByYou" ||
            ex.ErrorCode == "BucketAlreadyExists")
        {
            // ✅ 用 ErrorCode 精确匹配，不再用 ex.Message.Contains(...)
            _logger.LogInformation("Bucket '{Bucket}' already exists", bucketName);
        }
        // ✅ 其余 AmazonS3Exception / 网络异常一律向上抛，
        //    让 Program.cs 的 try/catch 在启动时立刻暴露问题，而不是推迟到第一次上传
    }
}