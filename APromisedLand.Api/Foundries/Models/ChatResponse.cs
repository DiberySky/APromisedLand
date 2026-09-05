namespace APromisedLand.Api.Foundries.Models;
public class ChatResponse
{
    public string Content { get; }
    public string Model { get; }
    public int? TokensUsed { get; }

    public ChatResponse(string content, string model, int? tokensUsed)
    {
        Content = content;
        Model = model;
        TokensUsed = tokensUsed;
    }
}