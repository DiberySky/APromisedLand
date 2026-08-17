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
        var nebulaMetad0 = builder.AddContainer("nebula-metad0", "docker.io/vesoft/nebula-metad", "nightly")
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
                "--minloglevel=0"
            )
            .WithEndpoint(name: "meta", port: 9559, targetPort: 9559, isProxied: false)
            .WithHttpEndpoint(name: "ws", port: 19559, targetPort: 19559, isProxied: false)
            .WithEndpoint(name: "ws2", port: 19560, targetPort: 19560, isProxied: false)
            .WithBindMount("./data/meta0", "/data/meta")
            .WithBindMount("./logs/meta0", "/logs")
            .WithContainerRuntimeArgs("--cap-add", "SYS_PTRACE")
            .WithContainerRuntimeArgs("--health-cmd", "curl -sf http://nebula-metad0:19559/status")
            .WithContainerRuntimeArgs("--health-interval", "30s")
            .WithContainerRuntimeArgs("--health-timeout", "10s")
            .WithContainerRuntimeArgs("--health-retries", "3")
            .WithContainerRuntimeArgs("--health-start-period", "20s");

        // ========== Storage ==========
        var nebulaStoraged0 = builder.AddContainer("nebula-storaged0", "docker.io/vesoft/nebula-storaged", "nightly")
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
                "--minloglevel=0"
            )
            .WithEndpoint(name: "storage", port: 9779, targetPort: 9779, isProxied: false)
            .WithHttpEndpoint(name: "ws", port: 19779, targetPort: 19779, isProxied: false)
            .WithEndpoint(name: "ws2", port: 19780, targetPort: 19780, isProxied: false)
            .WithBindMount("./data/storage0", "/data/storage")
            .WithBindMount("./logs/storage0", "/logs")
            .WithContainerRuntimeArgs("--cap-add", "SYS_PTRACE")
            .WithContainerRuntimeArgs("--health-cmd", "curl -sf http://nebula-storaged0:19779/status")
            .WithContainerRuntimeArgs("--health-interval", "30s")
            .WithContainerRuntimeArgs("--health-timeout", "10s")
            .WithContainerRuntimeArgs("--health-retries", "3")
            .WithContainerRuntimeArgs("--health-start-period", "20s")
            .WaitFor(nebulaMetad0);

        // ========== Graphd ==========
        var nebulaGraphd = builder.AddContainer("nebula-graphd", "docker.io/vesoft/nebula-graphd", "nightly")
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
                "--minloglevel=0"
            )
            .WithEndpoint(name: "graph", port: 9669, targetPort: 9669, isProxied: false)
            .WithHttpEndpoint(name: "ws", port: 19669, targetPort: 19669, isProxied: false)
            .WithEndpoint(name: "ws2", port: 19670, targetPort: 19670, isProxied: false)
            .WithBindMount("./logs/graph", "/logs")
            .WithContainerRuntimeArgs("--cap-add", "SYS_PTRACE")
            .WithContainerRuntimeArgs("--health-cmd", "curl -sf http://nebula-graphd:19669/status")
            .WithContainerRuntimeArgs("--health-interval", "30s")
            .WithContainerRuntimeArgs("--health-timeout", "10s")
            .WithContainerRuntimeArgs("--health-retries", "3")
            .WithContainerRuntimeArgs("--health-start-period", "20s")
            .WaitFor(nebulaStoraged0);

        // ========== HTTP Gateway（新增）==========
        // Gateway 提供 /api/db/connect 和 /api/db/exec REST API
        // 版本对应：Nebula 3.x → gateway v3.2.x；如 nightly 不兼容可尝试 latest
        var nebulaGateway = builder.AddContainer("nebula-http-gateway", "docker.io/vesoft/nebula-http-gateway", "v2.2.0")
            .WithEnvironment("USER", "root")
            .WithEnvironment("TZ", tz)
            .WithHttpEndpoint(name: "gateway", port: 8080, targetPort: 8080, isProxied: false)
            .WaitFor(nebulaGraphd);

        // ========== Console ==========
        var nebulaConsole = builder.AddContainer("nebula-console", "docker.io/vesoft/nebula-console", "nightly")
            .WithEntrypoint("/bin/sh")
            .WithArgs("-c",
                "for i in $(seq 1 60); do " +
                "nebula-console -addr nebula-graphd -port 9669 -u root -p nebula -e 'ADD HOSTS \"nebula-storaged0\":9779'; " +
                "if [ $? -eq 0 ]; then break; fi; " +
                "sleep 1; echo \"retry to add hosts.\"; " +
                "done && tail -f /dev/null")
            .WaitFor(nebulaGraphd);

        // API Service 应等待 Gateway 就绪（而不是直接等 graphd）
        context.NebulaGraph = nebulaGraphd;
        context.NebulaGraphEndpoint = nebulaGateway.GetEndpoint("gateway");
        context.NebulaConsole = nebulaConsole;

        return builder;
    }
}