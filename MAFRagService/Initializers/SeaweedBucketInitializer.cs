using Amazon.S3;
using Amazon.S3.Model;

﻿namespace MAFRagService.Initializers;

public class SeaweedBucketInitializer
{
    private readonly IAmazonS3 _s3Client;
    private readonly IConfiguration _config;
    private readonly ILogger<SeaweedBucketInitializer> _logger;

    public SeaweedBucketInitializer(IAmazonS3 s3Client, IConfiguration config, ILogger<SeaweedBucketInitializer> logger)
    {
        _s3Client = s3Client;
        _config = config;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        var bucketName = _config["SeaweedFS:Bucket"] ?? "rag-documents";
        try
        {
            var request = new PutBucketRequest { BucketName = bucketName };
            await _s3Client.PutBucketAsync(request, ct);
            _logger.LogInformation("Bucket '{Bucket}' created", bucketName);
        }
        catch (AmazonS3Exception ex) when (ex.Message.Contains("BucketAlreadyOwnedByYou") || ex.Message.Contains("BucketAlreadyExists"))
        {
            _logger.LogInformation("Bucket '{Bucket}' already exists", bucketName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Bucket creation ignored: {Message}", ex.Message);
        }
    }
}
