using System.ComponentModel.DataAnnotations;

namespace TreeGraph.MafWfApi.Agents;

/// <summary>
/// Agent 配置，绑定 appsettings.json 的 "Agent" 节。
/// 端点解析优先级：VLLM_HTTP 环境变量 > Agent:Endpoint > http://localhost:8000。
/// </summary>
public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    /// <summary>
    /// OpenAI 兼容端点（不带 /v1）。
    /// 通常由 Aspire 注入的 VLLM_HTTP / Agent__Endpoint 环境变量覆盖。
    /// </summary>
    [Url]
    public string? Endpoint { get; set; }

    /// <summary>API Key。vLLM 不校验，传非空字符串即可。</summary>
    public string ApiKey { get; set; } = "EMPTY";

    /// <summary>聊天模型名（与 vLLM --served-model-name 一致）。</summary>
    [Required(AllowEmptyStrings = false)]
    public string ChatModel { get; set; } = "qwen3-4b-awq";

    /// <summary>无工具对话（/api/chat）的系统提示词。</summary>
    public string SystemPrompt { get; set; } =
        "你是一个乐于助人的助手。回答简明扼要。/no_think";

    [Required, MinLength(10)]
    public string AssistantInstructions { get; set; } =
        "你是一名企业内部知识助手，回答简洁、专业，使用中文。";

    [Required, MinLength(10)]
    public string WriterInstructions { get; set; } =
        "你是一名技术文案撰写专家。根据用户给出的主题，写一段结构清晰、信息密度高的中文初稿，不超过 300 字。只输出正文。";

    [Required, MinLength(10)]
    public string CriticInstructions { get; set; } =
        "你是一名严格的技术编辑。阅读下面这篇初稿，挑出事实性、逻辑和表达问题，并直接输出一版润色后的中文终稿。只输出终稿正文，不要解释你改了什么。";
}
