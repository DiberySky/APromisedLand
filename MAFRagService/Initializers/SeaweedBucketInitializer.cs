using Amazon.S3;
using Amazon.S3.Model;
using MAFRagService.Startup.Configuration;
using Microsoft.Extensions.Options;

namespace MAFRagService.Initializers;

public class SeaweedBucketInitializer
{
    private readonly IAmazonS3 _s3Client;
    private readonly SeaweedFsOptions _options;
    private readonly ILogger<SeaweedBucketInitializer> _logger;

    public SeaweedBucketInitializer(
        IAmazonS3 s3Client,
        IOptions<SeaweedFsOptions> options,
        ILogger<SeaweedBucketInitializer> logger)
    {
        _s3Client = s3Client;
        _options  = options.Value;
        _logger   = logger;
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        var bucketName = _options.Bucket;

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
            _logger.LogInformation("Bucket '{Bucket}' already exists", bucketName);
        }
    }
}