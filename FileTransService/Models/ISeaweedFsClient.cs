namespace FileTransService.Models;

public interface ISeaweedFsClient
{
    Task<string> UploadAsync(Stream fileStream, string fileName, string? path = null);
    Task<Stream> DownloadAsync(string fid);
    Task DeleteAsync(string fid);
    Task<string> GetFidFromTusUploadAsync(string uploadId);
}