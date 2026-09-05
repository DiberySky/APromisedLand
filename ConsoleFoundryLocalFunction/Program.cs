using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.ComponentModel;
using System.Text.RegularExpressions;

// 创建 Kernel
var builder = Kernel.CreateBuilder();
builder.AddOpenAIChatCompletion(
    modelId: "qwen3-8b", //deepseek-r1-7b phi-4-mini qwen3-8b  qwen2.5-7b
    apiKey: "nokey",
    endpoint: new Uri("http://127.0.0.1:65238/v1")
);

// 注册插件
builder.Plugins.AddFromType<TimePlugin>();
builder.Plugins.AddFromType<MathPlugin>();
builder.Plugins.AddFromType<DeviceTemperaturePlugin>();

var kernel = builder.Build();

// 执行设置
var executionSettings = new OpenAIPromptExecutionSettings
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
};

var chatService = kernel.GetRequiredService<IChatCompletionService>();
var chatHistory = new ChatHistory();

// 系统提示：明确告诉模型不要输出思考过程
chatHistory.AddSystemMessage("""
                             你是一个乐于助人的助手，请用中文简洁回答问题。
                             你可以使用工具来获取信息。
                             重要：直接给出最终答案，不要输出任何思考过程、推理步骤或工具调用细节。
                             不要输出 <thinker>、<tool> 等标签。
                             """);

Console.WriteLine("=== 智能助手已启动（支持工具调用），输入 'exit' 退出 ===\n");

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
    var response = await chatService.GetChatMessageContentAsync(chatHistory, executionSettings, kernel);
    var rawContent = response.Items.FirstOrDefault()?.ToString() ?? string.Empty;

    // 清理输出：移除思考标签和工具调用 JSON
    var content = CleanResponse(rawContent);

    Console.WriteLine(content);
    Console.WriteLine();

    chatHistory.AddAssistantMessage(content);
}

// ========== 清理输出 ==========

static string CleanResponse(string raw)
{
    if (string.IsNullOrEmpty(raw)) return string.Empty;

    // 移除 <thinker>...</thinker> 标签及其内容
    raw = Regex.Replace(raw, @"<thinker>[\s\S]*?</thinker>", "", RegexOptions.IgnoreCase);

    // 移除 <tool>...</tool> 标签及其内容
    raw = Regex.Replace(raw, @"<tool>[\s\S]*?</tool>", "", RegexOptions.IgnoreCase);

    // 移除工具调用的 JSON 数组
    raw = Regex.Replace(raw, @"\[\s*\{""name""[^]]*\}\s*\]", "", RegexOptions.Singleline);

    // 移除单独的 JSON 对象
    raw = Regex.Replace(raw, @"\{""name""[^}]*\}", "");

    // 移除多余空行
    raw = Regex.Replace(raw, @"\n\s*\n+", "\n");

    // 如果清理后为空，尝试提取有用的中文内容
    if (string.IsNullOrWhiteSpace(raw))
    {
        return "（模型返回了空内容，请重试）";
    }

    return raw.Trim();
}

// ========== 插件定义 ==========

public class TimePlugin
{
    [KernelFunction("get_current_time")]
    [Description("获取当前的日期和时间")]
    public string GetCurrentTime() => DateTime.Now.ToString("yyyy年MM月dd日 HH时mm分ss秒");

    [KernelFunction("get_current_date")]
    [Description("获取当前日期")]
    public string GetCurrentDate() => DateTime.Now.ToString("yyyy年MM月dd日");
}

public class MathPlugin
{
    [KernelFunction("add")]
    [Description("两个数字相加")]
    public double Add([Description("第一个数字")] double a, [Description("第二个数字")] double b) => a + b;

    [KernelFunction("multiply")]
    [Description("两个数字相乘")]
    public double Multiply([Description("第一个数字")] double a, [Description("第二个数字")] double b) => a * b;
}

public class DeviceTemperaturePlugin
{
    [KernelFunction("get_device_temperature")]
    [Description("获取指定设备的温度信息")]
    public string GetDeviceTemperature([Description("设备名称")] string device)
    {
        var random = new Random();
        var conditions = new[] { "晴天", "多云", "阴天", "小雨", "雷阵雨" };
        var temp = random.Next(-10, 0);
        var condition = conditions[random.Next(conditions.Length)];
        return $"{device}今天{condition}，气温{temp}°C。";
    }
}