namespace APromisedLand.Api.Foundries.Models;
public class ChatRequest
{
    public string ModelAlias { get; set; } = string.Empty;
    public List<Message> Messages { get; set; } = new();
}

public class Message
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
