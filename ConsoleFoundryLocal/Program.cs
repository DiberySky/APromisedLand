using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

// 创建 Kernel
var builder = Kernel.CreateBuilder();
builder.AddOpenAIChatCompletion(
    modelId: "phi-4-mini", //deepseek-r1-7b phi-4-mini qwen3-8b  qwen2.5-7b
    apiKey: "nokey",
    endpoint: new Uri("http://127.0.0.1:60557/v1")
);
var kernel = builder.Build();

// 获取聊天服务
var chatService = kernel.GetRequiredService<IChatCompletionService>();

// 对话历史
var chatHistory = new ChatHistory();
chatHistory.AddSystemMessage("你是一个乐于助人的助手，请用中文简洁回答问题。");
chatHistory.AddUserMessage("你好，请介绍一下湖北黄石市。");

// 获取回复
var response = await chatService.GetChatMessageContentAsync(chatHistory);

// 获取内容
var content = response.Items.FirstOrDefault()?.ToString() ?? string.Empty;
Console.WriteLine("Agent 回复：" + content);