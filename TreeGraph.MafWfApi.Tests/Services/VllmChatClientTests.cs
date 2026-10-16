using System.Net;
using System.Text;
using TreeGraph.MafWfApi.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace TreeGraph.MafWfApi.Tests.Services;

public class VllmChatClientTests
{
    private sealed class CaptureHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }

    private static VllmChatClient CreateClient(CaptureHandler handler)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:8000/v1/")
        };
        return new VllmChatClient(http, "qwen3-4b-awq", "EMPTY",
            NullLogger<VllmChatClient>.Instance);
    }

    [Fact]
    public async Task 请求体_注入enable_thinking为false()
    {
        var handler = new CaptureHandler(HttpStatusCode.OK,
            """{"id":"r1","choices":[{"message":{"role":"assistant","content":"你好"},"finish_reason":"stop"}]}""");
        var client = CreateClient(handler);

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        Assert.NotNull(handler.LastBody);
        Assert.Contains("\"enable_thinking\":false", handler.LastBody);
        Assert.Contains("\"model\":\"qwen3-4b-awq\"", handler.LastBody);
    }

    [Fact]
    public async Task 请求_打到chat_completions且带Bearer头()
    {
        var handler = new CaptureHandler(HttpStatusCode.OK,
            """{"id":"r1","choices":[{"message":{"role":"assistant","content":"你好"},"finish_reason":"stop"}]}""");
        var client = CreateClient(handler);

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.Equal("http://localhost:8000/v1/chat/completions",
            handler.LastRequest.RequestUri?.ToString());
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization?.Scheme);
        Assert.Equal("EMPTY", handler.LastRequest.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task 响应_解析文本与模型()
    {
        var handler = new CaptureHandler(HttpStatusCode.OK,
            """{"id":"r1","choices":[{"message":{"role":"assistant","content":"最终答案"},"finish_reason":"stop"}]}""");
        var client = CreateClient(handler);

        var resp = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        Assert.Equal("最终答案", resp.Text);
        Assert.Equal("qwen3-4b-awq", resp.ModelId);
        Assert.Equal(ChatFinishReason.Stop, resp.FinishReason);
    }

    [Fact]
    public async Task 响应_非200抛HttpRequestException()
    {
        var handler = new CaptureHandler(HttpStatusCode.InternalServerError, "boom");
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]));
    }
}
