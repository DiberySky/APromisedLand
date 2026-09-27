using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;

namespace APromisedLand.AppHost.Extensions;

/// <summary>
/// vLLM 容器 + OpenAI 兼容端点的统一注册点。
/// 该文件是全项目 AI 模型名的唯一数据源。
///
/// ★ 硬件约束（GTX 1660 Ti / Turing / SM 7.5 / 6GB）：
///   - vLLM V1 引擎要求 SM 8.0+ → 必须用 VLLM_USE_V1=0 回退 V0
///   - Turing 不支持 bfloat16       → 必须 --dtype float16
///   - 6GB 装不下 FP16 4B 权重     → 必须用 AWQ 量化模型
///   - 必须用 v0.11 之前的版本      → v0.11+ 已删除 V0 引擎
///
/// ★ 模型策略：
///   - Chat:      Qwen/Qwen3-4B-AWQ   —— INT4 量化，约 2.5GB 权重
///   - Embedding: BAAI/bge-m3         —— 1024 维，与 LiteGraph 集合一致
///
/// ★ 性能策略：
///   - --gpu-memory-utilization 0.85  预留显存给 KV cache
///   - --max-model-len 4096           6GB 显存下的保守值
///   - --enforce-eager                禁用 CUDA graph，省显存
///   - HF cache 挂载为持久卷           避免重建容器重新下载模型
///
/// ★ 镜像版本策略：
///   - **Turing 必须固定为 v0.7.3**（最后一个稳定支持 V0 引擎的版本）
///   - Ampere+ 可用 latest
/// </summary>
public static class VllmExtension
{
    // ─── 模型名常量（单一数据源）────────────────────────────────
    /// <summary>嵌入模型（1024 维，与 LiteGraph 集合一致）。</summary>
    public const string EmbeddingModelName = "BAAI/bge-m3";

    /// <summary>
    /// 聊天模型（HuggingFace 格式）。
    /// Turing / 6GB：Qwen/Qwen3-4B-AWQ（INT4 量化）
    /// Ampere+ / 12GB+：可改用 Qwen/Qwen3-8B 提升质量
    /// </summary>
    public const string ChatModelName = "Qwen/Qwen3-4B-AWQ";

    /// <summary>对外暴露的模型名（/v1/models 里显示的名字，避免带斜杠）。</summary>
    public const string ServedModelName = "qwen3-4b-awq";

    /// <summary>bge-m3 的向量维度（下游初始化向量库集合时使用）。</summary>
    public const int EmbeddingDimension = 1024;

    /// <summary>
    /// 聊天模型的推荐上下文长度（6GB 显存下的保守值）。
    /// - 2048：极度省显存
    /// - 4096：推荐（默认）
    /// - 8192：仅 12GB+ 显卡可用
    /// </summary>
    public const int RecommendedContextLength = 4096;

    /// <summary>Turing 必须固定的镜像版本（最后一个支持 V0 引擎的版本）。</summary>
    public const string TuringSafeImageTag = "qwen3-tf451";

    // ─── 内部常量 ──────────────────────────────────────────────
    private const string HuggingFaceCacheVolumeName = "vllm-hf-cache";

    /// <summary>vLLM OpenAI 兼容端点的服务发现名（供 MafSampleApi 引用）。</summary>
    public const string HttpEndpointName = "http";

    // ─── 主入口 ────────────────────────────────────────────────
    public static IDistributedApplicationBuilder AddVllm(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        // ─── 可配置项 ──────────────────────────────────────────
        var useGpu = builder.Configuration.GetValue("Vllm:UseGpu", true);

        // ★ 默认固定为 v0.7.3（Turing 安全版本）；Ampere+ 可显式改为 latest
        var imageTag = builder.Configuration
            .GetValue<string?>("Vllm:ImageTag") ?? TuringSafeImageTag;
        var pinnedTag = !string.IsNullOrWhiteSpace(imageTag);

        var contextLength = builder.Configuration
            .GetValue("Vllm:ContextLength", RecommendedContextLength);

        var gpuMemoryUtil = builder.Configuration
            .GetValue("Vllm:GpuMemoryUtilization", 0.85);

        var hfToken = builder.Configuration.GetValue<string?>("Vllm:HfToken");

        // ★ 国内加速镜像（默认启用；设为空字符串可关闭）
        var hfEndpoint = builder.Configuration
            .GetValue<string?>("Vllm:HfEndpoint") ?? "https://hf-mirror.com";

        // ★ 是否强制 V0 引擎。Turing 必须为 true；Ampere+ 可设 false
        var forceV0Engine = builder.Configuration
            .GetValue("Vllm:ForceV0Engine", true);

        // ─── 1. vLLM 容器 ─────────────────────────────────────
        var vllm = builder.AddContainer("vllm", "vllm/vllm-openai")
            .WithImageTag("qwen3-tf451")
            .WithHttpEndpoint(targetPort: 8000, name: HttpEndpointName)
            .WithVolume(HuggingFaceCacheVolumeName, "/root/.cache/huggingface")
            .WithLifetime(ContainerLifetime.Persistent)
            .WithEntrypoint("python3")   // ★ 覆盖 bash
            .WithArgs(
                "-m", "vllm.entrypoints.openai.api_server",
                "--model", "Qwen/Qwen3-4B-AWQ",
                "--served-model-name", "qwen3-4b-awq",
                "--dtype", "float16",
                "--quantization", "awq",
                "--max-model-len", "4096",
                "--gpu-memory-utilization", "0.85",
                "--enforce-eager"
            )
            .WithEnvironment("VLLM_USE_V1", "0")
            .WithEnvironment("HF_ENDPOINT", "https://hf-mirror.com");

        // ─── 镜像 tag ─────────────────────────────────────────
        if (pinnedTag)
        {
            vllm = vllm.WithImageTag(imageTag!);
            Console.WriteLine($"[vLLM] 使用镜像 tag: {imageTag}");

            if (IsV0UnsupportedTag(imageTag!))
            {
                Console.WriteLine(
                    $"[vLLM] ⚠️ 警告：v{imageTag} 已删除 V0 引擎，" +
                    "Turing (SM 7.5) 将无法启动。请使用 v0.7.3。");
            }
        }

        // ─── 强制 V0 引擎（Turing 必须）───────────────────────
        if (forceV0Engine)
        {
            vllm = vllm.WithEnvironment("VLLM_USE_V1", "0");
            Console.WriteLine("[vLLM] 已强制 V0 引擎（VLLM_USE_V1=0）—— Turing 必需");
        }

        // ─── GPU 支持 ────────────────────────────────────────
        if (useGpu)
        {
            vllm = vllm.WithContainerRuntimeArgs("--gpus=all");
            Console.WriteLine("[vLLM] GPU 支持已启用（需宿主 Docker 已配置 NVIDIA Container Toolkit）");
        }
        else
        {
            Console.WriteLine("[vLLM] ⚠️ GPU 支持未启用，将使用 CPU 推理（极慢，不推荐）");
        }

        // ─── HuggingFace 相关环境变量 ─────────────────────────
        if (!string.IsNullOrWhiteSpace(hfToken))
        {
            vllm = vllm.WithEnvironment("HF_TOKEN", hfToken);
            Console.WriteLine("[vLLM] 已注入 HF_TOKEN（私有模型或加速下载）");
        }

        if (!string.IsNullOrWhiteSpace(hfEndpoint))
        {
            vllm = vllm.WithEnvironment("HF_ENDPOINT", hfEndpoint);
            Console.WriteLine($"[vLLM] HF_ENDPOINT = {hfEndpoint}");
        }

        resourceContext.Vllm = vllm;

        // ─── 2. 启动摘要 ─────────────────────────────────────
        Console.WriteLine(
            $"[vLLM] Chat={ChatModelName}, Embedding={EmbeddingModelName}, " +
            $"Context={contextLength}, Gpu={useGpu}, GpuMemUtil={gpuMemoryUtil:F2}, " +
            $"V0Engine={forceV0Engine}, Tag={imageTag ?? "latest"}");

        Console.WriteLine(
            "[vLLM] 💡 Turing / 6GB 配置：AWQ + FP16 + V0 + 4096 ctx + enforce-eager");

        return builder;
    }

    /// <summary>
    /// 判断给定 tag 是否已删除 V0 引擎（v0.11.1 起）。
    /// 支持 "v0.7.3" / "0.7.3" / "latest" 等格式。
    /// </summary>
    private static bool IsV0UnsupportedTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return false;
        if (tag.Equals("latest", StringComparison.OrdinalIgnoreCase)) return true;

        var cleaned = tag.StartsWith('v') || tag.StartsWith('V') ? tag[1..] : tag;
        var parts = cleaned.Split('.');
        if (parts.Length < 2) return false;
        if (!int.TryParse(parts[0], out var major)) return false;
        if (!int.TryParse(parts[1], out var minor)) return false;

        // v0.11.1 起删除 V0 引擎
        if (major > 0) return true;
        if (minor > 11) return true;
        if (minor == 11 && parts.Length >= 3 && int.TryParse(parts[2], out var patch))
        {
            return patch >= 1;
        }
        return false;
    }
}