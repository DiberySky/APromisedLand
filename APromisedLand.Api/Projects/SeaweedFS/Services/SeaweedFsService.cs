using System.Text.Json;
using APromisedLand.Api.Projects.SeaweedFS.Data;
using APromisedLand.Api.Projects.SeaweedFS.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace APromisedLand.Api.Projects.SeaweedFS.Services;

public class SeaweedFsService(SeaweedFsDbContext dbContext, 
    ISeaweedFsClient seaweedClient, ILogger<SeaweedFsService> logger)
    : ISeaweedFsService
{
    public async Task<FileMetadata> UploadAsync(UploadFileRequest request)
    {
        // 1. 上传文件到 SeaweedFS
        var fid = await seaweedClient.UploadAsync(request.FileStream, request.FileName);

        // 2. 保存元数据到 PostgreSQL
        var metadata = new FileMetadata
        {
            Id = Guid.NewGuid(),
            Fid = fid,
            FileName = request.FileName,
            FileSize = request.FileStream.Length,
            MimeType = request.MimeType ?? "application/octet-stream",
            BusinessId = request.BusinessId,
            BusinessType = request.BusinessType,
            UploaderId = request.UploaderId,
            Metadata = request.CustomMetadata != null 
                ? JsonSerializer.Serialize(request.CustomMetadata) 
                : null,
        };

        dbContext.Files.Add(metadata);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("File uploaded: {FileId}, FID: {Fid}", metadata.Id, fid);
        return metadata;
    }

    public async Task<(Stream FileStream, FileMetadata Metadata)> DownloadAsync(Guid fileId)
    {
        // 1. 从 PostgreSQL 查询元数据
        var metadata = await dbContext.Files
            .FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);
        if (metadata == null)
            throw new FileNotFoundException($"File {fileId} not found");

        // 2. 更新访问统计
        metadata.LastAccessTime = DateTime.UtcNow;
        metadata.DownloadCount++;
        await dbContext.SaveChangesAsync();

        // 3. 从 SeaweedFS 获取文件流
        var stream = await seaweedClient.DownloadAsync(metadata.Fid);
        return (stream, metadata);
    }

    public async Task DeleteAsync(Guid fileId)
    {
        var metadata = await dbContext.Files.FindAsync(fileId);
        if (metadata == null || metadata.IsDeleted)
            throw new FileNotFoundException($"File {fileId} not found");

        // 1. 从 SeaweedFS 删除
        await seaweedClient.DeleteAsync(metadata.Fid);

        // 2. 软删除元数据
        metadata.IsDeleted = true;
        metadata.DeletedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        logger.LogInformation("File deleted: {FileId}", fileId);
    }

    public async Task<FileMetadata?> GetMetadataAsync(Guid fileId)
    {
        return await dbContext.Files
            .FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);
    }
    
    public async Task<FileMetadata> CompleteTusUploadAsync(CompleteUploadRequest request)
    {
        // 1. 通过 SeaweedFsClient 获取 FID
        var fid = await seaweedClient.GetFidFromTusUploadAsync(request.UploadId);

        // 2. 保存元数据到 PostgreSQL
        var metadata = new FileMetadata
        {
            Id = Guid.NewGuid(),
            Fid = fid,
            FileName = request.FileName,
            FileSize = request.FileSize,
            MimeType = request.MimeType ?? "application/octet-stream",
            BusinessId = request.BusinessId,
            UploadTime = DateTime.UtcNow
        };

        dbContext.Files.Add(metadata);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("TUS upload completed, fileId: {FileId}, fid: {Fid}", metadata.Id, fid);
        return metadata;
    }
}