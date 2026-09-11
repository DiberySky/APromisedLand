using MAFRagService.Stubs.NebulaGraph;
using MAFRagService.Startup.Configuration;
using Microsoft.Extensions.Options;

namespace MAFRagService.Initializers;

public class GraphSchemaInitializer
{
    private static readonly TimeSpan SpaceReadyTimeout   = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SpaceReadyPollDelay = TimeSpan.FromMilliseconds(500);

    private readonly NebulaGraphClient _client;
    private readonly string _spaceName;
    private readonly ILogger<GraphSchemaInitializer> _logger;

    public GraphSchemaInitializer(
        NebulaGraphClient client,
        IOptions<NebulaGraphAppOptions> options,
        ILogger<GraphSchemaInitializer> logger)
    {
        _client    = client;
        _spaceName = options.Value.Space;
        _logger    = logger;
    }

    // DDL 改为实例方法：space 名从注入的 Options 读取
    private string[] BuildDdls() => new[]
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
        _logger.LogInformation("NebulaGraph schema init started (space='{Space}')", _spaceName);

        await ExecuteAsync($@"
            CREATE SPACE IF NOT EXISTS {_spaceName} (
                vid_type = FIXED_STRING(128),
                partition_num = 1,
                replica_factor = 1
            );", ct);

        await WaitForSpaceReadyAsync(ct);
        await _client.ChangeSpaceAsync(_spaceName, ct);

        foreach (var ddl in BuildDdls())
            await ExecuteAsync(ddl, ct);

        _logger.LogInformation("NebulaGraph schema initialized (space='{Space}')", _spaceName);
    }

    private async Task WaitForSpaceReadyAsync(CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + SpaceReadyTimeout;
        Exception? lastError = null;

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var probe = await _client.ExecuteAsync($"USE {_spaceName};", ct);
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
            $"NebulaGraph space '{_spaceName}' 未在 {SpaceReadyTimeout.TotalSeconds}s 内就绪。" +
            $"最后错误：{lastError?.Message}");
    }

    private async Task ExecuteAsync(string ngql, CancellationToken ct)
    {
        var result = await _client.ExecuteAsync(ngql, ct);
        if (result.IsSucceeded) return;

        if (result.ErrorMessage.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            return;

        throw new Exception($"Nebula init error: {result.ErrorMessage}");
    }
}