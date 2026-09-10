using MAFRagService.Startup.Diagnostics;
using Npgsql;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MAFRagService.Startup.Extensions;

/// <summary>
/// OpenTelemetry 追踪注册。
/// - 资源名固定为 "MAFRagServer"，可在 OTLP 后端按 service.name 过滤。
/// - 数据源：
///     · AspNetCore / HttpClient 自动埋点
///     · Npgsql SQL 客户端埋点
///     · 自定义 <see cref="MAFRagActivity.Source"/>（Hangfire / Nebula 手动 span）
/// - 导出：OTLP gRPC，端点取自 OTEL_EXPORTER_OTLP_ENDPOINT 或本地 4317。
/// </summary>
public static class ObservabilityServiceCollectionExtensions
{
    private const string DefaultOtlpEndpoint = "http://localhost:4317";
    private const string ServiceName         = "MAFRagServer";

    public static IServiceCollection AddRagObservability(
        this IServiceCollection services,
        IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        var endpoint = config["OTEL_EXPORTER_OTLP_ENDPOINT"];
        if (string.IsNullOrWhiteSpace(endpoint))
            endpoint = DefaultOtlpEndpoint;

        services.AddOpenTelemetry()
            .ConfigureResource(r => r
                .AddService(ServiceName)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] =
                        config["ASPNETCORE_ENVIRONMENT"] ?? "Unknown"
                }))
            .WithTracing(tracer => tracer
                // 自定义 ActivitySource（Hangfire / Nebula 等）
                .AddSource(MAFRagActivity.Name)

                // 自动埋点
                .AddAspNetCoreInstrumentation(opt =>
                {
                    opt.RecordException = true;
                })
                .AddHttpClientInstrumentation(opt =>
                {
                    opt.RecordException = true;
                })
                .AddNpgsql()

                // OTLP 导出
                .AddOtlpExporter(opt =>
                {
                    opt.Endpoint = new Uri(endpoint);
                    opt.Protocol = OtlpExportProtocol.Grpc;
                }));

        return services;
    }
}