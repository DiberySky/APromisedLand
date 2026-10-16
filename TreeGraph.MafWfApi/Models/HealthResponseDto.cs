namespace TreeGraph.MafWfApi.Models;

/// <summary>/api/health(/deep) 响应体。</summary>
public sealed class HealthResponseDto
{
    public string Status { get; set; } = "ok";
    public string? ChatModel { get; set; }
    public string? Endpoint { get; set; }
}
