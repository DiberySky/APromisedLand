using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.Extensions.Options;
using System.Diagnostics;                    // ★ 新增
using System.Globalization;

namespace FileStorageApi.Storage;

public sealed class S3ObjectStorage(
    IAmazonS3 client,
    IOptions<ObjectStorageOptions> options,
    ILogger<S3ObjectStorage> logger) : IObjectStorage
{
    private const long MultipartThreshold = 16 * 1024 * 1024;
    private const long PartSize           = 8 * 1024 * 1024;

    private readonly string _bucket = options.Value.Bucket;
    private volatile bool _bucketChecked;

    [DebuggerDisableUserUnhandledExceptions]                    // ★
    public async Task<ObjectStoragePutResult> PutAsync(
        string key, Stream content, long? contentLength,
        string contentType, CancellationToken ct = default)
    {
        await EnsureBucketAsync(ct);

        if (contentLength is > MultipartThreshold)
            return await PutMultipartAsync(key, content, contentLength.Value, contentType, ct);

        var request = new PutObjectRequest
        {
            BucketName      = _bucket,
            Key             = key,
            InputStream     = content,
            ContentType     = contentType,
            AutoCloseStream = false,
        };

        if (contentLength is > 0)
            request.Headers.ContentLength = contentLength.Value;

        var response = await client.PutObjectAsync(request, ct);
        logger.LogInformation("上传对象成功（单次）：{Key}（{Length} 字节）",
            key, contentLength?.ToString() ?? "unknown");

        return new ObjectStoragePutResult(key, contentLength ?? 0, response.ETag);
    }

    [DebuggerDisableUserUnhandledExceptions]                    // ★
    private async Task<ObjectStoragePutResult> PutMultipartAsync(
        string key, Stream content, long contentLength,
        string contentType, CancellationToken ct)
    {
        var init = await client.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest
        {
            BucketName  = _bucket,
            Key         = key,
            ContentType = contentType,
        }, ct);

        var uploadId = init.UploadId;
        var parts = new List<PartETag>();

        try
        {
            var buffer = new byte[PartSize];
            long remaining = contentLength;
            int partNumber = 1;

            while (remaining > 0)
            {
                var toRead = (int)Math.Min(PartSize, remaining);
                var read = 0;
                while (read < toRead)
                {
                    var n = await content.ReadAsync(
                        buffer.AsMemory(read, toRead - read), ct);
                    if (n == 0) break;
                    read += n;
                }
                if (read != toRead)
                    throw new IOException(
                        $"读取分片 {partNumber} 不完整：期望 {toRead}，实际 {read}。");

                using var ms = new MemoryStream(buffer, 0, toRead, writable: false);

                var partResp = await client.UploadPartAsync(new UploadPartRequest
                {
                    BucketName  = _bucket,
                    Key         = key,
                    UploadId    = uploadId,
                    PartNumber  = partNumber,
                    PartSize    = toRead,
                    InputStream = ms,
                    IsLastPart  = (remaining - toRead) <= 0,
                }, ct);

                parts.Add(new PartETag(partNumber, partResp.ETag));
                remaining -= toRead;
                partNumber++;
            }

            var complete = await client.CompleteMultipartUploadAsync(
                new CompleteMultipartUploadRequest
                {
                    BucketName = _bucket,
                    Key        = key,
                    UploadId   = uploadId,
                    PartETags  = parts,
                }, ct);

            logger.LogInformation(
                "上传对象成功（Multipart）：{Key}（{Length} 字节，{Parts} 片）",
                key, contentLength, parts.Count);

            return new ObjectStoragePutResult(key, contentLength, complete.ETag);
        }
        catch
        {
            try
            {
                await client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest
                {
                    BucketName = _bucket,
                    Key        = key,
                    UploadId   = uploadId,
                }, CancellationToken.None);
            }
            catch (Exception abortEx)
            {
                logger.LogWarning(abortEx,
                    "Abort multipart 失败（可能残留分片）：key={Key}, uploadId={UploadId}",
                    key, uploadId);
            }
            throw;
        }
    }

    [DebuggerDisableUserUnhandledExceptions]                    // ★
    public async Task<ObjectStorageGetResult> GetAsync(
        string key, long? start = null, long? end = null,
        CancellationToken ct = default)
    {
        var request = new GetObjectRequest { BucketName = _bucket, Key = key };

        if (start.HasValue && end.HasValue)
            request.ByteRange = new ByteRange(start.Value, end.Value);
        else if (start.HasValue)
            request.ByteRange = new ByteRange($"bytes={start.Value}-");
        else if (end.HasValue)
            request.ByteRange = new ByteRange(0, end.Value);

        GetObjectResponse response;
        try
        {
            response = await client.GetObjectAsync(request, ct);
        }
        catch (AmazonS3Exception ex) when (
            ex.StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            throw new RangeNotSatisfiableException();
        }
        catch (AmazonS3Exception ex) when (
            ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new ObjectNotFoundException(key);
        }

        string? contentRange = response.Headers?["Content-Range"];

        long? totalLength = null;
        if (!string.IsNullOrEmpty(contentRange))
        {
            var slash = contentRange.LastIndexOf('/');
            if (slash >= 0 && slash < contentRange.Length - 1)
            {
                var totalStr = contentRange.Substring(slash + 1);
                if (long.TryParse(totalStr,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var t))
                {
                    totalLength = t;
                }
            }
        }

        totalLength ??= response.ContentLength;

        return new ObjectStorageGetResult(
            new ObjectStream(response),
            response.ContentLength,
            totalLength,
            contentRange);
    }

    [DebuggerDisableUserUnhandledExceptions]                    // ★
    public async Task<bool> DeleteAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await client.DeleteObjectAsync(_bucket, key, ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    [DebuggerDisableUserUnhandledExceptions]                    // ★
    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await client.GetObjectMetadataAsync(_bucket, key, ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    [DebuggerDisableUserUnhandledExceptions]                    // ★
    private async Task EnsureBucketAsync(CancellationToken ct)
    {
        if (_bucketChecked) return;
        try
        {
            var exists = await AmazonS3Util.DoesS3BucketExistV2Async(client, _bucket);
            if (!exists)
            {
                await client.PutBucketAsync(new PutBucketRequest { BucketName = _bucket }, ct);
                logger.LogInformation("已创建 S3 桶：{Bucket}", _bucket);
            }
            _bucketChecked = true;
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode == "BucketAlreadyOwnedByYou")
        {
            _bucketChecked = true;
        }
    }
}