namespace DiberyBlazorWebSky.Models.Graph;

public class GraphChatMessage
{
    public required string Role { get; set; }
    public required string Text { get; set; }
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// 若本条消息启用了图上下文，记录上下文摘要（如 "TestGraph（3 节点 / 1 边）"）。
    /// UI 会在气泡下方显示标签。
    /// </summary>
    public string? GraphContextLabel { get; set; }
}