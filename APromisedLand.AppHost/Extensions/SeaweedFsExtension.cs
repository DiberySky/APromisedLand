namespace APromisedLand.AppHost.Extensions;

public static class SeaweedFsExtension
{
    // 固定镜像版本，避免 :latest 漂移导致行为变化。
    // 4.46 已包含 S3 网关的 external URL 支持（S3_EXTERNAL_URL / -externalUrl）。
    private const string Image = "chrislusf/seaweedfs:4.46";

    public static IDistributedApplicationBuilder AddSeaweedFs(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        // ============================================================
        // 1. Master —— 集群拓扑，必须最先启动
        // ============================================================
        context.SeaweedMaster = builder.AddContainer("seaweedfs-master", Image)
            .WithArgs(
                "master",
                "-ip=seaweedfs-master",     // 显式自注册名，避免容器重建后 IP 变化
                "-port=9333",
                "-mdir=/data",
                "-defaultReplication=000"   // 单节点：不复制
            )
            .WithHttpEndpoint(name: "http", targetPort: 9333)
            .WithVolume("seaweedfs-master-data", "/data")
            .WithOtlpExporter();

        // ============================================================
        // 2. Volume —— 实际数据块存储
        // ============================================================
        context.SeaweedVolume = builder.AddContainer("seaweedfs-volume", Image)
            .WithArgs(
                "volume",
                "-mserver=seaweedfs-master:9333",
                "-ip=seaweedfs-volume",
                "-port=8080",
                "-dir=/data",
                "-max=0"                    // 0 = 不限制磁盘用量（开发环境）
            )
            .WithHttpEndpoint(name: "http", targetPort: 8080)
            .WithVolume("seaweedfs-volume-data", "/data")
            .WithOtlpExporter()
            .WaitFor(context.SeaweedMaster);

        // ============================================================
        // 3. Filer —— POSIX 语义的文件系统层
        // ============================================================
        context.SeaweedFiler = builder.AddContainer("seaweedfs-filer", Image)
            .WithArgs(
                "filer",
                "-master=seaweedfs-master:9333",
                "-ip=seaweedfs-filer",
                "-port=8888",
                "-defaultStoreDir=/data"
            )
            .WithHttpEndpoint(name: "http", targetPort: 8888)
            .WithVolume("seaweedfs-filer-data", "/data")
            .WithOtlpExporter()
            .WaitFor(context.SeaweedMaster)
            .WaitFor(context.SeaweedVolume);

        // ============================================================
        // 4. S3 网关 —— 对外暴露 S3 API（监听 8333）
        //
        // 关键修复说明：
        //
        //   a) 固定宿主机端口 8333 且 isProxied: false，直连容器。
        //      Aspire DCP 反向代理会改写 Host / Authorization / x-amz-* 等头，
        //      破坏 AWS SDK 的 SigV4 签名，触发 "ResponseEnded" 类错误。
        //
        //   b) 通过 S3_EXTERNAL_URL 环境变量告诉网关"客户端签名的外部地址"。
        //      4.46 的正确参数名是 -externalUrl；环境变量形式更符合容器化习惯。
        //      ★ 只用其中一种即可，不要同时设置，避免歧义。
        //
        //   c) 网关通过 args 里的容器名 seaweedfs-filer:8888 直接寻址 filer，
        //      不需要 WithReference。
        // ============================================================
        context.SeaweedS3 = builder.AddContainer("seaweedfs-s3", Image)
            .WithArgs(
                "s3",
                "-filer=seaweedfs-filer:8888",
                "-port=8333"
            )
            .WithEnvironment("S3_EXTERNAL_URL", "http://localhost:8333")
            .WithHttpEndpoint(
                name: "s3",
                targetPort: 8333,
                port: 8333,             // 固定宿主机端口
                isProxied: false)       // 关闭 Aspire DCP 反向代理
            .WithOtlpExporter()
            .WaitFor(context.SeaweedFiler);

        // ========== FileTrans Service（暂未启用） ==========
        // context.FileTransService = builder.AddProject<Projects.FileTransService>("seaweedfs-service")
        //     .WithReference(context.FileTransDb)
        //     .WithEnvironment("SeaweedFS__BaseUrl",   context.SeaweedFiler.GetEndpoint("http"))
        //     .WithEnvironment("SeaweedFS__MasterUrl", context.SeaweedMaster.GetEndpoint("http"))
        //     .WithEnvironment("SeaweedFS__VolumeUrl", context.SeaweedVolume.GetEndpoint("http"))
        //     .WaitFor(context.FileTransDb)
        //     .WaitFor(context.SeaweedFiler)
        //     .WithOtlpExporter();

        return builder;
    }
}