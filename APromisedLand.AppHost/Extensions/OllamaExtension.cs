using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;

namespace APromisedLand.AppHost.Extensions;

/// <summary>
/// Ollama 容器 + 模型资源的统一注册点。
/// 该文件是全项目 AI 模型名的唯一数据源。
///
/// ★ 模型策略：
///   - Chat:      qwen3:4b  —— 6GB 显卡可全 GPU 卸载，比 8b 快 2~3 倍
///   - Embedding: bge-m3    —— 1024 维，与 LiteGraph 集合一致
///
/// ★ 性能策略：
///   - OLLAMA_NUM_PARALLEL=1       单请求并行，KV cache 不分裂
///   - OLLAMA_MAX_LOADED_MODELS=1  避免 chat 与 embedding 同时占显存
///   - OLLAMA_KEEP_ALIVE=30m       模型常驻，避免冷加载
///   - OLLAMA_CONTEXT_LENGTH=8192  4b 模型在 6GB 上足够
///
/// ★ 镜像版本策略：
///   - 默认不固定 tag → 使用 Aspire 内置 latest
///   - 若必须固定，通过 Ollama:ImageTag 显式指定
///   - ⚠️ 不要固定到 0.5.x！qwen3 需要 Ollama >= 0.6.2
/// </summary>
public static class OllamaExtension
{
    // ─── 模型名常量（单一数据源）────────────────────────────────
    /// <summary>嵌入模型（1024 维，与 LiteGraph 集合一致）。</summary>
    public const string EmbeddingModelName = "bge-m3";

    /// <summary>
    /// 聊天模型。
    /// 6GB 显卡推荐 qwen3:4b（全 GPU 卸载，速度 ×3~5）；
    /// 12GB+ 显卡可换 qwen3:8b 提升质量。
    /// </summary>
    public const string ChatModelName = "qwen3:4b";

    /// <summary>bge-m3 的向量维度（下游初始化向量库集合时使用）。</summary>
    public const int EmbeddingDimension = 1024;

    /// <summary>qwen3 系列模型要求的最低 Ollama 版本。</summary>
    public const string MinOllamaVersionForQwen3 = "0.6.2";

    /// <summary>
    /// 聊天模型的推荐上下文长度。
    /// - 4096：6GB 显存极致省显存，短对话够用
    /// - 8192：多轮 Agent 对话推荐（默认）
    /// - 16384：12GB+ 显卡可用
    /// </summary>
    public const int RecommendedContextLength = 4096;

    // ─── 内部常量 ──────────────────────────────────────────────
    private const string OllamaDataVolumeName = "ollama-data";

    /// <summary>Ollama HTTP 端点名（供其它扩展引用，例如 MafSampleApiExtension）。</summary>
    public const string HttpEndpointName = "http";

    // ─── 主入口 ────────────────────────────────────────────────
    public static IDistributedApplicationBuilder AddOllama(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        // ─── 可配置项 ──────────────────────────────────────────
        var useGpu = builder.Configuration.GetValue("Ollama:UseGpu", true);

        // ★ 只有显式配置时才固定 tag；默认留空 → 用 latest
        var imageTag = builder.Configuration.GetValue<string?>("Ollama:ImageTag");
        var pinnedTag = !string.IsNullOrWhiteSpace(imageTag);

        // 上下文长度（可通过配置覆盖）
        var contextLength = builder.Configuration
            .GetValue("Ollama:ContextLength", RecommendedContextLength);

        // ─── 1. Ollama 容器 ──────────────────────────────────
        var ollama = builder.AddOllama("ollama")
            .WithDataVolume(OllamaDataVolumeName)
            .WithLifetime(ContainerLifetime.Persistent)
            // ★ 显存友好 + 性能配置：
            //   - 单请求并行（避免并行 slot 分裂 KV cache）
            //   - 单模型加载（避免 chat 与 bge-m3 同时占显存）
            //   - 显式上下文长度（默认 4096，多轮 Agent 对话偏小）
            //   - keep_alive 让模型常驻显存，避免冷加载
            .WithEnvironment("OLLAMA_NUM_PARALLEL", "1")
            .WithEnvironment("OLLAMA_MAX_LOADED_MODELS", "1")
            .WithEnvironment("OLLAMA_CONTEXT_LENGTH", contextLength.ToString())
            .WithEnvironment("OLLAMA_KEEP_ALIVE", "30m");

        // ─── 镜像 tag 处理 ────────────────────────────────────
        if (pinnedTag)
        {
            ollama = ollama.WithImageTag(imageTag!);

            if (IsPotentiallyTooOld(imageTag!))
            {
                Console.WriteLine(
                    $"[Ollama] ⚠️ 警告：Ollama:ImageTag = '{imageTag}' 可能过旧，" +
                    $"模型 '{ChatModelName}' 需要 >= {MinOllamaVersionForQwen3}。" +
                    $"建议删除 Ollama:ImageTag 配置。");
            }
        }
        else
        {
            Console.WriteLine(
                "[Ollama] 未固定镜像版本，使用 Aspire 默认（latest）。" +
                "如需固定，请设置 Ollama:ImageTag。");
        }

        // ─── GPU 支持 ────────────────────────────────────────
        if (useGpu)
        {
            ollama = ollama.WithGPUSupport();
            Console.WriteLine("[Ollama] GPU 支持已启用（需宿主 Docker 已配置 --gpus all）");
        }
        else
        {
            Console.WriteLine("[Ollama] ⚠️ GPU 支持未启用，将使用 CPU 推理（慢 10~20 倍）");
        }

        // ─── 健康检查 ────────────────────────────────────────
        ollama.WithHttpHealthCheck(
            path: "/api/tags",
            statusCode: 200,
            endpointName: HttpEndpointName);

        resourceContext.Ollama = ollama;

        // ─── 2. 模型资源 ─────────────────────────────────────
        // AddModel 会触发 Aspire 自动 pull；容器启动后会等待模型就绪。
        resourceContext.Embedding = resourceContext.Ollama
            .AddModel("embedding", EmbeddingModelName);

        resourceContext.ChatModel = resourceContext.Ollama
            .AddModel("chat-model", ChatModelName);

        // ─── 启动摘要 ────────────────────────────────────────
        Console.WriteLine(
            $"[Ollama] Chat={ChatModelName}, Embedding={EmbeddingModelName}, " +
            $"Context={contextLength}, Gpu={useGpu}");

        if (!ChatModelName.Contains("4b") && useGpu)
        {
            Console.WriteLine(
                $"[Ollama] 💡 提示：当前 Chat 模型为 '{ChatModelName}'，" +
                "若显存 < 8GB，建议改为 'qwen3:4b' 以获得更好的 GPU 全卸载效果。");
        }

        return builder;
    }

    /// <summary>
    /// 判断 tag 是否可能过旧（不支持 qwen3）。
    /// 支持 v0.6.2 / 0.6.2 / latest 等格式。
    /// </summary>
    private static bool IsPotentiallyTooOld(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return false;
        if (tag.Equals("latest", StringComparison.OrdinalIgnoreCase)) return false;

        var cleaned = tag.StartsWith('v') || tag.StartsWith('V') ? tag[1..] : tag;
        var parts = cleaned.Split('.');
        if (parts.Length < 2) return false;
        if (!int.TryParse(parts[0], out var major)) return false;
        if (!int.TryParse(parts[1], out var minor)) return false;

        if (major > 0) return false;
        return minor < 6;
    }
}