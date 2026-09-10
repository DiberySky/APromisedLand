using APromisedLand.Api.Data;
using Hangfire;
using MAFRagService.Models;
using MAFRagService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MAFRagService.Controllers;

[ApiController]
[Route("rag/docs")]
[Authorize]
public class DocsController : BaseApiController
{
    private readonly IBackgroundJobClient    _hangfire;
    private readonly DocumentStorageService  _storage;
    private readonly DocumentMetadataService _metadataService;
    private readonly EventStoreService       _eventStore;
    private readonly MafRagContext           _db;
    private readonly ILogger<DocsController> _logger;

    public DocsController(
        IBackgroundJobClient hangfire,
        DocumentStorageService storage,
        DocumentMetadataService metadataService,
        EventStoreService eventStore,
        MafRagContext db,
        IConfiguration config,
        ILogger<DocsController> logger)
        : base(config)
    {
        _hangfire        = hangfire;
        _storage         = storage;
        _metadataService = metadataService;
        _eventStore      = eventStore;
        _db              = db;
        _logger          = logger;
    }

    // ============================================================
    // POST /rag/docs
    // 上传文档：先落对象存储 → 元数据 + 事件同事务 → 入队索引任务
    // ============================================================
    [HttpPost]
    [RequestSizeLimit(512L * 1024 * 1024)] // 512MB，可按需调整
    public async Task<IActionResult> Upload(
        [FromForm] IFormFile file,
        [FromForm] string docId,
        [FromForm] string? version,
        CancellationToken ct)
    {
        if (file is null || string.IsNullOrWhiteSpace(docId))
            return BadRequest("Missing file or docId");

        if (string.IsNullOrWhiteSpace(version))
            version = $"v{DateTime.UtcNow:yyyyMMddHHmmss}";

        var tenant = ResolveTenant();

        // 1) 先落对象存储（不可回滚的外部副作用）
        var blobInfo = await _storage.UploadAsync(
            file.OpenReadStream(), docId, version, file.FileName, tenant, ct);

        // 2) 元数据 + 事件溯源，同事务
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            await _metadataService.InsertAsync(new DocumentMetadata
            {
                DocId    = docId,
                Tenant   = tenant,
                Version  = version,
                FileName = file.FileName,
                FileSize = file.Length,
                MimeType = file.ContentType,
                BlobUri  = blobInfo.BlobUri,
                BlobName = blobInfo.BlobName
            }, ct);

            var evt = new
            {
                DocId     = docId,
                FileName  = file.FileName,
                Tenant    = tenant,
                Version   = version,
                Timestamp = DateTime.UtcNow
            };
            await _eventStore.AppendEventAsync(docId, evt, tenant, ct);

            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogError(ex,
                "上传处理失败，事务已回滚。DocId={DocId}, Tenant={Tenant}, Version={Version}",
                docId, tenant, version);
            return Problem("Document metadata persistence failed.");
        }

        // 3) 入队后台索引任务
        var jobId = _hangfire.Enqueue<IncrementalIndexer>(
            x => x.IndexAsync(docId, version, tenant));

        _logger.LogInformation(
            "文档已上传并已入队索引。DocId={DocId}, HangfireJobId={JobId}", docId, jobId);

        return Accepted(
            $"/rag/docs/{docId}/status",
            new
            {
                DocId   = docId,
                Version = version,
                Tenant  = tenant,
                JobId   = jobId,
                Status  = "Uploaded"
            });
    }

    // ============================================================
    // GET /rag/docs/{docId}/audit
    // 强制按租户过滤
    // ============================================================
    [HttpGet("{docId}/audit")]
    public async Task<IActionResult> Audit(string docId, CancellationToken ct)
    {
        var tenant = ResolveTenant();
        if (string.IsNullOrEmpty(tenant) || tenant == "default")
            return Forbid();

        var events = await _eventStore.GetEventsAsync(docId, ct);

        var filtered = events
            .Where(e => string.Equals(
                e.GetType().GetProperty("Tenant")?.GetValue(e)?.ToString(),
                tenant,
                StringComparison.Ordinal))
            .ToList();

        return Ok(filtered);
    }
}