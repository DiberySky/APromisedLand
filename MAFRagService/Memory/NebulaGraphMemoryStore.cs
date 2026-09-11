using MAFRagService.Services;
using MAFRagService.Startup.Configuration;
using MAFRagService.Stubs.MAF;
using Microsoft.Extensions.Options;

namespace MAFRagService.Memory;

public interface INebulaGraphMemoryStore : IMemoryStore
{
    Task SaveConversationAsync(ConversationMemory memory);
}

public class ConversationMemory
{
    public string ConversationId { get; set; } = Guid.NewGuid().ToString();
    public string Question { get; set; } = string.Empty;
    public List<string> Entities { get; set; } = new();
    public string Tenant { get; set; } = "default";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public List<string> Sources { get; set; } = new();
}

public class NebulaGraphMemoryStore : INebulaGraphMemoryStore
{
    private readonly NebulaGraphExecutor _executor;
    private readonly string _space;
    private readonly ILogger<NebulaGraphMemoryStore> _logger;

    public NebulaGraphMemoryStore(
        NebulaGraphExecutor executor,
        IOptions<NebulaGraphAppOptions> options,
        ILogger<NebulaGraphMemoryStore> logger)
    {
        _executor = executor;
        _space    = options.Value.Space;
        _logger   = logger;
    }

    public async Task SaveConversationAsync(ConversationMemory memory)
    {
        await _executor.ExecuteWithRetryAsync($"USE {_space}");
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var convId = memory.ConversationId;

        var insertConv = $@"
            INSERT VERTEX Conversation (question, tenant, timestamp)
            VALUES ""{Esc(convId)}"": (""{Esc(memory.Question)}"", ""{Esc(memory.Tenant)}"", {ts})
        ";
        await _executor.ExecuteWithRetryAsync(insertConv);

        foreach (var entity in memory.Entities)
        {
            var insertEntity = $@"
                INSERT VERTEX MemoryEntity (name, tenant, timestamp)
                VALUES ""{Esc(entity)}"": (""{Esc(entity)}"", ""{Esc(memory.Tenant)}"", {ts})
            ";
            await _executor.ExecuteWithRetryAsync(insertEntity);

            var edge = $@"
                INSERT EDGE MENTIONS (timestamp)
                VALUES ""{Esc(convId)}"" -> ""{Esc(entity)}"" @ ({ts})
            ";
            await _executor.ExecuteWithRetryAsync(edge);
        }

        _logger.LogInformation("Saved conversation memory: {ConvId}", convId);
    }

    public Task<IEnumerable<MemoryEntry>> QueryAsync(MemoryQuery query, CancellationToken ct)
        => Task.FromResult<IEnumerable<MemoryEntry>>(Array.Empty<MemoryEntry>());

    public Task AddMemoryAsync(MemoryEntry entry, CancellationToken ct)
        => throw new NotSupportedException(
            "NebulaGraphMemoryStore 仅支持会话级记忆写入（SaveConversationAsync），" +
            "通用 MemoryEntry 写入未启用。");

    private static string Esc(string? s) =>
        (s ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
}