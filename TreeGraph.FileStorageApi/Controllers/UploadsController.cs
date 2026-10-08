using TreeGraph.FileStorageApi.Uploads;
using TreeGraph.Shared.FileStorageSky.Contracts;   // ★ DTO 已迁移到 Shared
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;                    // ★ 新增

namespace TreeGraph.FileStorageApi.Controllers;

[ApiController]
[Route("[controller]")]
[Produces("application/json")]
public sealed class UploadsController(IFileUploadService uploads) : ControllerBase
{
    [DebuggerDisableUserUnhandledExceptions]                    // ★
    [HttpPost("initiate")]
    [ProducesResponseType(typeof(InitiateUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
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
    [ProducesResponseType(typeof(UploadChunkResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
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
    [ProducesResponseType(typeof(UploadStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UploadStatusResponse>> GetStatus(
        Guid uploadId, CancellationToken ct)
    {
        try { return Ok(await uploads.GetStatusAsync(uploadId, ct)); }
        catch (UploadNotFoundException ex) { return NotFound(Problem(ex)); }
    }

    [DebuggerDisableUserUnhandledExceptions]                    // ★
    [HttpPost("{uploadId:guid}/complete")]
    [ProducesResponseType(typeof(CompleteUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
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
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    public async Task<IActionResult> Renew(Guid uploadId, CancellationToken ct)
    {
        try { await uploads.RenewAsync(uploadId, ct); return NoContent(); }
        catch (UploadNotFoundException ex) { return NotFound(Problem(ex)); }
        catch (UploadGoneException ex)     { return StatusCode(StatusCodes.Status410Gone, Problem(ex)); }
        catch (UploadConflictException ex)  { return Conflict(Problem(ex)); }
    }

    [DebuggerDisableUserUnhandledExceptions]                    // ★
    [HttpDelete("{uploadId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(Guid uploadId, CancellationToken ct)
    {
        try { await uploads.CancelAsync(uploadId, ct); return NoContent(); }
        catch (UploadNotFoundException ex) { return NotFound(Problem(ex)); }
        catch (UploadConflictException ex)  { return Conflict(Problem(ex)); }
    }

    private static ProblemDetails Problem(UploadException ex) => new()
    {
        Title = ex.GetType().Name,
        Detail = ex.Message,
    };
}
