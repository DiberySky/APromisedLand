using APromisedLand.Api.Projects.SeaweedFS.Data;
using APromisedLand.Api.Projects.SeaweedFS.Models;
using APromisedLand.Api.Projects.SeaweedFS.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace APromisedLand.Api.Projects.SeaweedFS;

[ApiController]
[Route("[controller]")]
public class SeaweedFsController(ISeaweedFsService seaweedFsService, SeaweedFsDbContext db,
    HttpClient client) : ControllerBase
{
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

        var result = await seaweedFsService.UploadAsync(request);
        return Ok(new { fileId = result.Id, fileName = result.FileName, size = result.FileSize });
    }

    [HttpGet("download/{id}")]
    public async Task<IActionResult> Download(Guid id)
    {
        var (stream, metadata) = await seaweedFsService.DownloadAsync(id);
        return File(stream, metadata.MimeType ?? "application/octet-stream", metadata.FileName);
    }

    [HttpGet("{id}/metadata")]
    public async Task<IActionResult> GetMetadata(Guid id)
    {
        var metadata = await seaweedFsService.GetMetadataAsync(id);
        return metadata == null ? NotFound() : Ok(metadata);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await seaweedFsService.DeleteAsync(id);
        return NoContent();
    }
    
    [HttpPost("complete")]
    public async Task<IActionResult> CompleteUpload([FromBody] CompleteUploadRequest request)
    {
        var metadata = await seaweedFsService.CompleteTusUploadAsync(request);
        return Ok(new { fileId = metadata.Id, fid = metadata.Fid });
    }
}