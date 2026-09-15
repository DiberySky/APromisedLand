using FileStorageApi.Files;
using FileStorageApi.Models;
using FileStorageApi.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace FileStorageApi.Controllers;

[ApiController]
[Route("[controller]")]
[Produces("application/json")]
public sealed class FilesController(IFileMetadataService files) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FileMetadataDto>>> List(
        [FromQuery] int skip = 0, [FromQuery] int take = 50,
        CancellationToken ct = default)
        => Ok(await files.ListAsync(skip, take, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FileMetadataDto>> GetById(Guid id, CancellationToken ct)
    {
        var dto = await files.GetByIdAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpGet("by-doc/{docId}")]
    public async Task<ActionResult<FileMetadataDto>> GetByDocId(
        string docId, [FromQuery] int? version = null, CancellationToken ct = default)
    {
        var dto = await files.GetByDocIdAsync(docId, version, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        // 1) 先取元数据，用于解析 Range 语义（bytes=-N 需要 Size，越界需 416）
        var head = await files.GetByIdAsync(id, ct);
        if (head is null) return NotFound();

        long? start = null, end = null;
        var isRangeRequest = false;
        var range = Request.Headers.Range.FirstOrDefault();

        if (!string.IsNullOrEmpty(range) && range.StartsWith("bytes="))
        {
            var spec = range["bytes=".Length..];

            // 多 Range 直接降级为 200 全量（保守策略）
            if (spec.Contains(','))
                return await FullDownloadAsync(id, ct);

            var parts = spec.Split('-');
            if (parts.Length == 2)
            {
                if (long.TryParse(parts[0], out var s))
                {
                    // 起点越界 → 416（RFC 7233）
                    if (s >= head.Size)
                    {
                        Response.Headers.ContentRange = $"bytes */{head.Size}";
                        return StatusCode(StatusCodes.Status416RangeNotSatisfiable);
                    }

                    start = s;

                    if (long.TryParse(parts[1], out var e))
                    {
                        // ★ 修复：start > end 属非法 Range，直接 416
                        //   否则 length = end - start + 1 可能为负
                        if (e < s)
                        {
                            Response.Headers.ContentRange = $"bytes */{head.Size}";
                            return StatusCode(StatusCodes.Status416RangeNotSatisfiable);
                        }
                        end = Math.Min(e, head.Size - 1);
                    }
                    isRangeRequest = true;
                }
                else if (long.TryParse(parts[1], out var suffixLen) && suffixLen > 0)
                {
                    // ★ 修复：空对象 + suffix Range → 任何 Range 都 416
                    if (head.Size == 0)
                    {
                        Response.Headers.ContentRange = "bytes */0";
                        return StatusCode(StatusCodes.Status416RangeNotSatisfiable);
                    }

                    start = Math.Max(0, head.Size - suffixLen);
                    end = head.Size - 1;
                    isRangeRequest = true;
                }
            }
        }

        DownloadResult? result;
        try
        {
            result = await files.DownloadAsync(id, start, end, ct);
        }
        catch (RangeNotSatisfiableException ex)
        {
            // ★ 存储层拿不到响应头；用元数据 Size 填充 Content-Range
            Response.Headers.ContentRange =
                ex.ContentRange ?? $"bytes */{head.Size}";
            return StatusCode(StatusCodes.Status416RangeNotSatisfiable);
        }

        if (result is null) return NotFound();

        var contentType = string.IsNullOrWhiteSpace(result.Metadata.ContentType)
            ? "application/octet-stream"
            : result.Metadata.ContentType;

        if (!isRangeRequest)
            return File(result.Content, contentType, result.Metadata.FileName);

        // ── 206 Partial Content ──
        var actualStart = start ?? 0;
        var actualEnd = end ?? result.Metadata.Size - 1;
        var total = result.TotalLength ?? result.Metadata.Size;
        var length = actualEnd - actualStart + 1;

        Response.StatusCode = StatusCodes.Status206PartialContent;
        Response.ContentType = contentType;
        Response.ContentLength = length;
        Response.Headers.ContentRange = $"bytes {actualStart}-{actualEnd}/{total}";
        Response.Headers.AcceptRanges = "bytes";

        if (!string.IsNullOrEmpty(result.Metadata.FileName))
        {
            var cd = new Microsoft.Net.Http.Headers.ContentDispositionHeaderValue("attachment");
            cd.SetHttpFileName(result.Metadata.FileName);
            Response.Headers.ContentDisposition = cd.ToString();
        }

        await using (result.Content)
        {
            await result.Content.CopyToAsync(Response.Body, ct);
        }

        return new EmptyResult();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await files.DeleteAsync(id, ct) ? NoContent() : NotFound();

    // ── 本地辅助：全量下载（多 Range 降级用） ──
    private async Task<IActionResult> FullDownloadAsync(Guid id, CancellationToken ct)
    {
        DownloadResult? full;
        try
        {
            full = await files.DownloadAsync(id, null, null, ct);
        }
        catch (RangeNotSatisfiableException)
        {
            // 全量下载理论上不应触发 416；若发生，返回 500 便于排查
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        if (full is null) return NotFound();

        var contentType = string.IsNullOrWhiteSpace(full.Metadata.ContentType)
            ? "application/octet-stream"
            : full.Metadata.ContentType;

        return File(full.Content, contentType, full.Metadata.FileName);
    }
}