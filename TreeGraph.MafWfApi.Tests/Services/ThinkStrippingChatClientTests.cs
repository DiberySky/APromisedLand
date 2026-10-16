using TreeGraph.MafWfApi.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace TreeGraph.MafWfApi.Tests.Services;

public class ThinkStrippingChatClientTests
{
    private static ThinkStrippingChatClient Create(IChatClient inner)
        => new(inner, NullLogger<ThinkStrippingChatClient>.Instance);

    // ─── 非流式 ─────────────────────────────────────────────

    [Fact]
    public async Task 非流式_剥离think块()
    {
        var inner = new FakeChatClient
        {
            NextResponse = new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "<think>推理过程</think>最终答案"))
        };

        var client = Create(inner);
        var resp = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        Assert.Equal("最终答案", resp.Text);
    }

    [Fact]
    public async Task 非流式_无think块原样返回()
    {
        var inner = new FakeChatClient
        {
            NextResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "普通回答"))
        };

        var client = Create(inner);
        var resp = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        Assert.Equal("普通回答", resp.Text);
    }

    [Fact]
    public async Task 非流式_工具结果追加no_think()
    {
        var inner = new FakeChatClient();
        var client = Create(inner);

        var toolMsg = new ChatMessage(ChatRole.Tool,
            [new FunctionResultContent("call-1", "工具输出")]);

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi"), toolMsg]);

        var sent = inner.ReceivedMessages
            .SelectMany(m => m.Contents)
            .OfType<FunctionResultContent>()
            .Single();

        Assert.Contains("/no_think", sent.Result?.ToString());
    }

    [Fact]
    public async Task 非流式_工具结果已含no_think不重复追加()
    {
        var inner = new FakeChatClient();
        var client = Create(inner);

        var toolMsg = new ChatMessage(ChatRole.Tool,
            [new FunctionResultContent("call-1", "工具输出 /no_think")]);

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi"), toolMsg]);

        var sent = inner.ReceivedMessages
            .SelectMany(m => m.Contents)
            .OfType<FunctionResultContent>()
            .Single();

        Assert.Equal("工具输出 /no_think", sent.Result?.ToString());
    }

    // ─── 流式 ─────────────────────────────────────────────

    [Fact]
    public async Task 流式_跨chunk剥离think块()
    {
        var inner = new FakeChatClient();
        inner.NextUpdates.AddRange(new[]
        {
            new ChatResponseUpdate(ChatRole.Assistant, "<thi"),
            new ChatResponseUpdate(ChatRole.Assistant, "nk>推理"),
            new ChatResponseUpdate(ChatRole.Assistant, "过程</thi"),
            new ChatResponseUpdate(ChatRole.Assistant, "nk>\n\n最终"),
            new ChatResponseUpdate(ChatRole.Assistant, "答案"),
        });

        var client = Create(inner);
        var text = await CollectAsync(client);

        Assert.Equal("最终答案", text);
    }

    [Fact]
    public async Task 流式_纯文本不受影响()
    {
        var inner = new FakeChatClient();
        inner.NextUpdates.AddRange(new[]
        {
            new ChatResponseUpdate(ChatRole.Assistant, "你好"),
            new ChatResponseUpdate(ChatRole.Assistant, "，世界"),
        });

        var client = Create(inner);
        var text = await CollectAsync(client);

        Assert.Equal("你好，世界", text);
    }

    [Fact]
    public async Task 流式_未闭合think整段丢弃()
    {
        var inner = new FakeChatClient();
        inner.NextUpdates.AddRange(new[]
        {
            new ChatResponseUpdate(ChatRole.Assistant, "前言"),
            new ChatResponseUpdate(ChatRole.Assistant, "<think>未闭合的推理"),
        });

        var client = Create(inner);
        var text = await CollectAsync(client);

        Assert.Equal("前言", text);
    }

    private static async Task<string> CollectAsync(IChatClient client)
    {
        var sb = new System.Text.StringBuilder();
        await foreach (var update in client.GetStreamingResponseAsync(
                           [new ChatMessage(ChatRole.User, "hi")]))
        {
            foreach (var content in update.Contents)
            {
                if (content is TextContent t)
                    sb.Append(t.Text);
            }
        }
        return sb.ToString();
    }
}
