using FileStorageApi.Uploads;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;                    // ★ 新增

namespace FileStorageApi.Controllers;

[ApiController]
[Route("[controller]")]
[Produces("application/json")]
public sealed class UploadsController(IFileUploadService uploads) : ControllerBase
{
    [DebuggerDisableUserUnhandledExceptions]                    // ★
    [HttpPost("initiate")]
    public async Task<ActionResult<InitiateUploadResponse>> Initiate(
        [FromBody] InitiateUploadRequest request, CancellationToken ct)
    {
        try { return Ok(await uploads.InitiateAsync(request, ct)); }
        catch (UploadValidationException ex) { return UnprocessableEntity(Problem(ex)); }
    }

    [DebuggerDisableUserUnhandledExceptions]                    // ★
    [HttpPut("{uploadId:guid}/chunks/{chunkIndex:int}")]
    [RequestSizeLimit(16 * 1024 * 1024)]
    [Consumes("application/octet-stream")]
    public async Task<ActionResult<UploadChunkResponse>> UploadChunk(
        Guid uploadId, int chunkIndex, CancellationToken ct)
    {
        var len = Request.ContentLength ?? 0;
        if (len <= 0)
            return BadRequest(new ProblemDetails { Title = "缺少 Content-Length 或请求体为空。" });

        var expectedSha = Request.Headers["X-Chunk-Sha256"].FirstOrDefault();

        try
        {
            return Ok(await uploads.UploadChunkAsync(
                uploadId, chunkIndex, Request.Body, len, expectedSha, ct));
        }
        catch (UploadNotFoundException ex)   { return NotFound(Problem(ex)); }
        catch (UploadGoneException ex)       { return StatusCode(StatusCodes.Status410Gone, Problem(ex)); }
        catch (UploadConflictException ex)   { return Conflict(Problem(ex)); }
        catch (UploadValidationException ex) { return UnprocessableEntity(Problem(ex)); }
    }

    [DebuggerDisableUserUnhandledExceptions]                    // ★
    [HttpGet("{uploadId:guid}/status")]
    public async Task<ActionResult<UploadStatusResponse>> GetStatus(
        Guid uploadId, CancellationToken ct)
    {
        try { return Ok(await uploads.GetStatusAsync(uploadId, ct)); }
        catch (UploadNotFoundException ex) { return NotFound(Problem(ex)); }
    }

    [DebuggerDisableUserUnhandledExceptions]                    // ★
    [HttpPost("{uploadId:guid}/complete")]
    public async Task<ActionResult<CompleteUploadResponse>> Complete(
        Guid uploadId, [FromBody] CompleteUploadRequest request, CancellationToken ct)
    {
        try { return Ok(await uploads.CompleteAsync(uploadId, request, ct)); }
        catch (UploadNotFoundException ex)   { return NotFound(Problem(ex)); }
        catch (UploadGoneException ex)       { return StatusCode(StatusCodes.Status410Gone, Problem(ex)); }
        catch (UploadConflictException ex)   { return Conflict(Problem(ex)); }
        catch (UploadValidationException ex) { return UnprocessableEntity(Problem(ex)); }
    }

    [DebuggerDisableUserUnhandledExceptions]                    // ★
    [HttpPost("{uploadId:guid}/heartbeat")]
    public async Task<IActionResult> Renew(Guid uploadId, CancellationToken ct)
        => await uploads.RenewAsync(uploadId, ct) ? NoContent() : NotFound();

    [DebuggerDisableUserUnhandledExceptions]                    // ★
    [HttpDelete("{uploadId:guid}")]
    public async Task<IActionResult> Cancel(Guid uploadId, CancellationToken ct)
        => await uploads.CancelAsync(uploadId, ct) ? NoContent() : NotFound();

    private static ProblemDetails Problem(UploadException ex) => new()
    {
        Title = ex.GetType().Name,
        Detail = ex.Message,
    };
}