namespace DiberyBlazorWebSky.Models;

public class ChatMessage
{
    public required string Role { get; set; }
    public required string Text { get; set; }
    public DateTime Timestamp { get; set; }
}