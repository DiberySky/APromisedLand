namespace APromisedLand.AppHost.Extensions;

public static class SeaweedFsExtensions
{
    public static IDistributedApplicationBuilder AddSeaweedFs(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        if (context.FileTransDb is null) return builder;

        // SeaweedFS Master
        context.SeaweedMaster = builder.AddContainer("seaweedfs-master", "chrislusf/seaweedfs")
            .WithArgs("master", "-ip=seaweedfs-master", "-port=9333")
            .WithHttpEndpoint(name: "http", port: 9333, targetPort: 9333)
            .WithOtlpExporter();

        // Volume
        context.SeaweedVolume = builder.AddContainer("seaweedfs-volume", "chrislusf/seaweedfs")
            .WithArgs("volume", "-mserver=seaweedfs-master:9333", "-port=8080", "-dir=/data")
            .WithHttpEndpoint(name: "http", port: 8080, targetPort: 8080)
            .WithVolume("seaweedfs-volume-data", "/data")
            .WithOtlpExporter()
            .WaitFor(context.SeaweedMaster);

        // Filer
        context.SeaweedFiler = builder.AddContainer("seaweedfs-filer", "chrislusf/seaweedfs")
            .WithArgs("filer", "-master=seaweedfs-master:9333")
            .WithHttpEndpoint(name: "http", port: 8888, targetPort: 8888)
            .WithOtlpExporter()
            .WaitFor(context.SeaweedMaster)
            .WaitFor(context.SeaweedVolume);

        // FileTrans Service
        context.FileTransService = builder.AddProject<Projects.FileTransService>("SeaweedFS-Service")
            .WithReference(context.FileTransDb)
            .WithEnvironment("SeaweedFS__BaseUrl", context.SeaweedFiler.GetEndpoint("http"))
            .WithEnvironment("SeaweedFS__MasterUrl", context.SeaweedMaster.GetEndpoint("http"))
            .WithEnvironment("SeaweedFS__VolumeUrl", context.SeaweedVolume.GetEndpoint("http"))
            .WaitFor(context.FileTransDb)
            .WithOtlpExporter();

        return builder;
    }
}