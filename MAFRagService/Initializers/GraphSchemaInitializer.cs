using MAFRagService.Stubs.NebulaGraph;

namespace MAFRagService.Initializers;

public class GraphSchemaInitializer
{
    private const string SpaceName = "rag_space";

    // ★ 用轮询代替魔法 Task.Delay(2000)
    private static readonly TimeSpan SpaceReadyTimeout    = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SpaceReadyPollDelay  = TimeSpan.FromMilliseconds(500);

    private readonly NebulaGraphClient _client;
    private readonly ILogger<GraphSchemaInitializer> _logger;

    public GraphSchemaInitializer(NebulaGraphClient client, ILogger<GraphSchemaInitializer> logger)
    {
        _client = client;
        _logger = logger;
    }

    // ★ DDL 拆成独立语句，避免依赖 ExecuteAsync 对多语句的支持
    private static readonly string[] SchemaDdls =
    {
        // ---- Tags ----
        "CREATE TAG IF NOT EXISTS Document (doc_id string, tenant string, version string, file_name string, uploaded_at timestamp);",
        "CREATE TAG IF NOT EXISTS Entity (name string, type string, tenant string, created_at timestamp);",
        "CREATE TAG IF NOT EXISTS Chunk (chunk_id string, doc_id string, content string, tenant string, index int);",
        "CREATE TAG IF NOT EXISTS Conversation (question string, tenant string, timestamp timestamp);",
        "CREATE TAG IF NOT EXISTS MemoryEntity (name string, tenant string, timestamp timestamp);",

        // ---- Edges ----
        "CREATE EDGE IF NOT EXISTS MENTIONS (weight double, timestamp timestamp);",
        "CREATE EDGE IF NOT EXISTS CO_OCCURS_WITH (count int, timestamp timestamp);",
        "CREATE EDGE IF NOT EXISTS BELONGS_TO (timestamp timestamp);",
        "CREATE EDGE IF NOT EXISTS HAS_CHUNK (order int);",

        // ---- Indexes ----
        "CREATE TAG INDEX IF NOT EXISTS idx_entity_name ON Entity(name(64));",
        "CREATE TAG INDEX IF NOT EXISTS idx_doc_tenant ON Document(tenant(32));",
        "CREATE TAG INDEX IF NOT EXISTS idx_chunk_doc ON Chunk(doc_id(64));",
        "CREATE EDGE INDEX IF NOT EXISTS idx_mentions_timestamp ON MENTIONS(timestamp);"
    };

    public async Task InitializeAsync(CancellationToken ct)
    {
        _logger.LogInformation("NebulaGraph schema init started (space='{Space}')", SpaceName);

        // ---- 1. 创建 space ----
        await ExecuteAsync($@"
            CREATE SPACE IF NOT EXISTS {SpaceName} (
                vid_type = FIXED_STRING(128),
                partition_num = 1,
                replica_factor = 1
            );", ct);

        // ---- 2. 等待 space 就绪（heartbeat 后 graphd 才能感知）----
        await WaitForSpaceReadyAsync(ct);

        // ---- 3. 切到该 space ----
        await _client.ChangeSpaceAsync(SpaceName, ct);

        // ---- 4. Tags / Edges / Indexes ----
        foreach (var ddl in SchemaDdls)
            await ExecuteAsync(ddl, ct);

        _logger.LogInformation("NebulaGraph schema initialized (space='{Space}')", SpaceName);
    }

    /// <summary>
    /// ★ 用轮询 "USE space" 代替固定 2 秒等待：
    ///   冷启动时可能更慢（一次性把超时时间拉长到 30 秒）；
    ///   热启动时几乎立即返回，不浪费启动时间。
    /// </summary>
    private async Task WaitForSpaceReadyAsync(CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + SpaceReadyTimeout;
        Exception? lastError = null;

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var probe = await _client.ExecuteAsync($"USE {SpaceName};", ct);
                if (probe.IsSucceeded) return;
                lastError = new Exception(probe.ErrorMessage);
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
            await Task.Delay(SpaceReadyPollDelay, ct);
        }

        throw new TimeoutException(
            $"NebulaGraph space '{SpaceName}' 未在 {SpaceReadyTimeout.TotalSeconds}s 内就绪。" +
            $"最后错误：{lastError?.Message}");
    }

    private async Task ExecuteAsync(string ngql, CancellationToken ct)
    {
        var result = await _client.ExecuteAsync(ngql, ct);
        if (result.IsSucceeded) return;

        // ★ 幂等：already exists 视为成功（加 OrdinalIgnoreCase 避免大小写差异）
        if (result.ErrorMessage.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            return;

        throw new Exception($"Nebula init error: {result.ErrorMessage}");
    }
}