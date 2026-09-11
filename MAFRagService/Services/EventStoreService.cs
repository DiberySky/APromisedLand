using System.Text.Json;
using APromisedLand.Api.Data;
using APromisedLand.Api.MafRag.Entities;
using Microsoft.EntityFrameworkCore;

namespace MAFRagService.Services;

public sealed record DomainEventDto(
    Guid Id,
    string StreamId,
    string EventType,
    JsonElement? Data,
    int Version,
    string? Tenant,
    DateTime Timestamp);

public class EventStoreService
{
    private readonly MafRagContext _dbContext;
    private readonly ILogger<EventStoreService> _logger;

    public EventStoreService(MafRagContext dbContext, ILogger<EventStoreService> logger)
    {
        _dbContext = dbContext;
        _logger    = logger;
    }

    public async Task AppendEventAsync<T>(
        string streamId, T @event, string? tenant, CancellationToken ct) where T : class
    {
        var entity = new DomainEventEntity
        {
            StreamId  = streamId,
            EventType = typeof(T).Name,
            EventData = JsonSerializer.Serialize(@event),
            Tenant    = tenant,
            Version   = await GetNextVersionAsync(streamId, ct)
        };
        _dbContext.DomainEvents.Add(entity);
        await _dbContext.SaveChangesAsync(ct);
        _logger.LogDebug("Appended event {EventType} for stream {StreamId} v{Version}",
            entity.EventType, streamId, entity.Version);
    }

    private async Task<int> GetNextVersionAsync(string streamId, CancellationToken ct)
    {
        var max = await _dbContext.DomainEvents
            .Where(e => e.StreamId == streamId)
            .MaxAsync(e => (int?)e.Version, ct) ?? 0;
        return max + 1;
    }

    // ============================================================
    // ★ 修改：强类型 DTO 返回，消除反射；Tenant 从列直接读 + 可选过滤
    // ============================================================
    public async Task<IReadOnlyList<DomainEventDto>> GetEventsAsync(
        string streamId, string? tenant, CancellationToken ct)
    {
        var query = _dbContext.DomainEvents.Where(e => e.StreamId == streamId);
        if (!string.IsNullOrEmpty(tenant))
            query = query.Where(e => e.Tenant == tenant);

        var list = await query
            .OrderBy(e => e.Version)
            .ToListAsync(ct);

        return list.Select(e => new DomainEventDto(
            e.Id, e.StreamId, e.EventType,
            TryParse(e.EventData),
            e.Version, e.Tenant, e.Timestamp)).ToList();
    }

    private static JsonElement? TryParse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch { return null; }
    }
}