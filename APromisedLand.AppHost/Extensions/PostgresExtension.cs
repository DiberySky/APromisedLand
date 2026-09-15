namespace APromisedLand.AppHost.Extensions;

public static class PostgresExtension
{
    public static IDistributedApplicationBuilder AddPostgres(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        resourceContext.Postgres = builder.AddPostgres("Postgres", port: 8433)
            .WithDataVolume("postgres-data")
            .WithPgAdmin(pg => pg.WithHostPort(15050));

        resourceContext.TreeDb        = resourceContext.Postgres.AddDatabase("TreeDb");
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