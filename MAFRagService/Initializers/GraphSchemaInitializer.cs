using MAFRagService.Stubs.NebulaGraph;

﻿namespace MAFRagService.Initializers;

public class GraphSchemaInitializer
{
    private readonly NebulaGraphClient _client;
    private readonly ILogger<GraphSchemaInitializer> _logger;

    public GraphSchemaInitializer(NebulaGraphClient client, ILogger<GraphSchemaInitializer> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        var createSpace = @"
            CREATE SPACE IF NOT EXISTS rag_space (
                vid_type = FIXED_STRING(128),
                partition_num = 1,
                replica_factor = 1
            );
        ";
        await ExecuteAsync(createSpace, ct);
        await Task.Delay(2000, ct);

        await _client.ChangeSpaceAsync("rag_space", ct);

        var tags = @"
            CREATE TAG IF NOT EXISTS Document (
                doc_id string, tenant string, version string, file_name string, uploaded_at timestamp
            );
            CREATE TAG IF NOT EXISTS Entity (
                name string, type string, tenant string, created_at timestamp
            );
            CREATE TAG IF NOT EXISTS Chunk (
                chunk_id string, doc_id string, content string, tenant string, index int
            );
            CREATE TAG IF NOT EXISTS Conversation (
                question string, tenant string, timestamp timestamp
            );
            CREATE TAG IF NOT EXISTS MemoryEntity (
                name string, tenant string, timestamp timestamp
            );
        ";
        await ExecuteAsync(tags, ct);

        var edges = @"
            CREATE EDGE IF NOT EXISTS MENTIONS (weight double, timestamp timestamp);
            CREATE EDGE IF NOT EXISTS CO_OCCURS_WITH (count int, timestamp timestamp);
            CREATE EDGE IF NOT EXISTS BELONGS_TO (timestamp timestamp);
            CREATE EDGE IF NOT EXISTS HAS_CHUNK (order int);
        ";
        await ExecuteAsync(edges, ct);

        var indexes = @"
            CREATE TAG INDEX IF NOT EXISTS idx_entity_name ON Entity(name(64));
            CREATE TAG INDEX IF NOT EXISTS idx_doc_tenant ON Document(tenant(32));
            CREATE TAG INDEX IF NOT EXISTS idx_chunk_doc ON Chunk(doc_id(64));
            CREATE EDGE INDEX IF NOT EXISTS idx_mentions_timestamp ON MENTIONS(timestamp);
        ";
        await ExecuteAsync(indexes, ct);

        _logger.LogInformation("NebulaGraph schema initialized");
    }

    private async Task ExecuteAsync(string ngql, CancellationToken ct)
    {
        var result = await _client.ExecuteAsync(ngql, ct);
        if (!result.IsSucceeded && !result.ErrorMessage.Contains("already exists"))
            throw new Exception($"Nebula init error: {result.ErrorMessage}");
    }
}
