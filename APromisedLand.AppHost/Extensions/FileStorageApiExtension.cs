using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace APromisedLand.AppHost.Extensions;

public static class FileStorageApiExtension
{
    private const int FileStorageApiHttpPort = 5325;

    public static IDistributedApplicationBuilder AddFileStorageApi(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        resourceContext.FileStorageApi = builder
            .AddProject<Projects.FileStorageApi>("FileStorageApi")
            .WithHttpEndpoint(port: FileStorageApiHttpPort, name: "http");

        // ── 1. 数据库：注入 FileMetadataDb ──
        // 与 Program.cs 中 AddNpgsqlDbContext<FileStorageContext>("FileMetadataDb") 对应，
        // Aspire 会注入 ConnectionStrings__FileMetadataDb。
        if (resourceContext.FileMetadataDb is not null)
        {
            resourceContext.FileStorageApi
                .WithReference(resourceContext.FileMetadataDb)
                .WaitFor(resourceContext.FileMetadataDb);
        }

        // ── 2. SeaweedS3：容器资源，无连接字符串 → 只 WaitFor ──
        if (resourceContext.SeaweedS3 is not null)
        {
            resourceContext.FileStorageApi.WaitFor(resourceContext.SeaweedS3);
        }

        // ── 3. SeaweedFS 配置：显式注入环境变量 ──
        if (resourceContext.SeaweedS3 is not null)
        {
            resourceContext.FileStorageApi
                .WithEnvironment("SeaweedFS__Endpoint",
                    resourceContext.SeaweedS3.GetEndpoint("s3")
                        .Property(EndpointProperty.Url))
                .WithEnvironment("SeaweedFS__AccessKey", "admin")
                .WithEnvironment("SeaweedFS__SecretKey", "admin")
                .WithEnvironment("SeaweedFS__Bucket", "documents")
                .WithEnvironment("SeaweedFS__ForcePathStyle", "true")
                .WithEnvironment("SeaweedFS__UseHttp", "true");
        }

        // ── 4. 上传清理调度参数 ──
        // 原 Database__AutoMigrate / Database__FailFast 已删除：
        // Program.cs 未读取这两个键，属于死配置；迁移在 Development 下无条件执行。
        resourceContext.FileStorageApi
            .WithEnvironment("UploadCleanup__Enabled", "true")
            .WithEnvironment("UploadCleanup__IntervalMinutes", "5")
            .WithEnvironment("UploadCleanup__StartupDelayMinutes", "0")
            .WithEnvironment("UploadCleanup__StaleMergingMinutes", "5")
            .WithEnvironment("UploadCleanup__StalePendingMinutes", "10")
            .WithEnvironment("UploadCleanup__CompletedRetentionMinutes", "10080"); // 7 天

        // ── 5. 健康检查 + OTLP ──
        resourceContext.FileStorageApi
            .WithHttpHealthCheck("/health", 200, "http")
            .WithOtlpExporter();

        return builder;
    }
}