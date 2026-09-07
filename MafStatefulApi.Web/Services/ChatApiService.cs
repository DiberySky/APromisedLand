using System.Net.Http.Json;

namespace MafStatefulApi.Web.Services;

public class ChatApiService(IHttpClientFactory httpClientFactory, ILogger<ChatApiService> logger)
{
    public async Task<ChatResponse?> SendMessageAsync(string message, string? conversationId = null)
    {
        try
        {
            var client = httpClientFactory.CreateClient("api");
            var request = new ChatRequest
            {
                Message = message,
                ConversationId = conversationId
            };

            var response = await client.PostAsJsonAsync("/chat", request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<ChatResponse>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending message to API");
            return null;
        }
    }

    public async Task<List<string>> GetSessionsAsync()
    {
        try
        {
            var client = httpClientFactory.CreateClient("api");
            var response = await client.GetAsync("chat/sessions");
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<SessionsResponse>();
            return result?.Sessions?.ToList() ?? new List<string>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting sessions from API");
            return new List<string>();
        }
    }

    public async Task<bool> ResetSessionAsync(string conversationId)
    {
        try
        {
            var client = httpClientFactory.CreateClient("api");
            var response = await client.PostAsync($"chat/reset/{conversationId}", null);
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error resetting session {ConversationId}", conversationId);
            return false;
        }
    }
}

public record ChatRequest
{
    public string? ConversationId { get; init; }
    public required string Message { get; init; }
}

public record ChatResponse
{
    public required string ConversationId { get; init; }
    public required string Answer { get; init; }
}

public record SessionsResponse
{
    public required IEnumerable<string> Sessions { get; init; }
}
