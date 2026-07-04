namespace FileTransService.Models;

public record UploadResponse(string Fid, string Name, long Size, string ETag);