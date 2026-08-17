namespace NebulaGraphApiService.Services;

public class NebulaClientOptions
{
    public string Ip { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 9669;
    public string Username { get; set; } = "root";
    public string Password { get; set; } = "123456";
    public string DefaultSpace { get; set; } = string.Empty; // 可选默认空间
}