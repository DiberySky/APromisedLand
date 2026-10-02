namespace APromisedLand.AppHost.Extensions;

public static class PostgresExtension
{
    public static IDistributedApplicationBuilder AddPostgres(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        resourceContext.Postgres = builder.AddPostgres("Postgres", port: 8433)
            .WithDataVolume("postgres-data")
            // 容器跨 AppHost 重启保留（默认 Session 会在停止时删除容器、重建并改名）。
            // 持久卷 postgres-data 本就固定；此项让容器本身也稳定，避免每次重启重建/重跑 seed。
            // 注意：若日后修改端口/镜像映射后不生效，需手动 docker rm -f 旧容器。
            .WithLifetime(ContainerLifetime.Persistent)
            .WithPgAdmin(pg => pg.WithHostPort(15050));

        resourceContext.TreeDb        = resourceContext.Postgres.AddDatabase("TreeDb");
        resourceContext.TreeGraphDb   = resourceContext.Postgres.AddDatabase("TreeGraphDb");
        resourceContext.FileTransDb   = resourceContext.Postgres.AddDatabase("fileTransDb");
        resourceContext.MetadataDb    = resourceContext.Postgres.AddDatabase("MetadataDb");
        resourceContext.HangfireDb    = resourceContext.Postgres.AddDatabase("HangfireDb");
        resourceContext.FileMetadataDb= resourceContext.Postgres.AddDatabase("FileMetadataDb");
        resourceContext.VectorAdminDb = resourceContext.Postgres.AddDatabase("vectorAdminDb");

        // 注：FileStorageDb 已移除。
        // FileStorageApi 的 DbContext 连接名统一为 FileMetadataDb（见 Program.cs）。
        return builder;
    }
}