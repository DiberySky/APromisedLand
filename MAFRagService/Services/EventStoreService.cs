using System.Text.Json;
using APromisedLand.Api.Data;
using APromisedLand.Api.MafRag.Entities;
using Microsoft.EntityFrameworkCore;

namespace MAFRagService.Services;

public class EventStoreService
{
    private readonly MafRagContext _dbContext;
    private readonly ILogger<EventStoreService> _logger;

    public EventStoreService(MafRagContext dbContext, ILogger<EventStoreService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task AppendEventAsync<T>(string streamId, T @event, string? tenant, CancellationToken ct) where T : class
    {
        var entity = new DomainEventEntity
        {
            StreamId = streamId,
            EventType = typeof(T).Name,
            EventData = JsonSerializer.Serialize(@event),
            Tenant = tenant,
            Version = await GetNextVersionAsync(streamId, ct)
        };
        await _dbContext.DomainEvents.AddAsync(entity, ct);
        await _dbContext.SaveChangesAsync(ct);
        _logger.LogDebug("Appended event {EventType} for stream {StreamId} v{Version}", entity.EventType, streamId, entity.Version);
    }

    private async Task<int> GetNextVersionAsync(string streamId, CancellationToken ct)
    {
        var max = await _dbContext.DomainEvents
            .Where(e => e.StreamId == streamId)
            .MaxAsync(e => (int?)e.Version, ct) ?? 0;
        return max + 1;
    }

    public async Task<IEnumerable<object>> GetEventsAsync(string streamId, CancellationToken ct)
    {
        var events = await _dbContext.DomainEvents
            .Where(e => e.StreamId == streamId)
            .OrderBy(e => e.Version)
            .ToListAsync(ct);
        return events.Select(e => JsonSerializer.Deserialize<object>(e.EventData)!);
    }
}
