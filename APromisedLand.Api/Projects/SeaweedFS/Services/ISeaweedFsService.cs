using APromisedLand.Api.Projects.SeaweedFS.Models;
using FileMetadata = APromisedLand.Api.Projects.SeaweedFS.Models.FileMetadata;

namespace APromisedLand.Api.Projects.SeaweedFS.Services;

public interface ISeaweedFsService
{
    Task<FileMetadata> UploadAsync(UploadFileRequest request);
    Task<(Stream FileStream, FileMetadata Metadata)> DownloadAsync(Guid fileId);
    Task DeleteAsync(Guid fileId);
    Task<FileMetadata?> GetMetadataAsync(Guid fileId);
    Task<FileMetadata> CompleteTusUploadAsync(CompleteUploadRequest request);
}