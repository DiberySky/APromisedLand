namespace APromisedLand.AppHost.Extensions;

public static class SeaweedFsExtension
{
    // ★ 固定镜像版本，避免 :latest 漂移导致行为变化
    //   如需升级，改这一处即可
    private const string Image = "chrislusf/seaweedfs:3.71";

    public static IDistributedApplicationBuilder AddSeaweedFs(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        // if (context.FileTransDb is null) return builder;

        // ========== Master ==========
        // Master 保存集群拓扑（volume 位置、filer 注册等）
        context.SeaweedMaster = builder.AddContainer("seaweedfs-master", Image)
            .WithArgs(
                "master",
                "-ip=seaweedfs-master",       // ★ 显式自注册名，避免容器重建后 IP 变化
                "-port=9333",
                "-mdir=/data",                // ★ master 元数据目录
                "-defaultReplication=000"     // 单节点开发环境：不复制
            )
            // ★ 只声明 targetPort，宿主机端口由 Aspire 自动分配，避免 9333 冲突
            .WithHttpEndpoint(name: "http", targetPort: 9333)
            // ★ 持久化 master 元数据，容器重建后 topology 不丢
            .WithVolume("seaweedfs-master-data", "/data")
            .WithOtlpExporter();

        // ========== Volume ==========
        // Volume 负责实际的数据块存储
        context.SeaweedVolume = builder.AddContainer("seaweedfs-volume", Image)
            .WithArgs(
                "volume",
                "-mserver=seaweedfs-master:9333",
                "-ip=seaweedfs-volume",       // ★ 与 master 保持一致，显式声明
                "-port=8080",
                "-dir=/data",
                "-max=0"                      // 0 = 不限制磁盘用量（开发环境）
            )
            .WithHttpEndpoint(name: "http", targetPort: 8080)
            // ★ 持久化数据块，否则容器一重建所有文件内容丢失
            .WithVolume("seaweedfs-volume-data", "/data")
            .WithOtlpExporter()
            .WaitFor(context.SeaweedMaster);

        // ========== Filer ==========
        // Filer 提供 POSIX 风格的文件系统语义（目录、文件名 → 文件 ID 映射）
        context.SeaweedFiler = builder.AddContainer("seaweedfs-filer", Image)
            .WithArgs(
                "filer",
                "-master=seaweedfs-master:9333",
                "-ip=seaweedfs-filer",        // ★ 显式声明
                "-port=8888",
                "-defaultStoreDir=/data"      // ★ filer 元数据落盘目录
            )
            .WithHttpEndpoint(name: "http", targetPort: 8888)
            // ★ 持久化 filer 元数据，容器重建后文件目录结构不丢
            .WithVolume("seaweedfs-filer-data", "/data")
            .WithOtlpExporter()
            .WaitFor(context.SeaweedMaster)
            .WaitFor(context.SeaweedVolume);

        // ========== FileTrans Service（暂未启用） ==========
        // context.FileTransService = builder.AddProject<Projects.FileTransService>("seaweedfs-service")
        //     .WithReference(context.FileTransDb)
        //     // ★ 改用 EndpointReferenceExpression，自动跟随端口变化
        //     .WithEnvironment("SeaweedFS__BaseUrl",   context.SeaweedFiler.GetEndpoint("http"))
        //     .WithEnvironment("SeaweedFS__MasterUrl", context.SeaweedMaster.GetEndpoint("http"))
        //     .WithEnvironment("SeaweedFS__VolumeUrl", context.SeaweedVolume.GetEndpoint("http"))
        //     .WaitFor(context.FileTransDb)
        //     .WaitFor(context.SeaweedFiler)
        //     .WithOtlpExporter();

        return builder;
    }
}