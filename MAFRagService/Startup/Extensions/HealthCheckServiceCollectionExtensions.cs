using MAFRagService.Startup.Configuration;
using MAFRagService.Startup.HealthChecks;
using Microsoft.Extensions.DependencyInjection;              // ★ 新增：IServiceCollection / AddHealthChecks
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MAFRagService.Startup.Extensions;

/// <summary>
/// 健康检查注册。
/// 标签约定：
///   · "ready"  → 就绪探针候选（K8s readinessProbe 使用）
///   · "live"   → 存活探针候选（当前由 /health/live 无检查兜底）
///   · "db" / "cache" / "ai" / "graph" → 领域标签，便于按子系统切分
///
/// 说明：
///   · postgres / redis / weaviate 属于关键依赖，未启用 FeatureFlags 时依然注册
///     （因为这些服务在应用启动阶段被硬依赖）。
///   · nebula 仅在 Features:Graph=true 时注册，避免关闭图模块后探针报红。
/// </summary>
public static class HealthCheckServiceCollectionExtensions
{
    public static IServiceCollection AddRagHealthChecks(
        this IServiceCollection services,
        FeatureFlags features)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(features);

        var builder = services
            .AddHealthChecks()

            // ---------- 关系型数据库 ----------
            .AddCheck<PostgresHealthCheck>(
                name: "postgres",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "db", "ready" })

            // ---------- 缓存 ----------
            .AddCheck<RedisHealthCheck>(
                name: "redis",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "cache", "ready" })

            // ---------- 模型服务 ----------
            .AddCheck<OllamaHealthCheck>(
                name: "ollama",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "ai" })

            // ---------- 向量库 ----------
            .AddCheck<WeaviateHealthCheck>(
                name: "weaviate",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "ai", "ready" });

        // ---------- 图数据库（可选） ----------
        if (features.Graph)
        {
            builder.AddCheck<NebulaHealthCheck>(
                name: "nebula",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "graph", "ready" });
        }

        return services;
    }
}