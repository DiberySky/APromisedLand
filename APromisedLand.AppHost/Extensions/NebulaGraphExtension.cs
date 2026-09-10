using System.Net.Sockets;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;   // 需要 ProtocolType

namespace APromisedLand.AppHost.Extensions;

public static class NebulaGraphExtension
{
    public static IDistributedApplicationBuilder AddNebulaGraph(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        var tz = builder.Configuration["TZ"] ?? "UTC";

        // ========== Meta ==========
        // 仅容器内端口，宿主机不暴露（仅集群内部通信）
        var nebulaMetad0 = builder.AddContainer("nebula-metad0", "docker.io/vesoft/nebula-metad", "v3.8.0")
            .WithEnvironment("USER", "root")
            .WithEnvironment("TZ", tz)
            .WithArgs(
                "--meta_server_addrs=nebula-metad0:9559",
                "--local_ip=nebula-metad0",
                "--ws_ip=nebula-metad0",
                "--port=9559",
                "--ws_http_port=19559",
                "--data_path=/data/meta",
                "--log_dir=/logs",
                "--v=0",
                "--minloglevel=0",
                "--redirect_stdout=false",
                "--logtostderr=true"
            )
            .WithEndpoint("meta", e =>
            {
                e.TargetPort = 9559;
                e.UriScheme = "thrift";
                e.Protocol = ProtocolType.Tcp;
            })
            .WithEndpoint("meta-http", e =>
            {
                e.TargetPort = 19559;
                e.UriScheme = "http";
            })
            .WithContainerRuntimeArgs("--hostname", "nebula-metad0")
            .WithContainerRuntimeArgs("--memory", "1g");

        // ========== Storage ==========
        var nebulaStoraged0 = builder.AddContainer("nebula-storaged0", "docker.io/vesoft/nebula-storaged", "v3.8.0")
            .WithEnvironment("USER", "root")
            .WithEnvironment("TZ", tz)
            .WithArgs(
                "--meta_server_addrs=nebula-metad0:9559",
                "--local_ip=nebula-storaged0",
                "--ws_ip=nebula-storaged0",
                "--port=9779",
                "--ws_http_port=19779",
                "--data_path=/data/storage",
                "--log_dir=/logs",
                "--v=0",
                "--minloglevel=0",
                "--redirect_stdout=false",
                "--logtostderr=true"
            )
            .WithEndpoint("storage", e =>
            {
                e.TargetPort = 9779;
                e.UriScheme = "thrift";
                e.Protocol = ProtocolType.Tcp;
            })
            .WithEndpoint("storage-http", e =>
            {
                e.TargetPort = 19779;
                e.UriScheme = "http";
            })
            .WithContainerRuntimeArgs("--hostname", "nebula-storaged0")
            .WithContainerRuntimeArgs("--memory", "1g")
            .WaitFor(nebulaMetad0);

        // ========== Graphd ==========
        // 业务侧（MafRagService）通过 "graph" 端点连 Thrift 9669
        var nebulaGraphd = builder.AddContainer("nebula-graphd", "docker.io/vesoft/nebula-graphd", "v3.8.0")
            .WithEnvironment("USER", "root")
            .WithEnvironment("TZ", tz)
            .WithArgs(
                "--meta_server_addrs=nebula-metad0:9559",
                "--port=9669",
                "--local_ip=nebula-graphd",
                "--ws_ip=nebula-graphd",
                "--ws_http_port=19669",
                "--log_dir=/logs",
                "--v=0",
                "--minloglevel=0",
                "--redirect_stdout=false",
                "--logtostderr=true"
            )
            .WithEndpoint("graph", e =>
            {
                e.TargetPort = 9669;
                e.UriScheme = "thrift";
                e.Protocol = ProtocolType.Tcp;
            })
            .WithEndpoint("graph-http", e =>
            {
                e.TargetPort = 19669;
                e.UriScheme = "http";
            })
            .WithContainerRuntimeArgs("--hostname", "nebula-graphd")
            .WithContainerRuntimeArgs("--memory", "1g")
            .WaitFor(nebulaStoraged0);

        // ========== Console ==========
        var nebulaConsole = builder.AddContainer("nebula-console", "docker.io/vesoft/nebula-console", "v3.8.0")
            .WithEntrypoint("/bin/sh")
            .WithArgs("-c",
                "for i in $(seq 1 60); do " +
                "nebula-console -addr nebula-graphd -port 9669 -u root -p nebula -e 'ADD HOSTS \"nebula-storaged0\":9779'; " +
                "if [ $? -eq 0 ]; then break; fi; " +
                "sleep 1; echo \"retry to add hosts.\"; " +
                "done && tail -f /dev/null")
            .WaitFor(nebulaGraphd);

        // ========== Studio ==========
        // ★ 宿主机端口 7001 → 17001，避开 WebLogic 等默认 7001
        //   容器内仍是 7001，浏览器访问 http://localhost:17001
        var studio = builder.AddContainer("nebula-studio", "vesoft/nebula-graph-studio:v3.8.0")
            .WithHttpEndpoint(port: 17001, targetPort: 7001, name: "studio-http")
            .WithEnvironment("STUDIO_PORT", "7001")
            .WaitFor(nebulaGraphd);

        context.NebulaGraph = nebulaGraphd;
        context.NebulaGraphEndpoint = nebulaGraphd.GetEndpoint("graph");
        context.NebulaConsole = nebulaConsole;
        context.NebulaStudio = studio;

        return builder;
    }
}