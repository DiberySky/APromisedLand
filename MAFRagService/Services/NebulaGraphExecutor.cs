using MAFRagService.Stubs.NebulaGraph;
using Polly;
using Polly.Retry;

namespace MAFRagService.Services;

public class NebulaGraphExecutor
{
    private readonly NebulaGraphClient _client;
    private readonly AsyncRetryPolicy<ResultSet> _retryPolicy;
    private readonly ILogger<NebulaGraphExecutor> _logger;

    public NebulaGraphExecutor(NebulaGraphClient client, ILogger<NebulaGraphExecutor> logger)
    {
        _client = client;
        _logger = logger;

        _retryPolicy = Policy<ResultSet>
            .Handle<Exception>(ex => ex is not OperationCanceledException)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retry => TimeSpan.FromSeconds(Math.Pow(2, retry)),
                onRetry: (outcome, delay, attempt, context) =>
                {
                    _logger.LogWarning("NebulaGraph retry {Attempt} due to {Error}", attempt, outcome.Exception?.Message);
                });
    }

    public async Task<ResultSet> ExecuteWithRetryAsync(string ngql, CancellationToken ct = default)
    {
        return await _retryPolicy.ExecuteAsync(async (token) =>
        {
            return await _client.ExecuteAsync(ngql, token);
        }, ct);
    }
}
