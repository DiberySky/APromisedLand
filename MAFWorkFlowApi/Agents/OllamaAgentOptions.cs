using System.ComponentModel.DataAnnotations;

namespace MAFWorkFlowApi.Agents;

/// <summary>
/// 绑定 appsettings.json 中的 "Agent" 节点。
/// </summary>
public sealed class OllamaAgentOptions
{
    public const string SectionName = "Agent";

    /// <summary>Ollama 模型 ID。</summary>
    [Required(AllowEmptyStrings = false)]
    public string ModelId { get; set; } = "qwen2.5:7b";

    /// <summary>
    /// Ollama 基地址。留空时自动读取 Aspire 注入的连接串。
    /// </summary>
    [Url]
    public string? Endpoint { get; set; }

    /// <summary>通用助手的系统指令。</summary>
    [Required, MinLength(10)]
    public string AssistantInstructions { get; set; } =
        "你是一名企业内部知识助手，回答简洁、专业，使用中文。";

    /// <summary>写作 Agent 的系统指令。</summary>
    [Required, MinLength(10)]
    public string WriterInstructions { get; set; } =
        "你是一名技术文案撰写专家。根据用户给出的主题，写一段结构清晰、信息密度高的中文初稿，不超过 300 字。只输出正文。";

    /// <summary>审校 Agent 的系统指令。</summary>
    [Required, MinLength(10)]
    public string CriticInstructions { get; set; } =
        "你是一名严格的技术编辑。阅读下面这篇初稿，挑出事实性、逻辑和表达问题，并直接输出一版润色后的中文终稿。只输出终稿正文，不要解释你改了什么。";
}