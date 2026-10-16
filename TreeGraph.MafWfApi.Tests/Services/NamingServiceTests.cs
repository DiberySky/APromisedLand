using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using TreeGraph.MafWfApi.Services;
using Xunit;

namespace TreeGraph.MafWfApi.Tests.Services;

/// <summary>
/// NamingService 测试：LLM 用假 IChatClient 返回固定翻译，
/// 断言代码侧的规范化/拼接/冲突检测（方案 §5.4 用例矩阵 + 边界）。
/// </summary>
public class NamingServiceTests
{
    /// <summary>返回固定文本的假 IChatClient。</summary>
    private sealed class StubChatClient(string reply) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply)));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private static NamingService Create(string llmReply)
        => new(new StubChatClient(llmReply), NullLogger<NamingService>.Instance);

    private static Task<TreeGraph.MafWfApi.Models.NamingResult> Gen(string reply, string input)
        => Create(reply).GenerateAsync(input, CancellationToken.None);

    // ════════ 方案 §5.4 用例矩阵 ════════

    [Fact]
    public async Task 用户ID_缩写映射()
    {
        var r = await Gen("User Id", "用户ID");
        Assert.Equal("user_id", r.SnakeCase);
        Assert.Equal("UserId", r.PascalCase);
        Assert.Empty(r.Warnings);
    }

    [Fact]
    public async Task 三级菜单_数字单独成词()
    {
        var r = await Gen("Level 3 Menu", "三级菜单");
        Assert.Equal("level_3_menu", r.SnakeCase);
        Assert.Equal("Level3Menu", r.PascalCase);
        Assert.Empty(r.Warnings);
    }

    [Fact]
    public async Task IPv6地址_白名单保留()
    {
        var r = await Gen("IPv6 Address", "IPv6地址");
        Assert.Equal("ipv6_address", r.SnakeCase);
        Assert.Equal("IPv6Address", r.PascalCase);
        Assert.Empty(r.Warnings);
    }

    [Fact]
    public async Task 用户Profile_驼峰保留()
    {
        var r = await Gen("User Profile", "用户Profile");
        Assert.Equal("user_profile", r.SnakeCase);
        Assert.Equal("UserProfile", r.PascalCase);
        Assert.Empty(r.Warnings);
    }

    [Fact]
    public async Task DNS服务器_全大写缩写归一()
    {
        var r = await Gen("DNS Server", "DNS服务器");
        Assert.Equal("dns_server", r.SnakeCase);
        Assert.Equal("DnsServer", r.PascalCase);
        Assert.Empty(r.Warnings);
    }

    [Fact]
    public async Task 班级_命中CSharp关键字告警()
    {
        var r = await Gen("Class", "班级");
        Assert.Equal("class", r.SnakeCase);
        Assert.Equal("Class", r.PascalCase);
        var w = Assert.Single(r.Warnings);
        Assert.Contains("C# 关键字", w);
        Assert.Contains("@class", w);
    }

    // ════════ 规范化边界 ════════

    [Fact]
    public async Task LLM返回连写_Level3Menu_被字母数字边界拆开()
    {
        var r = await Gen("Level3Menu", "三级菜单");
        Assert.Equal("level_3_menu", r.SnakeCase);
        Assert.Equal("Level3Menu", r.PascalCase);
    }

    [Fact]
    public async Task LLM返回全大写ID_仍映射为Id()
    {
        var r = await Gen("User ID", "用户ID");
        Assert.Equal("user_id", r.SnakeCase);
        Assert.Equal("UserId", r.PascalCase);
    }

    [Fact]
    public async Task LLM返回Markdown围栏_被剥离()
    {
        var r = await Gen("```\nOrder Payment Status\n```", "订单支付状态");
        Assert.Equal("order_payment_status", r.SnakeCase);
        Assert.Equal("OrderPaymentStatus", r.PascalCase);
    }

    [Fact]
    public async Task LLM返回带引号标点_被清洗()
    {
        var r = await Gen("\"Graph, Neighbor: Node!\"", "图中的邻居节点");
        Assert.Equal("graph_neighbor_node", r.SnakeCase);
        Assert.Equal("GraphNeighborNode", r.PascalCase);
    }

    [Fact]
    public async Task BCL同名_PascalCase命中告警()
    {
        var r = await Gen("Task", "任务");
        Assert.Equal("task", r.SnakeCase);
        Assert.Equal("Task", r.PascalCase);
        var w = Assert.Single(r.Warnings);
        Assert.Contains("BCL", w);
    }

    [Fact]
    public async Task LLM返回空_兜底原输入并告警翻译失败()
    {
        var r = await Gen("   ", "用户");
        // 结果由原输入生成（含中文），且必须显式告警
        Assert.Contains(r.Warnings, w => w.Contains("翻译失败"));
    }

    [Fact]
    public async Task 输入前后空白_被裁剪()
    {
        var r = await Gen("User Id", "  用户ID  ");
        Assert.Equal("用户ID", r.Source);
        Assert.Equal("user_id", r.SnakeCase);
    }
}
