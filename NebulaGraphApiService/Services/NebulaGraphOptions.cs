namespace NebulaGraphApiService.Services;

public class NebulaGraphOptions
{
    // Gateway 配置（API Service -> Gateway）
    public string GatewayHost { get; set; } = "localhost";
    public int GatewayPort { get; set; } = 8080;

    // Graphd 配置（Gateway -> Graphd，容器网络内使用容器名）
    public string Host { get; set; } = "nebula-graphd";  // ← 修改：从 localhost 改为容器名
    public int Port { get; set; } = 9669;
    public int HttpPort { get; set; } = 19669;

    public string Username { get; set; } = "root";
    public string Password { get; set; } = "nebula";
    public string SpaceName { get; set; } = "academic_graph";

    public int MaxRetries { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 2000;
    public int SchemaReadyTimeoutSeconds { get; set; } = 30;
    public int SpaceReadyTimeoutSeconds { get; set; } = 30;
}