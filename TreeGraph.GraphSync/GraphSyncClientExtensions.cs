using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TreeGraph.GraphSync;

/// <summary>两个 Web 宿主共用的 Nebula REST 客户端注册。</summary>
public static class GraphSyncClientExtensions
{
    public static IServiceCollection AddNebulaGraphClient(
        this IServiceCollection services, IConfiguration config)
    {
        var options = config
            .GetSection(GraphSyncOptions.SectionName)
            .Get<GraphSyncOptions>() ?? new GraphSyncOptions();
        var endpoint = config["NebulaFastApi:Endpoint"] ?? options.NebulaApiEndpoint;

        services.AddOptions<GraphSyncOptions>()
            .Bind(config.GetSection(GraphSyncOptions.SectionName));
        services.AddHttpClient<NebulaGraphClient>(client =>
        {
            client.BaseAddress = new Uri(endpoint);
            client.Timeout = TimeSpan.FromSeconds(120);
        });

        return services;
    }
}
