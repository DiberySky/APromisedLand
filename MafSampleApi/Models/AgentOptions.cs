namespace MafSampleApi.Models;

/// <summary>vLLM 相关配置，绑定 appsettings.json 的 "Agent" 节。</summary>
public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    // ─── Chat 端点 ────────────────────────────────────────────
    /// <summary>
    /// OpenAI 兼容端点（不带 /v1）。
    /// 由 Aspire 注入的 VLLM_HTTP 环境变量覆盖。
    /// </summary>
    public string Endpoint { get; set; } = "http://localhost:8000";

    /// <summary>API Key。vLLM 不校验，传非空字符串即可。</summary>
    public string ApiKey { get; set; } = "EMPTY";

    // ─── Embedding 端点 ───────────────────────────────────────
    /// <summary>
    /// Embedding 的 OpenAI 兼容端点（不带 /v1）。
    /// 由 Aspire 注入的 VLLM_EMBEDDING_HTTP 覆盖。
    /// </summary>
    public string EmbeddingEndpoint { get; set; } = "http://localhost:8001";

    // ─── 模型 ─────────────────────────────────────────────────
    /// <summary>聊天模型名（与 vLLM --served-model-name 一致）。</summary>
    public string ChatModel { get; set; } = "qwen3-4b-awq";

    /// <summary>嵌入模型名（与 vLLM --served-model-name 一致）。</summary>
    public string EmbeddingModel { get; set; } = "bge-m3";

    // ─── 提示词 ───────────────────────────────────────────────
    public string SystemPrompt { get; set; } =
        "你是一个乐于助人的助手。回答简明扼要。/no_think";

    // ─── 会话 ─────────────────────────────────────────────────
    public int MaxSessions { get; set; } = 256;
    public TimeSpan SessionIdleTimeout { get; set; } = TimeSpan.FromMinutes(30);

    // ─── 超时预算 ─────────────────────────────────────────────
    public int ChatBudgetSeconds { get; set; } = 120;
    public int LoopBudgetSeconds { get; set; } = 240;
}