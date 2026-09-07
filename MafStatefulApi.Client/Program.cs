﻿using MafStatefulApi.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Http.Resilience;
using Polly;

Console.WriteLine("=== MAF 有状态 API 客户端演示 ===");
Console.WriteLine("此客户端使用 Aspire 服务发现来调用 API。\n");

// Build the host with service defaults (service discovery, resilience with extended timeouts, OpenTelemetry)
var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

// Reduce logging noise from HTTP client and Polly resilience handlers
// Keep only warnings and errors for infrastructure components
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
builder.Logging.AddFilter("Polly", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.Extensions.Http", LogLevel.Warning);

// 移除所有默认的弹性处理器（包括 AddServiceDefaults 添加的）
builder.Services.ConfigureHttpClientDefaults(httpBuilder =>
{
    httpBuilder.AddStandardResilienceHandler(options =>
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(300);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(7);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(600);
    });
});

// Configure typed HttpClient with service discovery
// The base address "http://api" is resolved via Aspire service discovery
builder.Services.AddHttpClient<ApiClient>(client =>
    {
        client.BaseAddress = new Uri("http+https://api");
        client.Timeout = TimeSpan.FromMinutes(3); // 仍保留作为最后防线
    });

var host = builder.Build();

// Get the API client from DI
var client = host.Services.GetRequiredService<ApiClient>();
var logger = host.Services.GetRequiredService<ILogger<Program>>();

try
{
    // Demo: Multi-turn conversation
    Console.WriteLine("开始多轮对话...\n");

    // Turn 1: Start a new conversation
    Console.WriteLine("用户：你好！我叫爱丽丝，我喜欢徒步。");
    var response1 = await client.ChatAsync("你好我的名字叫爱丽丝，我喜欢徒步旅行。");
    Console.WriteLine($"代理：{response1?.Answer}");
    Console.WriteLine($"(对话 ID：{response1?.ConversationId})\n");

    if (response1?.ConversationId is not null)
    {
        // Wait a moment between requests
        // await Task.Delay(1000);

        // Turn 2: Continue the same conversation (tests session persistence)
        Console.WriteLine("用户：我叫什么名字？我喜欢做什么？");
        var response2 = await client.ChatAsync(
            "我叫什么名字？我喜欢做什么？",
            response1.ConversationId);
        Console.WriteLine($"代理：{response2?.Answer}\n");

        // await Task.Delay(1000);

        // Turn 3: Ask another contextual question
        Console.WriteLine("用户：你能为我推荐一些徒步路线吗？");
        var response3 = await client.ChatAsync(
            "你能为我推荐一些远足路线吗？",
            response1.ConversationId);
        Console.WriteLine($"代理：{response3?.Answer}\n");

        // Reset the conversation
        Console.WriteLine($"正在重置对话 {response1.ConversationId}...");
        await client.ResetAsync(response1.ConversationId);
        Console.WriteLine("对话重置成功。\n");
    }

    Console.WriteLine("=== 演示完成 ===");
    Console.WriteLine("对话演示了：");
    Console.WriteLine("1. 开始新对话（创建新会话）");
    Console.WriteLine("2. 使用相同的 conversationId 继续（从存储加载会话）");
    Console.WriteLine("3. 代理记住之前消息的上下文");
    Console.WriteLine("4. 重置对话（从存储中删除会话）");
}
catch (HttpRequestException ex)
{
    logger.LogError(ex, "连接 API 失败。API 正在运行吗？");
    Console.WriteLine($"\n错误：{ex.Message}");
    Console.WriteLine("请确保 API 通过 Aspire AppHost 运行。");
}
catch (Exception ex)
{
    logger.LogError(ex, "发生意外错误。");
    Console.WriteLine($"\n错误：{ex.Message}");
}