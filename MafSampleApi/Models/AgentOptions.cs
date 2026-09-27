namespace MafSampleApi.Models;

/// <summary>Ollama / vLLM 相关配置，绑定 appsettings.json 的 "Agent" 节。</summary>
public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    // ─── 后端端点 ─────────────────────────────────────────────
    /// <summary>
    /// OpenAI 兼容端点（不带 /v1）。
    /// - vLLM：http://localhost:8000
    /// - Ollama 的 OpenAI 兼容层：http://localhost:11434
    /// </summary>
    public string Endpoint { get; set; } = "http://localhost:8000";

    /// <summary>API Key。vLLM 不校验，传非空字符串即可。</summary>
    public string ApiKey { get; set; } = "EMPTY";

    // ─── 模型 ─────────────────────────────────────────────────
    /// <summary>
    /// 聊天模型名。
    /// - vLLM：与 --served-model-name 一致（qwen3-4b-awq）
    /// - Ollama：qwen3:4b
    /// </summary>
    public string ChatModel { get; set; } = "qwen3-4b-awq";

    /// <summary>嵌入模型。</summary>
    public string EmbeddingModel { get; set; } = "bge-m3";

    // ─── 提示词 ───────────────────────────────────────────────
    public string SystemPrompt { get; set; } =
        "你是一个乐于助人的助手。回答简明扼要。/no_think";

    // ─── 会话 ─────────────────────────────────────────────────
    public int MaxSessions { get; set; } = 256;
    public TimeSpan SessionIdleTimeout { get; set; } = TimeSpan.FromMinutes(30);

    // ─── 超时预算 ─────────────────────────────────────────────
    /// <summary>/api/chat 单轮非流式的整体时间预算（秒），夹紧到 [15, 300]。</summary>
    public int ChatBudgetSeconds { get; set; } = 120;

    /// <summary>/api/chat/loop/sync 的整体时间预算（秒），夹紧到 [30, 600]。</summary>
    public int LoopBudgetSeconds { get; set; } = 240;
}