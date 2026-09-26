using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace APromisedLand.AppHost.Extensions;

public static class FileStorageApiExtension
{
    // ── 端口 ──
    private const int FileStorageApiHttpPort = 5325;

    // ── 上传清理默认值（分钟） ──
    // 这些值是"调试友好"的：给迁移 / 健康检查留出窗口，
    // 同时把 DB 噪音压到最低，避免调试器被后台异常打断。
    private const int CleanupIntervalMinutes      = 15;
    private const int CleanupStartupDelayMinutes  = 2;
    private const int StaleMergingMinutes         = 10;
    private const int StalePendingMinutes         = 15;
    private const int CompletedRetentionMinutes   = 7 * 24 * 60; // 7 天

    // ── 环境变量名（集中管理，避免散落字符串） ──
    private const string EnvCleanupDisabled = "FSA_CLEANUP_DISABLED";
    private const string EnvOtlpEnabled     = "FSA_OTLP_ENABLED";

    public static IDistributedApplicationBuilder AddFileStorageApi(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        var api = builder
            .AddProject<Projects.FileStorageApi>("FileStorageApi")
            .WithHttpEndpoint(port: FileStorageApiHttpPort, name: "http");

        ConfigureDatabase(api, resourceContext);
        ConfigureObjectStorage(api, resourceContext);
        ConfigureUploadCleanup(api);
        ConfigureObservability(api);

        resourceContext.FileStorageApi = api;
        return builder;
    }

    // ══════════════════════════════════════════════════════════
    // 1. 数据库：注入 FileMetadataDb
    //    与 Program.cs 中 AddNpgsqlDbContext<FileStorageContext>("FileMetadataDb")
    //    对应，Aspire 会注入 ConnectionStrings__FileMetadataDb。
    // ══════════════════════════════════════════════════════════
    private static void ConfigureDatabase(
        IResourceBuilder<ProjectResource> api,
        AppHostResourceContext ctx)
    {
        if (ctx.FileMetadataDb is null) return;

        api.WithReference(ctx.FileMetadataDb)
           .WaitFor(ctx.FileMetadataDb);
    }

    // ══════════════════════════════════════════════════════════
    // 2. SeaweedS3：容器资源，无连接字符串 → WaitFor + 显式 env
    // ══════════════════════════════════════════════════════════
    private static void ConfigureObjectStorage(
        IResourceBuilder<ProjectResource> api,
        AppHostResourceContext ctx)
    {
        if (ctx.SeaweedS3 is null) return;

        var s3Endpoint = ctx.SeaweedS3
            .GetEndpoint("s3")
            .Property(EndpointProperty.Url);

        api.WaitFor(ctx.SeaweedS3)
           .WithEnvironment("SeaweedFS__Endpoint",       s3Endpoint)
           .WithEnvironment("SeaweedFS__AccessKey",      "admin")
           .WithEnvironment("SeaweedFS__SecretKey",      "admin")
           .WithEnvironment("SeaweedFS__Bucket",         "documents")
           .WithEnvironment("SeaweedFS__ForcePathStyle", "true")
           .WithEnvironment("SeaweedFS__UseHttp",        "true");
    }

    // ══════════════════════════════════════════════════════════
    // 3. 上传清理调度参数
    //
    // ★ 为什么调参：
    //   1) StartupDelayMinutes=0 会让清理服务与 EF 迁移、健康检查
    //      Publisher 在同一时刻抢占 Npgsql 连接池，调试器下极易触发
    //      BreakForUserUnhandledException。
    //   2) IntervalMinutes=5 让 DB 日志噪音累积得太快。
    //
    // ★ 本地完全关掉清理：
    //   设环境变量 FSA_CLEANUP_DISABLED=true（AppHost 进程级即可）。
    // ══════════════════════════════════════════════════════════
    private static void ConfigureUploadCleanup(
        IResourceBuilder<ProjectResource> api)
    {
        var disabled = IsTruthy(Environment.GetEnvironmentVariable(EnvCleanupDisabled));

        api.WithEnvironment("UploadCleanup__Enabled",
                                disabled ? "false" : "true")
           .WithEnvironment("UploadCleanup__IntervalMinutes",
                                CleanupIntervalMinutes.ToString())
           .WithEnvironment("UploadCleanup__StartupDelayMinutes",
                                CleanupStartupDelayMinutes.ToString())
           .WithEnvironment("UploadCleanup__StaleMergingMinutes",
                                StaleMergingMinutes.ToString())
           .WithEnvironment("UploadCleanup__StalePendingMinutes",
                                StalePendingMinutes.ToString())
           .WithEnvironment("UploadCleanup__CompletedRetentionMinutes",
                                CompletedRetentionMinutes.ToString());
    }

    // ══════════════════════════════════════════════════════════
    // 4. 健康检查 + OTLP
    //
    // ★ 为什么有条件挂 OTLP：
    //   WithOtlpExporter 会注册后台 gRPC 导出循环。Aspire Dashboard
    //   未就绪/重启时该循环抛 TaskCanceledException，被 .NET 9+ 新
    //   的 BreakForUserUnhandledException 捕获并打断调试器。
    //
    // ★ 默认策略：
    //   - 生产（Dashboard 常驻）→ 默认开
    //   - 本地调试（Debugger.IsAttached 或 Development）→ 默认关
    //   - 手动覆盖：FSA_OTLP_ENABLED=true / false
    // ══════════════════════════════════════════════════════════
    private static void ConfigureObservability(
        IResourceBuilder<ProjectResource> api)
    {
        api.WithHttpHealthCheck("/health", 200, "http");

        if (ShouldEnableOtlp())
        {
            api.WithOtlpExporter();
        }
    }

    private static bool ShouldEnableOtlp()
    {
        // 显式覆盖优先
        var overrideValue = Environment.GetEnvironmentVariable(EnvOtlpEnabled);
        if (!string.IsNullOrWhiteSpace(overrideValue))
        {
            return IsTruthy(overrideValue);
        }

        // 默认：调试会话关闭，其它场景打开
        return !IsDebugSession();
    }

    private static bool IsDebugSession() =>
        System.Diagnostics.Debugger.IsAttached ||
        string.Equals(
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"),
            "Development",
            StringComparison.OrdinalIgnoreCase);

    // ── 通用：布尔字符串判断 ──
    private static bool IsTruthy(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        (value.Equals("1",    StringComparison.OrdinalIgnoreCase) ||
         value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
         value.Equals("yes",  StringComparison.OrdinalIgnoreCase) ||
         value.Equals("on",   StringComparison.OrdinalIgnoreCase));
}