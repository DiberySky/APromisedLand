using APromisedLand.Api.Data;
using Hangfire;
using MAFRagService.Models;
using MAFRagService.Services;
using Microsoft.AspNetCore.Mvc;

namespace MAFRagService.Controllers;

[ApiController]
[Route("rag/docs")]
public class DocsController : BaseApiController
{
    private readonly IBackgroundJobClient    _hangfire;
    private readonly DocumentStorageService  _storage;
    private readonly DocumentMetadataService _metadataService;
    private readonly DocumentAuditService    _auditService;
    private readonly EventStoreService       _eventStore;
    private readonly IndexTaskService        _indexTasks;
    private readonly MafRagContext           _db;
    private readonly ILogger<DocsController> _logger;

    public DocsController(
        IBackgroundJobClient hangfire,
        DocumentStorageService storage,
        DocumentMetadataService metadataService,
        DocumentAuditService auditService,
        EventStoreService eventStore,
        IndexTaskService indexTasks,
        MafRagContext db,
        IConfiguration config,
        ILogger<DocsController> logger)
        : base(config)
    {
        _hangfire        = hangfire;
        _storage         = storage;
        _metadataService = metadataService;
        _auditService    = auditService;
        _eventStore      = eventStore;
        _indexTasks      = indexTasks;
        _db              = db;
        _logger          = logger;
    }

    [HttpPost]
    [RequestSizeLimit(512L * 1024 * 1024)]
    public async Task<IActionResult> Upload(
        [FromForm] IFormFile file,
        [FromForm] string docId,
        [FromForm] string? version,
        CancellationToken ct)
    {
        if (!Features.Rag)      return ModuleDisabled("Rag");
        if (!Features.Indexing) return ModuleDisabled("Indexing");

        if (file is null || string.IsNullOrWhiteSpace(docId))
            return BadRequest("Missing file or docId");

        if (string.IsNullOrWhiteSpace(version))
            version = $"v{DateTime.UtcNow:yyyyMMddHHmmss}";

        var tenant = ResolveTenant();
        // ★ 开发阶段无身份验证，Operator 留空
        string? @operator = null;

        var blobInfo = await _storage.UploadAsync(
            file.OpenReadStream(), docId, version, file.FileName, tenant, ct);

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

            await _eventStore.AppendEventAsync(docId, new
            {
                DocId     = docId,
                FileName  = file.FileName,
                Tenant    = tenant,
                Version   = version,
                Timestamp = DateTime.UtcNow
            }, tenant, ct);

            await _auditService.RecordAsync(
                docId, tenant, action: "Upload",
                oldVersion: null, newVersion: version,
                @operator: @operator, ct);

            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogError(ex,
                "上传处理失败，事务已回滚。DocId={DocId}, Tenant={Tenant}, Version={Version}",
                docId, tenant, version);

            try
            {
                await _storage.DeleteAsync(blobInfo.BlobName, ct);
                _logger.LogInformation("已补偿删除 blob: {Blob}", blobInfo.BlobName);
            }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(cleanupEx,
                    "补偿删除 blob 失败，需人工清理: {Blob}", blobInfo.BlobName);
            }

            return Problem("Document metadata persistence failed.");
        }

        var jobId = _hangfire.Enqueue<IncrementalIndexer>(
            x => x.IndexAsync(docId, version, tenant, JobCancellationToken.Null));

        _logger.LogInformation(
            "文档已上传并已入队索引。DocId={DocId}, HangfireJobId={JobId}",
            docId, jobId);

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

    [HttpGet("{docId}/status")]
    public async Task<IActionResult> Status(string docId, CancellationToken ct)
    {
        if (!Features.Indexing) return ModuleDisabled("Indexing");

        var tenant = ResolveTenant();
        var tasks  = await _indexTasks.ListByDocAsync(docId, tenant, ct);

        return Ok(new
        {
            DocId  = docId,
            Tenant = tenant,
            Tasks  = tasks.Select(t => new
            {
                t.Id, t.TaskType, t.Status,
                t.ErrorMessage, t.RetryCount,
                t.CreatedAt, t.CompletedAt
            })
        });
    }

    [HttpGet("{docId}/audit")]
    public async Task<IActionResult> Audit(string docId, CancellationToken ct)
    {
        var tenant = ResolveTenant();
        if (string.IsNullOrEmpty(tenant) || tenant == "default")
            return Forbid();

        var audits = await _auditService.ListAsync(docId, tenant, ct);
        return Ok(audits);
    }

    [HttpGet("{docId}/events")]
    public async Task<IActionResult> Events(string docId, CancellationToken ct)
    {
        if (!Features.Indexing) return ModuleDisabled("Indexing");

        var tenant = ResolveTenant();
        var events = await _eventStore.GetEventsAsync(docId, tenant, ct);
        return Ok(events);
    }
}