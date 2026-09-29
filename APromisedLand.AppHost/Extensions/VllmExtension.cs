using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;

namespace APromisedLand.AppHost.Extensions;

/// <summary>
/// vLLM 容器 + OpenAI 兼容端点的统一注册点。
/// 该文件是全项目 AI 模型名的唯一数据源。
///
/// ★ 容器拓扑：
///   - vllm        : Chat 模型  (Qwen3-4B-AWQ)      端口 8000
///   - vllm-embed  : Embedding  (BAAI/bge-m3)       端口 8001
///
/// ★ 健康检查策略：
///   - 不给 vLLM 容器注册 Aspire 的 WithHttpHealthCheck
///     原因：vLLM 的 /health 在 HTTP 服务起来后就返回 200，
///     但模型权重仍在异步加载（30s~数分钟）。Aspire 的探针周期固定
///     且不支持 initialDelaySeconds，启动窗口必然失败，导致状态被
///     错误标记为 Unhealthy（不影响功能，但影响 Dashboard 可读性）。
///   - 就绪判断交给业务层：MafRagApi 的 VllmWarmupService 负责预热，
///     /api/health/deep 提供 readiness 探活。
///
/// ★ 硬件约束（GTX 1660 Ti / Turing / SM 7.5 / 6GB）：
///   - vLLM V1 引擎要求 SM 8.0+ → 必须用 VLLM_USE_V1=0 回退 V0
///   - Turing 不支持 bfloat16       → 必须 --dtype float16
///   - 6GB 装不下 FP16 4B 权重     → 必须用 AWQ 量化模型
///   - 必须用 v0.11 之前的版本      → v0.11+ 已删除 V0 引擎
/// </summary>
public static class VllmExtension
{
    // ─── 模型名常量（单一数据源）────────────────────────────────
    public const string EmbeddingModelName = "BAAI/bge-m3";
    public const string ChatModelName = "Qwen/Qwen3-4B-AWQ";
    public const string ServedModelName = "qwen3-4b-awq";
    public const string ServedEmbeddingModelName = "bge-m3";

    public const int EmbeddingDimension = 1024;
    public const int RecommendedContextLength = 4096;

    /// <summary>Turing 必须固定的镜像版本。</summary>
    public const string TuringSafeImageTag = "qwen3-tf451";

    // ─── 端点名 ────────────────────────────────────────────────
    public const string HttpEndpointName = "http";
    public const string EmbeddingHttpEndpointName = "http-embed";

    // ─── 固定宿主端口（避免 Aspire 随机分配）───────────────────
    public const int ChatHostPort = 5723;
    public const int EmbeddingHostPort = 5719;
    private const int ContainerPort = 8000;

    // ─── 内部常量 ──────────────────────────────────────────────
    private const string HuggingFaceCacheVolumeName = "vllm-hf-cache";

    public static IDistributedApplicationBuilder AddVllm(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        // ─── 可配置项 ──────────────────────────────────────────
        var useGpu = builder.Configuration.GetValue("Vllm:UseGpu", true);

        var imageTag = builder.Configuration
            .GetValue<string?>("Vllm:ImageTag") ?? TuringSafeImageTag;
        var pinnedTag = !string.IsNullOrWhiteSpace(imageTag);

        var contextLength = builder.Configuration
            .GetValue("Vllm:ContextLength", RecommendedContextLength);

        var gpuMemoryUtil = builder.Configuration
            .GetValue("Vllm:GpuMemoryUtilization", 0.85);

        var hfToken = builder.Configuration.GetValue<string?>("Vllm:HfToken");

        var hfEndpoint = builder.Configuration
            .GetValue<string?>("Vllm:HfEndpoint") ?? "https://hf-mirror.com";

        var forceV0Engine = builder.Configuration
            .GetValue("Vllm:ForceV0Engine", true);

        // ══════════════════════════════════════════════════════
        // 1. Chat 容器
        // ══════════════════════════════════════════════════════
        var vllm = builder.AddContainer("vllm", "vllm/vllm-openai")
            .WithImageTag(TuringSafeImageTag)
            .WithEndpoint(port: ChatHostPort, targetPort: ContainerPort, scheme: "http", name: HttpEndpointName)
            .WithVolume(HuggingFaceCacheVolumeName, "/root/.cache/huggingface")
            .WithLifetime(ContainerLifetime.Persistent)
            .WithEntrypoint("python3")
            .WithArgs(
                "-m", "vllm.entrypoints.openai.api_server",
                "--model", ChatModelName,
                "--served-model-name", ServedModelName,
                "--enable-auto-tool-choice",
                "--tool-call-parser", "hermes",
                "--dtype", "float16",
                "--quantization", "awq",
                "--max-model-len", contextLength.ToString(),
                "--gpu-memory-utilization", gpuMemoryUtil.ToString("F2"),
                "--enforce-eager"
            )
            .WithEnvironment("VLLM_USE_V1", forceV0Engine ? "0" : "1")
            .WithEnvironment("HF_ENDPOINT", hfEndpoint)
            .WithEnvironment("HF_HUB_OFFLINE", "1")
            .WithEnvironment("TRANSFORMERS_OFFLINE", "1");

        // ★ 不注册 WithHttpHealthCheck —— 见文件头注释

        if (pinnedTag)
        {
            vllm = vllm.WithImageTag(imageTag!);
            Console.WriteLine($"[vLLM] Chat 容器使用镜像 tag: {imageTag}");

            if (IsV0UnsupportedTag(imageTag!))
            {
                Console.WriteLine(
                    $"[vLLM] ⚠️ 警告：v{imageTag} 已删除 V0 引擎，" +
                    "Turing (SM 7.5) 将无法启动。请使用 v0.7.3。");
            }
        }

        if (useGpu)
        {
            vllm = vllm.WithContainerRuntimeArgs("--gpus=all");
        }
        else
        {
            Console.WriteLine("[vLLM] ⚠️ GPU 支持未启用，将使用 CPU 推理（极慢）");
        }

        if (!string.IsNullOrWhiteSpace(hfToken))
            vllm = vllm.WithEnvironment("HF_TOKEN", hfToken);

        resourceContext.Vllm = vllm;

        // ══════════════════════════════════════════════════════
        // 2. Embedding 容器（bge-m3）
        //    v0.7.3 用 --task embed（不是 --runner pooling）
        // ══════════════════════════════════════════════════════
        var vllmEmbed = builder.AddContainer("vllm-embed", "vllm/vllm-openai")
            .WithImageTag(TuringSafeImageTag)
            .WithEndpoint(port: EmbeddingHostPort, targetPort: ContainerPort, scheme: "http", name: EmbeddingHttpEndpointName)
            .WithVolume(HuggingFaceCacheVolumeName, "/root/.cache/huggingface")
            .WithLifetime(ContainerLifetime.Persistent)
            .WithEntrypoint("python3")
            .WithArgs(
                "-m", "vllm.entrypoints.openai.api_server",
                "--model", EmbeddingModelName,
                "--served-model-name", ServedEmbeddingModelName,
                "--task", "embed",
                "--dtype", "float16",
                "--gpu-memory-utilization", gpuMemoryUtil.ToString("F2"),
                "--enforce-eager"
            )
            .WithEnvironment("VLLM_USE_V1", forceV0Engine ? "0" : "1")
            .WithEnvironment("HF_ENDPOINT", hfEndpoint)
            .WithEnvironment("HF_HUB_OFFLINE", "1")
            .WithEnvironment("TRANSFORMERS_OFFLINE", "1");

        // ★ 同样不注册 WithHttpHealthCheck

        if (pinnedTag)
        {
            vllmEmbed = vllmEmbed.WithImageTag(imageTag!);
        }

        if (useGpu)
        {
            vllmEmbed = vllmEmbed.WithContainerRuntimeArgs("--gpus=all");
        }

        if (!string.IsNullOrWhiteSpace(hfToken))
            vllmEmbed = vllmEmbed.WithEnvironment("HF_TOKEN", hfToken);

        resourceContext.VllmEmbed = vllmEmbed;

        // ══════════════════════════════════════════════════════
        // 3. 启动摘要
        // ══════════════════════════════════════════════════════
        Console.WriteLine(
            $"[vLLM] Chat={ChatModelName} (:{ChatHostPort}→{ContainerPort}), " +
            $"Embed={EmbeddingModelName} (:{EmbeddingHostPort}→{ContainerPort}), " +
            $"Context={contextLength}, Gpu={useGpu}, Tag={imageTag ?? TuringSafeImageTag}");

        return builder;
    }

    private static bool IsV0UnsupportedTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return false;
        if (tag.Equals("latest", StringComparison.OrdinalIgnoreCase)) return true;

        var cleaned = tag.StartsWith('v') || tag.StartsWith('V') ? tag[1..] : tag;
        var parts = cleaned.Split('.');
        if (parts.Length < 2) return false;
        if (!int.TryParse(parts[0], out var major)) return false;
        if (!int.TryParse(parts[1], out var minor)) return false;

        if (major > 0) return true;
        if (minor > 11) return true;
        if (minor == 11 && parts.Length >= 3 && int.TryParse(parts[2], out var patch))
            return patch >= 1;

        return false;
    }
}