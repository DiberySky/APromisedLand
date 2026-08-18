namespace NebulaGraphApiService.Services;

public class NebulaGraphOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 9669;
    public string Username { get; set; } = "root";
    public string Password { get; set; } = "nebula";
    public string SpaceName { get; set; } = "social_network";

    public int MaxRetries { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 2000;
    public int SchemaReadyTimeoutSeconds { get; set; } = 30;
    public int SpaceReadyTimeoutSeconds { get; set; } = 30;
}