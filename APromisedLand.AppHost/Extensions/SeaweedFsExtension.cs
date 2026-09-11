using System.IO;

namespace APromisedLand.AppHost.Extensions;

public static class SeaweedFsExtension
{
    // 固定镜像版本，避免 :latest 漂移导致行为变化。
    // 4.46 已包含 S3 网关的 external URL 支持（S3_EXTERNAL_URL / -externalUrl）。
    private const string Image = "chrislusf/seaweedfs:4.46";

    /// <summary>
    /// 解析 Configs 目录下配置文件的绝对路径。
    /// 基于 AppContext.BaseDirectory（编译输出目录，例如
    /// D:\APromisedLand\APromisedLand.AppHost\bin\Debug\net10.0\），
    /// 与当前工作目录无关，Aspire 从任何目录启动都能找到。
    /// </summary>
    private static string ConfigPath(string fileName)
        => Path.Combine(AppContext.BaseDirectory, "Configs", fileName);

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
                "-ip=seaweedfs-master",
                "-mdir=/data",
                "-port=9333",
                "-defaultReplication=000"
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
                "-max=0"
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
        // 关键点：
        //   a) 固定宿主机端口 8333 且 isProxied: false，直连容器。
        //      Aspire DCP 反向代理会改写 Host / Authorization / x-amz-* 等头，
        //      破坏 AWS SDK 的 SigV4 签名。
        //
        //   b) 通过 S3_EXTERNAL_URL 环境变量告诉网关"客户端签名的外部地址"。
        //
        //   c) 通过 -config 加载 S3 身份配置，否则 AWS SDK 带签名的请求会被拒：
        //      "Signed request requires setting up SeaweedFS S3 authentication"
        //
        //   d) 配置文件路径从 AppContext.BaseDirectory 解析（编译输出目录），
        //      与当前工作目录无关。
        // ============================================================
        var s3ConfigPath = ConfigPath("seaweedfs-s3.json");

        if (!File.Exists(s3ConfigPath))
        {
            throw new FileNotFoundException(
                $"SeaweedFS S3 配置文件缺失：{s3ConfigPath}。" +
                "请确认 APromisedLand.AppHost.csproj 中已配置 " +
                "<Content Include=\"Configs\\**\\*\" /> 并执行 dotnet build。",
                s3ConfigPath);
        }

        context.SeaweedS3 = builder.AddContainer("seaweedfs-s3", Image)
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
            .WithEnvironment("S3_EXTERNAL_URL", "http://localhost:8333")
            .WithHttpEndpoint(
                name: "s3",
                targetPort: 8333,
                port: 8333,
                isProxied: false)
            .WithOtlpExporter()
            .WaitFor(context.SeaweedFiler);

        return builder;
    }
}