using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;

namespace APromisedLand.AppHost.Extensions;

/// <summary>
/// Ollama 容器 + 模型资源的统一注册点。
/// 该文件是全项目 AI 模型名的唯一数据源。
///
/// ★ 模型策略：
///   - Chat:      qwen3:4b  —— 6GB 显卡可全 GPU 卸载，速度 ×3~5
///   - Embedding: bge-m3    —— 1024 维，与 LiteGraph 集合一致
///
/// ★ 镜像版本策略：
///   - 默认不固定 tag → 使用 Aspire 内置 latest
///   - 若必须固定，通过 Ollama:ImageTag 显式指定
///   - ⚠️ 不要固定到 0.5.x！qwen3 需要 Ollama >= 0.6.2
/// </summary>
public static class OllamaExtension
{
    // ─── 模型名常量（单一数据源）────────────────────────────────
    public const string EmbeddingModelName = "bge-m3";
    public const string ChatModelName = "qwen3:8b";  // ★ 从 8b 换为 4b
    
    /// <summary>bge-m3 的向量维度（下游初始化向量库集合时使用）。</summary>
    public const int EmbeddingDimension = 1024;

    /// <summary>qwen3 系列模型要求的最低 Ollama 版本。</summary>
    public const string MinOllamaVersionForQwen3 = "0.6.2";

    /// <summary>qwen3:4b 在 6GB 显卡上的推荐上下文长度。</summary>
    public const int RecommendedContextLength = 8192; 

    private const string OllamaDataVolumeName   = "ollama-data";
    private const string OllamaHttpEndpointName = "http";

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
            // ★ 显存友好配置：
            //   - 单请求并行（避免并行 slot 分裂 KV cache）
            //   - 单模型加载（避免 8b 与 bge-m3 同时占用显存）
            //   - 显式上下文长度（默认 4096，对多轮 Agent 对话过小）
            //   - keep_alive 让模型常驻显存
            .WithEnvironment("OLLAMA_NUM_PARALLEL", "1")
            .WithEnvironment("OLLAMA_MAX_LOADED_MODELS", "1")
            .WithEnvironment("OLLAMA_CONTEXT_LENGTH", contextLength.ToString())
            .WithEnvironment("OLLAMA_KEEP_ALIVE", "30m");

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

        if (useGpu)
            ollama = ollama.WithGPUSupport();

        ollama.WithHttpHealthCheck(
            path: "/api/tags",
            statusCode: 200,
            endpointName: OllamaHttpEndpointName);

        resourceContext.Ollama = ollama;

        // ─── 2. 模型资源 ─────────────────────────────────────
        resourceContext.Embedding = resourceContext.Ollama
            .AddModel("embedding", EmbeddingModelName);

        resourceContext.ChatModel = resourceContext.Ollama
            .AddModel("chat-model", ChatModelName);

        Console.WriteLine(
            $"[Ollama] Chat={ChatModelName}, Embedding={EmbeddingModelName}, " +
            $"Context={contextLength}, Gpu={useGpu}");

        return builder;
    }

    /// <summary>
    /// 判断 tag 是否可能过旧（不支持 qwen3）。
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