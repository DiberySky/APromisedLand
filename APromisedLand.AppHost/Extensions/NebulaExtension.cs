using Microsoft.Extensions.Hosting;

namespace APromisedLand.AppHost.Extensions;

public static class NebulaExtension
{
    public static IDistributedApplicationBuilder AddNebula(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        // 1. Meta 服务
        var nebulaMetad = builder.AddContainer("nebula-metad", "vesoft/nebula-metad:v3.8.0")
            .WithEndpoint(targetPort: 9559, name: "meta")
            .WithEndpoint(targetPort: 19559, name: "meta-http")
            .WithArgs(
                "--meta_server_addrs=nebula-metad:9559",
                "--local_ip=nebula-metad",
                "--ws_ip=nebula-metad",
                "--port=9559",
                "--ws_http_port=19559",
                "--data_path=/data/meta"
            )
            .WithVolume("nebula-meta-data", "/data/meta");

        // 2. Storage 服务
        var nebulaStoraged = builder.AddContainer("nebula-storaged", "vesoft/nebula-storaged:v3.8.0")
            .WithEndpoint(targetPort: 9779, name: "storage")
            .WithEndpoint(targetPort: 19779, name: "storage-http")
            .WithArgs(
                "--meta_server_addrs=nebula-metad:9559",
                "--local_ip=nebula-storaged",
                "--ws_ip=nebula-storaged",
                "--port=9779",
                "--ws_http_port=19779",
                "--data_path=/data/storage"
            )
            .WithVolume("nebula-storage-data", "/data/storage")
            .WaitFor(nebulaMetad);

        // 3. Graph 服务（修复：固定 HTTP 端口为 19669）
        var nebulaGraphd = builder.AddContainer("nebula-graphd", "vesoft/nebula-graphd:v3.8.0")
            .WithEndpoint(port: 9669, targetPort: 9669, name: "graphd")          // Thrift 端口
            .WithHttpEndpoint(port: 19669, targetPort: 19669, name: "graphd-http")   // 固定 HTTP 端口为 19669
            .WithArgs(
                "--meta_server_addrs=nebula-metad:9559",
                "--local_ip=nebula-graphd",
                "--ws_ip=nebula-graphd",
                "--port=9669",
                "--ws_http_port=19669"
            )
            .WaitFor(nebulaMetad)
            .WaitFor(nebulaStoraged);

        // 4. 初始化容器：注册 Storage 节点
        string initCommand = 
            "while ! nebula-console -addr nebula-graphd -port 9669 -u root -p nebula -e 'ADD HOSTS \"nebula-storaged\":9779;' ; do echo 'Retry...' ; sleep 1 ; done && echo 'Storage hosts added successfully.' && tail -f /dev/null";

        var nebulaInit = builder.AddContainer("nebula-init", "vesoft/nebula-console:v3.8.0")
            .WithEntrypoint("/bin/sh")
            .WithArgs("-c", initCommand)
            .WaitFor(nebulaGraphd);

        var nebulaHttpGateway = builder.AddContainer("nebula-http-gateway", "vesoft/nebula-http-gateway:v3.8.0")
            .WithEndpoint(port: 8080, targetPort: 8080, name: "gateway-http") // 固定宿主机端口
            .WithArgs(
                "--graph_server_addrs=nebula-graphd:9669",
                "--meta_server_addrs=nebula-metad:9559",
                "--storage_server_addrs=nebula-storaged:9779",
                "--http_port=8080"
            )
            .WaitFor(nebulaGraphd);
        
        // 保存引用
        context.NebulaGraph = nebulaGraphd;
        context.NebulaGraphEndpoint = nebulaGraphd.GetEndpoint("graphd");

        return builder;
    }
}