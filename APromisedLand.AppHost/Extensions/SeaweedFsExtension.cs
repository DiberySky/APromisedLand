using System.IO;

namespace APromisedLand.AppHost.Extensions;

public static class SeaweedFsExtension
{
    private const string Image = "chrislusf/seaweedfs:4.46";

    private static string ConfigPath(string fileName)
        => Path.Combine(AppContext.BaseDirectory, "Configs", fileName);

    public static IDistributedApplicationBuilder AddSeaweedFs(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        // 1. Master
        resourceContext.SeaweedMaster = builder.AddContainer("seaweedfs-master", Image)
            .WithArgs(
                "master",
                "-ip=seaweedfs-master",
                "-mdir=/data",
                "-port=9333",
                "-defaultReplication=000"
            )
            .WithHttpEndpoint(name: "http", targetPort: 9333)
            .WithVolume("seaweedfs-master-data", "/data");

        // 2. Volume
        resourceContext.SeaweedVolume = builder.AddContainer("seaweedfs-volume", Image)
            .WithArgs(
                "volume",
                "-mserver=seaweedfs-master:9333",
                "-ip=seaweedfs-volume",
                "-port=8080",
                "-dir=/data",
                "-max=0"
            )
            .WithHttpEndpoint(name: "http", targetPort: 8080)
            .WithVolume("seaweedfs-volume-data", "/data")
            .WaitFor(resourceContext.SeaweedMaster);

        // 3. Filer
        resourceContext.SeaweedFiler = builder.AddContainer("seaweedfs-filer", Image)
            .WithArgs(
                "filer",
                "-master=seaweedfs-master:9333",
                "-ip=seaweedfs-filer",
                "-port=8888",
                "-defaultStoreDir=/data"
            )
            .WithHttpEndpoint(name: "http", targetPort: 8888)
            .WithVolume("seaweedfs-filer-data", "/data")
            .WaitFor(resourceContext.SeaweedMaster)
            .WaitFor(resourceContext.SeaweedVolume);

        // 4. S3 网关
        var s3ConfigPath = ConfigPath("seaweedfs-s3.json");

        if (!File.Exists(s3ConfigPath))
        {
            throw new FileNotFoundException(
                $"SeaweedFS S3 配置文件缺失：{s3ConfigPath}。" +
                "请确认 APromisedLand.AppHost.csproj 中已配置 " +
                "<Content Include=\"Configs\\**\\*\" /> 并执行 dotnet build。",
                s3ConfigPath);
        }

        // S3_EXTERNAL_URL 可配置：配置 "SeaweedFS:S3ExternalUrl" > 默认 localhost:8333
        var s3ExternalUrl = builder.Configuration["SeaweedFS:S3ExternalUrl"]
            ?? "http://localhost:8333";

        resourceContext.SeaweedS3 = builder.AddContainer("seaweedfs-s3", Image)
            .WithArgs(
                "s3",
                "-filer=seaweedfs-filer:8888",
                "-port=8333",
                "-config=/etc/seaweedfs/s3.json"
            )
            .WithBindMount(
                source: s3ConfigPath,
                target: "/etc/seaweedfs/s3.json",
                isReadOnly: true)
            .WithEnvironment("S3_EXTERNAL_URL", s3ExternalUrl)
            .WithHttpEndpoint(
                name: "s3",
                targetPort: 8333,
                port: 8333,
                isProxied: false)
            .WaitFor(resourceContext.SeaweedFiler);

        return builder;
    }
}