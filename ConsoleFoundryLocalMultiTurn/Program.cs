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

var chatService = kernel.GetRequiredService<IChatCompletionService>();
var chatHistory = new ChatHistory();
chatHistory.AddSystemMessage("你是一个乐于助人的助手，请用中文简洁回答问题。");

Console.WriteLine("=== 智能助手已启动，输入 'exit' 退出 ===\n");

while (true)
{
    Console.Write("用户：");
    var input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input) || input.ToLower() == "exit")
    {
        Console.WriteLine("再见！");
        break;
    }

    chatHistory.AddUserMessage(input);

    Console.Write("Agent：");
    var response = await chatService.GetChatMessageContentAsync(chatHistory);
    var content = response.Items.FirstOrDefault()?.ToString() ?? string.Empty;
    Console.WriteLine(content);
    Console.WriteLine();

    chatHistory.AddAssistantMessage(content);
}