using Elastic.Clients.Elasticsearch;

namespace ElasticsearchService.Services;

public class ElasticsearchIndexInitializer(ElasticsearchClient client,
    ILogger<ElasticsearchIndexInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ElasticsearchIndexSetup.EnsureIndexAsync(client);
            logger.LogInformation("Elasticsearch 索引初始化完成");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Elasticsearch 索引初始化失败");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}