namespace FileTransService.Models;

public interface IFileService
{
    Task<FileMetadata> UploadAsync(UploadFileRequest request);
    Task<(Stream FileStream, FileMetadata Metadata)> DownloadAsync(Guid fileId);
    Task DeleteAsync(Guid fileId);
    Task<FileMetadata?> GetMetadataAsync(Guid fileId);
    Task<FileMetadata> CompleteTusUploadAsync(CompleteUploadRequest request);
}