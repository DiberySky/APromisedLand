using System.Diagnostics;
using Hangfire;
using MAFRagService.Connectors;
using MAFRagService.Models;
using MAFRagService.Services.TextChunking;
using MAFRagService.Services.Weaviate;
using MAFRagService.Stubs.NebulaGraph;

namespace MAFRagService.Services;

/// <summary>
/// 增量索引器。
///
/// <para><b>Hangfire 可调度方法约束：</b></para>
/// <list type="bullet">
///   <item>必须是 public <b>实例</b>方法；</item>
///   <item>参数必须可 JSON 序列化（string / int / Guid / 数组等）；</item>
///   <item>可以使用 <see cref="IJobCancellationToken"/> 参数——Hangfire 会识别并
///         注入真实 token，不参与序列化。调用方 Enqueue 时用
///         <see cref="JobCancellationToken.Null"/> 占位。</item>
///   <item><b>禁止</b>使用 <see cref="System.Threading.CancellationToken"/> 参数
///         （无法序列化）；也不要在方法体里访问
///         <c>JobCancellationToken.Null.ShutdownToken</c>——
///         它是一个 null 占位符，访问成员会 NRE。</item>
///   <item>返回 Task 走异步重载。</item>
/// </list>
///
/// <para><b>状态归属：</b></para>
/// <list type="bullet">
///   <item>索引状态  → <c>IndexTaskEntity</c>（TaskType=full）</item>
///   <item>文档生命周期 → <c>DocumentMetadataEntity.Status</c>（active/inactive）</item>
///   <item>审计轨迹  → <c>DocumentAuditEntity</c></item>
///   <item>事件溯源  → <c>DomainEventEntity</c></item>
/// </list>
///
/// <para><b>幂等性：</b></para>
/// <list type="bullet">
///   <item>IndexTaskService.BeginAsync 复用未完成任务；</item>
///   <item>Weaviate 先删后写；</item>
///   <item>Nebula 使用 INSERT VERTEX/EDGE 覆盖语义。</item>
/// </list>
/// </summary>
public class IncrementalIndexer
{
    public const string TaskType = "full";

    private readonly DocumentMetadataService _metadata;
    private readonly DocumentStorageService _storage;
    private readonly IndexTaskService _tasks;
    private readonly ITextChunker _chunker;
    private readonly IOllamaEmbeddingClient _embedding;
    private readonly IWeaviateChunkWriter _weaviate;
    private readonly IEntityExtractionService _entityService;
    private readonly IndexerCapabilities _caps;
    private readonly ILogger<IncrementalIndexer> _logger;

    public IncrementalIndexer(
        DocumentMetadataService metadata,
        DocumentStorageService storage,
        IndexTaskService tasks,
        ITextChunker chunker,
        IOllamaEmbeddingClient embedding,
        IWeaviateChunkWriter weaviate,
        IEntityExtractionService entityService,
        IndexerCapabilities caps,
        ILogger<IncrementalIndexer> logger)
    {
        _metadata      = metadata;
        _storage       = storage;
        _tasks         = tasks;
        _chunker       = chunker;
        _embedding     = embedding;
        _weaviate      = weaviate;
        _entityService = entityService;
        _caps          = caps;
        _logger        = logger;
    }

    [Queue("indexing")]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task IndexAsync(
        string docId,
        string version,
        string tenant,
        IJobCancellationToken cancellationToken)   // ★ 接口，不是 JobCancellationToken
    {
        // ★ 修复：用注入的真实 token，不再访问 JobCancellationToken.Null.ShutdownToken
        var ct = cancellationToken.ShutdownToken;
        var sw = Stopwatch.StartNew();

        _logger.LogInformation(
            "索引开始 DocId={DocId}, Version={Version}, Tenant={Tenant}",
            docId, version, tenant);

        var task = await _tasks.BeginAsync(docId, tenant, TaskType, ct);

        try
        {
            // 1. 读元数据
            var meta = await _metadata.GetAsync(docId, version, tenant, ct)
                ?? throw new InvalidOperationException(
                    $"元数据不存在：DocId={docId}, Version={version}, Tenant={tenant}");

            // 2. 下载原文
            await using var stream = await _storage.DownloadAsync(meta.BlobName, ct);
            using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
            var text = await reader.ReadToEndAsync(ct);

            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException($"文档内容为空：{meta.BlobName}");

            // 3. 分块
            var chunks = _chunker.Chunk(text, new ChunkOptions
            {
                MaxChars     = 800,
                OverlapChars = 100,
                MinChars     = 20
            });
            _logger.LogInformation(
                "分块完成 DocId={DocId}, Chunks={Count}", docId, chunks.Count);

            // 4. 幂等清理旧 chunk
            await _weaviate.DeleteByDocAsync(docId, version, tenant, ct);

            // 5. Embedding + 写 Weaviate
            var records = new List<ChunkRecord>(chunks.Count);
            foreach (var chunk in chunks)
            {
                ct.ThrowIfCancellationRequested();
                var vector = await _embedding.EmbedAsync(chunk.Text, ct);
                records.Add(new ChunkRecord
                {
                    DocId      = docId,
                    Tenant     = tenant,
                    Version    = version,
                    ChunkIndex = chunk.Index,
                    Content    = chunk.Text,
                    FilePath   = meta.BlobName,
                    Vector     = vector
                });
            }
            await _weaviate.UpsertBatchAsync(records, ct);

            // 6. 实体抽取 + 图写入（Graph 关闭时跳过）
            if (_caps.Nebula is not null)
            {
                var entities = await _entityService.ExtractAsync(text, tenant, ct);
                if (entities.Count > 0)
                    await WriteGraphAsync(meta, chunks, entities, tenant, ct);
            }

            // 7. 完成
            await _tasks.CompleteAsync(task.Id, ct);

            _logger.LogInformation(
                "索引完成 DocId={DocId}, Chunks={Count}, Elapsed={Elapsed}ms",
                docId, chunks.Count, sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            await _tasks.CancelAsync(task.Id, CancellationToken.None);
            _logger.LogWarning("索引被取消 DocId={DocId}", docId);
            throw;
        }
        catch (Exception ex)
        {
            await _tasks.FailAsync(task.Id, ex.Message, CancellationToken.None);
            _logger.LogError(ex,
                "索引失败 DocId={DocId}, Version={Version}", docId, version);
            throw; // 交给 Hangfire 重试策略
        }
    }

    // ------------------------------------------------------------
    // Nebula 写入
    // ------------------------------------------------------------
    private async Task WriteGraphAsync(
        DocumentMetadata meta,
        IReadOnlyList<TextChunk> chunks,
        List<EntityInfo> entities,
        string tenant,
        CancellationToken ct)
    {
        var nebula = _caps.Nebula!;
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Document 顶点
        await nebula.ExecuteWithRetryAsync($@"
            INSERT VERTEX Document (doc_id, tenant, version, file_name, uploaded_at)
            VALUES ""{Esc(meta.DocId)}"":
                (""{Esc(meta.DocId)}"", ""{Esc(tenant)}"", ""{Esc(meta.Version)}"", ""{Esc(meta.FileName)}"", {ts})
        ", ct);

        // Chunk 顶点 + HAS_CHUNK 边
        foreach (var c in chunks)
        {
            var chunkVid = $"{meta.DocId}:{meta.Version}:{c.Index}";
            await nebula.ExecuteWithRetryAsync($@"
                INSERT VERTEX Chunk (chunk_id, doc_id, content, tenant, index)
                VALUES ""{Esc(chunkVid)}"":
                    (""{Esc(chunkVid)}"", ""{Esc(meta.DocId)}"", ""{Esc(Truncate(c.Text, 200))}"", ""{Esc(tenant)}"", {c.Index})
            ", ct);

            await nebula.ExecuteWithRetryAsync($@"
                INSERT EDGE HAS_CHUNK (order)
                VALUES ""{Esc(meta.DocId)}"" -> ""{Esc(chunkVid)}"" @ ({c.Index})
            ", ct);
        }

        // Entity 顶点 + MENTIONS 边
        foreach (var e in entities)
        {
            var entityVid = $"entity:{e.Type}:{e.Name}:{tenant}";

            await nebula.ExecuteWithRetryAsync($@"
                INSERT VERTEX Entity (name, type, tenant, created_at)
                VALUES ""{Esc(entityVid)}"":
                    (""{Esc(e.Name)}"", ""{Esc(e.Type)}"", ""{Esc(tenant)}"", {ts})
            ", ct);

            await nebula.ExecuteWithRetryAsync($@"
                INSERT EDGE MENTIONS (weight, timestamp)
                VALUES ""{Esc(meta.DocId)}"" -> ""{Esc(entityVid)}"" @ (1.0, {ts})
            ", ct);
        }

        _logger.LogInformation(
            "Nebula 写入完成 DocId={DocId}, Chunks={Chunks}, Entities={Entities}",
            meta.DocId, chunks.Count, entities.Count);
    }

    private static string Esc(string? s) =>
        (s ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", string.Empty);

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max];
}

/// <summary>
/// 索引器可选能力。Graph 关闭时 Nebula 为 null，Indexer 自动跳过图写入。
/// </summary>
public sealed record IndexerCapabilities(NebulaGraphExecutor? Nebula);