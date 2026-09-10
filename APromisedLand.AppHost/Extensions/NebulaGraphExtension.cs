using Aspire.Hosting;

namespace APromisedLand.AppHost.Extensions;

public static class NebulaGraphExtension
{
    public static IDistributedApplicationBuilder AddNebulaGraph(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        var tz = builder.Configuration["TZ"] ?? "UTC";

        // ========== Meta ==========
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
                "--redirect_stdout=false", // 新增
                "--logtostderr=true" // 新增
            )
            // ... endpoints, mounts ...
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
            // ... endpoints, mounts ...
            .WithContainerRuntimeArgs("--hostname", "nebula-storaged0") // 新增
            .WithContainerRuntimeArgs("--memory", "1g") // 可选
            // 如需健康检查可保留，但建议统一
            .WaitFor(nebulaMetad0);

        // ========== Graphd ==========
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
            // ... endpoints, mounts ...
            .WithContainerRuntimeArgs("--hostname", "nebula-graphd") // 新增
            .WithContainerRuntimeArgs("--memory", "1g") // 可选
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

        // ========== studio ==========
        // builder.AddNebulaStudio(context);
        // 添加 Studio 容器
        var studio = builder.AddContainer("nebula-studio", "vesoft/nebula-graph-studio:v3.8.0")
            .WithHttpEndpoint(port: 7001, targetPort: 7001, name: "studio-http")
            .WithEnvironment("STUDIO_PORT", "7001") // 显式声明，默认即为 7001
            .WaitFor(nebulaGraphd);

        // 直连 graphd Thrift 端口，无需 Gateway
        context.NebulaGraph = nebulaGraphd;
        context.NebulaGraphEndpoint = nebulaGraphd.GetEndpoint("graph");
        context.NebulaConsole = nebulaConsole;
        context.NebulaStudio = studio;

        return builder;
    }
}