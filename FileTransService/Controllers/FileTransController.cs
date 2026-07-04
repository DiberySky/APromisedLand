using FileTransService.Models;
using Microsoft.AspNetCore.Mvc;

namespace FileTransService.Controllers;

[ApiController]
[Route("[controller]")]
public class FileTransController : ControllerBase
{
    private readonly IFileService _fileService;

    public FileTransController(IFileService fileService)
    {
        _fileService = fileService;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile? file, [FromQuery] string? businessId = null)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file provided");

        await using var stream = file.OpenReadStream();
        var request = new UploadFileRequest
        {
            FileStream = stream,
            FileName = file.FileName,
            MimeType = file.ContentType,
            FileSize = file.Length,
            BusinessId = businessId
        };

        var result = await _fileService.UploadAsync(request);
        return Ok(new { fileId = result.Id, fileName = result.FileName, size = result.FileSize });
    }

    [HttpGet("download/{id}")]
    public async Task<IActionResult> Download(Guid id)
    {
        var (stream, metadata) = await _fileService.DownloadAsync(id);
        return File(stream, metadata.MimeType ?? "application/octet-stream", metadata.FileName);
    }

    [HttpGet("{id}/metadata")]
    public async Task<IActionResult> GetMetadata(Guid id)
    {
        var metadata = await _fileService.GetMetadataAsync(id);
        return metadata == null ? NotFound() : Ok(metadata);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _fileService.DeleteAsync(id);
        return NoContent();
    }
}