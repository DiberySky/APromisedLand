using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace TreeGraph.MafWfApi.Tests.Services;

/// <summary>测试用 IChatClient：记录收到的消息，返回预制的响应/流。</summary>
internal sealed class FakeChatClient : IChatClient
{
    public List<ChatMessage> ReceivedMessages { get; } = new();
    public ChatResponse? NextResponse { get; set; }
    public List<ChatResponseUpdate> NextUpdates { get; } = new();

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ReceivedMessages.AddRange(messages);
        return Task.FromResult(NextResponse ?? new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ReceivedMessages.AddRange(messages);
        foreach (var update in NextUpdates)
        {
            yield return update;
        }
        await Task.CompletedTask;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
