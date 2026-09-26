namespace MafSampleApi.Models;

/// <summary>Ollama / Agent 相关配置，绑定 appsettings.json 的 "Agent" 节。</summary>
public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    /// <summary>
    /// 默认聊天模型。
    /// ⚠️ 必须与 OllamaExtension.ChatModelName 保持一致（AppHost 会通过
    ///    Agent__ChatModel 环境变量覆盖，未配置时才用此默认值）。
    /// </summary>
    public string ChatModel { get; set; } = "qwen3:4b";

    /// <summary>
    /// 默认嵌入模型。
    /// ⚠️ 必须与 OllamaExtension.EmbeddingModelName 保持一致。
    /// </summary>
    public string EmbeddingModel { get; set; } = "bge-m3";

    /// <summary>
    /// 系统提示词。
    /// 末尾 /no_think 关闭 qwen3 的思考模式，简单对话可提速 5~10 倍。
    /// </summary>
    public string SystemPrompt { get; set; } =
        "你是一个乐于助人的助手，通过 Ollama 在本地运行。回答简明扼要。/no_think";

    /// <summary>内存中最多保留的会话数；超出后按 LRU 淘汰。</summary>
    public int MaxSessions { get; set; } = 256;

    /// <summary>会话空闲超时（保留字段，用于未来后台清理）。</summary>
    public TimeSpan SessionIdleTimeout { get; set; } = TimeSpan.FromMinutes(30);
}