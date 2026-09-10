using Amazon.Runtime;
using Amazon.S3;
using APromisedLand.Api.Data;
using Hangfire;
using Hangfire.PostgreSql;
using MAFRagService.Services;
using MAFRagService.Startup.Configuration;
using MAFRagService.Startup.Diagnostics;
using MAFRagService.Stubs.NebulaGraph;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MAFRagService.Startup.Extensions;

/// <summary>
/// 基础设施层服务注册：Hangfire / EF Core / Redis / NebulaGraph / SeaweedFS(S3)。
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddRagInfrastructure(
        this IServiceCollection services,
        ConnectionStrings conns,
        FeatureFlags features)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(conns);
        ArgumentNullException.ThrowIfNull(features);

        AddHangfire(services, conns);
        AddEfCore(services, conns);
        AddRedis(services, conns);

        if (features.Graph)
            AddNebulaGraph(services, conns);

        AddSeaweedFs(services, conns);

        return services;
    }

    // ------------------------------------------------------------
    // Hangfire
    // ------------------------------------------------------------
    private static void AddHangfire(IServiceCollection services, ConnectionStrings conns)
    {
        services.AddHangfire(cfg => cfg
            .UsePostgreSqlStorage(
                opt => opt.UseNpgsqlConnection(conns.HangfireDb),
                new PostgreSqlStorageOptions
                {
                    PrepareSchemaIfNecessary = true,
                    QueuePollInterval        = TimeSpan.FromSeconds(15)
                })
            // ★ 分布式追踪：每个 job 生成一个 Consumer Span
            .UseFilter(new HangfireTelemetryFilter()));

        services.AddHangfireServer(opt =>
        {
            opt.WorkerCount = Environment.ProcessorCount * 2;
            opt.Queues      = new[] { "default", "indexing", "embedding", "graph", "entity" };
        });
    }

    // ------------------------------------------------------------
    // EF Core
    // ------------------------------------------------------------
    private static void AddEfCore(IServiceCollection services, ConnectionStrings conns)
    {
        services.AddDbContextPool<MafRagContext>(opt =>
            opt.UseNpgsql(conns.MetadataDb));
    }

    // ------------------------------------------------------------
    // Redis
    // ------------------------------------------------------------
    private static void AddRedis(IServiceCollection services, ConnectionStrings conns)
    {
        services.AddStackExchangeRedisCache(opt =>
        {
            opt.Configuration = conns.Redis;
            opt.InstanceName  = "MAFRag_";
        });
    }

    // ------------------------------------------------------------
    // NebulaGraph
    // ------------------------------------------------------------
    private static void AddNebulaGraph(IServiceCollection services, ConnectionStrings conns)
    {
        services.AddSingleton(_ =>
            new NebulaGraphClient(NebulaGraphOptions.FromConnectionString(conns.Nebula)));
        services.AddSingleton<NebulaGraphExecutor>();
    }

    // ------------------------------------------------------------
    // SeaweedFS (S3 兼容)
    // ------------------------------------------------------------
    private static void AddSeaweedFs(IServiceCollection services, ConnectionStrings conns)
    {
        services.AddSingleton<IAmazonS3>(sp =>
        {
            var opt    = sp.GetRequiredService<IOptions<SeaweedFsOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<Program>>();

            var s3Config = new AmazonS3Config
            {
                ServiceURL           = conns.SeaweedFs,
                ForcePathStyle       = true,
                AuthenticationRegion = opt.Region
            };

            // ★ 端点脱敏后输出，防止 userinfo / query 泄漏
            logger.LogInformation(
                "SeaweedFS S3 endpoint={Endpoint}, PathStyle={PathStyle}, Region={Region}",
                Sanitizer.Url(s3Config.ServiceURL),
                s3Config.ForcePathStyle,
                opt.Region);

            return new AmazonS3Client(
                new BasicAWSCredentials(opt.AccessKey, opt.SecretKey),
                s3Config);
        });
    }
}