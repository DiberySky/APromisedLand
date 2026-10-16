using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace TreeGraph.MafWfApi.Tests.Integration;

/// <summary>
/// 集成测试：整管启动，但 IChatClient 用假实现、Redis 用内存缓存替代。
/// Agent:Endpoint 指向 127.0.0.1:9（ discard 端口），保证 /api/health/deep 确定性 503。
/// </summary>
public sealed class MafWfApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Agent:Endpoint"] = "http://127.0.0.1:9",
            });
        });

        builder.ConfigureServices(services =>
        {
            // 内存缓存替代 Redis（会话存储依赖 IDistributedCache）
            services.AddSingleton<IDistributedCache>(
                _ => new MemoryDistributedCache(
                    Microsoft.Extensions.Options.Options.Create(
                        new MemoryDistributedCacheOptions())));

            // 假 IChatClient 替代 vLLM（后注册生效）
            services.AddSingleton<IChatClient>(new EchoChatClient());
        });
    }

    private sealed class EchoChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "集成测试回答")));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}

public class ApiIntegrationTests(MafWfApiFactory factory)
    : IClassFixture<MafWfApiFactory>
{
    [Fact]
    public async Task health_恒200()
    {
        var client = factory.CreateClient();

        var resp = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task health_deep_vllm不可达返回503()
    {
        var client = factory.CreateClient();

        var resp = await client.GetAsync("/api/health/deep");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, resp.StatusCode);
    }

    [Fact]
    public async Task chat_正常消息返回200与回复()
    {
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/chat",
            new { message = "你好" });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("集成测试回答", body);
        Assert.Contains("qwen3-4b-awq", body);
    }

    [Fact]
    public async Task chat_空消息返回400()
    {
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/chat",
            new { message = "" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
