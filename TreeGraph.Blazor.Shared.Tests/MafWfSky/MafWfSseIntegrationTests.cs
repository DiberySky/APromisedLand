using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using TreeGraph.Blazor.Shared.MafWfSky.Extensions;
using TreeGraph.Blazor.Shared.MafWfSky.Services;
using TreeGraph.Shared.MafWfSky.Contracts;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.MafWfSky;

/// <summary>
/// MafWfSseReader 集成测试：TestServer 起内存 SSE 服务器，走 AddMafWfSky 真实注册链路
/// （含弹性管道），验证 SSE 逐行解析、event:done 终止、错误行容错、非 2xx 抛错。
/// </summary>
public class MafWfSseIntegrationTests
{
    private const string GraphAgentPath = "/api/graph-agent/chat/stream";
    private const string WriterCriticPath = "/api/Workflows/writer-critic/stream";

    /// <summary>构建内存 SSE 服务器 + 挂 MafWfSky 的服务提供者。</summary>
    private static (TestServer Server, ServiceProvider Provider) Create(
        string expectedPath, Func<HttpContext, Task> handler)
    {
        var server = new TestServer(new WebHostBuilder().Configure(app =>
            app.Run(ctx => ctx.Request.Path == expectedPath
                ? handler(ctx)
                : Task.FromResult(ctx.Response.StatusCode = 404))));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMafWfSky(server.BaseAddress.ToString());

        // ★ 关键：DI 里的 typed client 默认走真实网络 handler，必须把 TestServer
        //   的内存 handler 注入为 PrimaryHandler，请求才会落到内存服务器。
        services.ConfigureAll<HttpClientFactoryOptions>(o =>
            o.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = server.CreateHandler()));

        return (server, services.BuildServiceProvider());
    }

    private static async Task WriteSseAsync(HttpContext ctx, params string[] lines)
    {
        ctx.Response.ContentType = "text/event-stream";
        ctx.Response.Headers.CacheControl = "no-cache";
        foreach (var line in lines)
        {
            await ctx.Response.WriteAsync(line + "\n\n", Encoding.UTF8);
            await ctx.Response.Body.FlushAsync();
        }
    }

    // ══════════════════════════════════════════════════════
    // Graph Agent 流
    // ══════════════════════════════════════════════════════

    [Fact]
    public async Task GraphAgentStream_按序解析StartDeltaDone_遇EventDone终止()
    {
        var (server, provider) = Create(GraphAgentPath, ctx => WriteSseAsync(ctx,
            """data: {"type":"Start","conversationId":"c1","agentName":"GraphAgent"}""",
            """data: {"type":"Delta","text":"你好"}""",
            """data: {"type":"Delta","text":"，世界"}""",
            """data: {"type":"Done","conversationId":"c1","toolsInvoked":["SearchNodes"]}""",
            "event: done\ndata: {}"));

        using (server)
        using (provider)
        {
            var reader = provider.GetRequiredService<MafWfSseReader>();
            var chunks = new List<StreamChunkDto>();
            await foreach (var c in reader.GraphAgentStreamAsync(null, "hi"))
                chunks.Add(c);

            Assert.Equal(4, chunks.Count);
            Assert.Equal(StreamChunkType.Start, chunks[0].Type);
            Assert.Equal("c1", chunks[0].ConversationId);
            Assert.Equal("GraphAgent", chunks[0].AgentName);
            Assert.Equal(StreamChunkType.Delta, chunks[1].Type);
            Assert.Equal("你好", chunks[1].Text);
            Assert.Equal("，世界", chunks[2].Text);
            Assert.Equal(StreamChunkType.Done, chunks[3].Type);
            Assert.Equal(new[] { "SearchNodes" }, chunks[3].ToolsInvoked);
        }
    }

    [Fact]
    public async Task GraphAgentStream_无EventDone时流自然结束()
    {
        var (server, provider) = Create(GraphAgentPath, ctx => WriteSseAsync(ctx,
            """data: {"type":"Delta","text":"only"}"""));

        using (server)
        using (provider)
        {
            var reader = provider.GetRequiredService<MafWfSseReader>();
            var chunks = new List<StreamChunkDto>();
            await foreach (var c in reader.GraphAgentStreamAsync("c9", "hi"))
                chunks.Add(c);

            Assert.Single(chunks);
            Assert.Equal("only", chunks[0].Text);
        }
    }

    [Fact]
    public async Task GraphAgentStream_空data与非data行与坏JSON被跳过()
    {
        var (server, provider) = Create(GraphAgentPath, ctx => WriteSseAsync(ctx,
            ": comment",                                   // SSE 注释行
            "data:",                                       // 空载荷
            "data: {}",                                    // 空对象（可反序列化，正常产出）
            """data: {not-json}""",                        // 坏 JSON
            """data: {"type":"Delta","text":"ok"}""",
            "event: done\ndata: {}"));

        using (server)
        using (provider)
        {
            var reader = provider.GetRequiredService<MafWfSseReader>();
            var chunks = new List<StreamChunkDto>();
            await foreach (var c in reader.GraphAgentStreamAsync(null, "hi"))
                chunks.Add(c);

            // 注释行/空 data/{}/坏 JSON 全部被跳过，仅 ok 产出
            Assert.Single(chunks);
            Assert.Equal("ok", chunks[0].Text);
        }
    }

    // ══════════════════════════════════════════════════════
    // Writer-Critic 流
    // ══════════════════════════════════════════════════════

    [Fact]
    public async Task WriterCriticStream_按Agent分块_遇EventDone终止()
    {
        var (server, provider) = Create(WriterCriticPath, ctx => WriteSseAsync(ctx,
            """data: {"agent":"Writer","delta":"第一段","isFinal":false}""",
            """data: {"agent":"Critic","delta":"建议","isFinal":false}""",
            """data: {"agent":"Writer","delta":"终稿内容","isFinal":true}""",
            "event: done\ndata: {}"));

        using (server)
        using (provider)
        {
            var reader = provider.GetRequiredService<MafWfSseReader>();
            var events = new List<WorkflowStreamEventDto>();
            await foreach (var e in reader.WriterCriticStreamAsync("主题"))
                events.Add(e);

            Assert.Equal(3, events.Count);
            Assert.Equal("Writer", events[0].Agent);
            Assert.False(events[0].IsFinal);
            Assert.Equal("Critic", events[1].Agent);
            Assert.True(events[2].IsFinal);
            Assert.Equal("终稿内容", events[2].Delta);
        }
    }

    // ══════════════════════════════════════════════════════
    // 错误路径
    // ══════════════════════════════════════════════════════

    [Fact]
    public async Task Stream_非2xx响应抛HttpRequestException()
    {
        var (server, provider) = Create(GraphAgentPath, ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return Task.CompletedTask;
        });

        using (server)
        using (provider)
        {
            var reader = provider.GetRequiredService<MafWfSseReader>();

            await Assert.ThrowsAsync<HttpRequestException>(async () =>
            {
                await foreach (var _ in reader.GraphAgentStreamAsync(null, "hi"))
                {
                    // 不应产出任何块
                }
            });
        }
    }

    [Fact]
    public async Task Stream_路径不存在返回404同样抛错()
    {
        var (server, provider) = Create("/other-path", ctx => Task.CompletedTask);

        using (server)
        using (provider)
        {
            var reader = provider.GetRequiredService<MafWfSseReader>();

            await Assert.ThrowsAsync<HttpRequestException>(async () =>
            {
                await foreach (var _ in reader.WriterCriticStreamAsync("t"))
                {
                }
            });
        }
    }
}
