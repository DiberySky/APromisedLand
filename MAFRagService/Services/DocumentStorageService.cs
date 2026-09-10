using Amazon.S3;
using Amazon.S3.Model;
using MAFRagService.Models;

namespace MAFRagService.Services;

public class DocumentStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly IConfiguration _config;
    private readonly ILogger<DocumentStorageService> _logger;

    public DocumentStorageService(IAmazonS3 s3Client, IConfiguration config, ILogger<DocumentStorageService> logger)
    {
        _s3Client = s3Client;
        _config = config;
        _logger = logger;
    }

    public async Task<DocumentBlobInfo> UploadAsync(Stream fileStream, string docId, string version, string fileName, string tenant, CancellationToken ct)
    {
        var bucket = _config["SeaweedFS:Bucket"] ?? "rag-documents";
        var key = $"{tenant}/{docId}/{version}/{fileName}";

        var request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = fileStream,
            AutoCloseStream = true,
            ContentType = "application/octet-stream"
        };
        var response = await _s3Client.PutObjectAsync(request, ct);
        _logger.LogInformation("Uploaded {Key} to bucket {Bucket}, ETag: {ETag}", key, bucket, response.ETag);

        var blobUri = $"{_config["SeaweedFS:PublicUrl"] ?? "http://localhost:8333"}/{bucket}/{key}";
        return new DocumentBlobInfo { BlobUri = blobUri, BlobName = key };
    }
}
