using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;

namespace APromisedLand.AppHost.Extensions;

/// <summary>
/// vLLM 容器 + OpenAI 兼容端点的统一注册点。
/// 该文件是全项目 AI 模型名的唯一数据源。
///
/// ★ 容器拓扑：
///   - vllm        : Chat 模型  (Qwen3-4B-AWQ)   宿主端口 5723 → 容器 8000
///   - vllm-embed  : Embedding  (BAAI/bge-m3)    宿主端口 5719 → 容器 8000
///
/// ★ 健康检查与串行启动策略：
///   - Chat 容器不注册 Aspire 探针：/health 在 HTTP 服务起来后就返回 200，
///     但模型权重仍在异步加载（30s~数分钟），启动窗口必然失败误报。
///   - Embed 容器注册 /health 探针，作为 Chat 的串行启动同步点：
///     6GB 单卡双容器同时加载会竞争显存（Embed 加载中的占用被 Chat 探活
///     计入 non_torch_memory，KV Cache 预算为负、Chat 引擎必崩，已实测复现）。
///     Chat 通过 WaitFor(vllmEmbed) 等 Embed Healthy（模型加载完、显存稳定）
///     后才启动。V0 引擎下 uvicorn 在模型加载完成后才对外服务，
///     故 /health 200 ≈ 模型就绪，探针误报窗口短、可接受。
///   - 业务层就绪判断不变：MafRagApi/MafWfApi 的 VllmWarmupService 预热，
///     /api/health/deep 提供 readiness 探活。下游项目的 WaitFor(vllm)
///     只等 Chat 容器进入 Running，不等模型加载完成。
///
/// ★ 离线与镜像源：
///   - 默认 HF_HUB_OFFLINE=1 / TRANSFORMERS_OFFLINE=1：模型完全依赖共享卷
///     vllm-hf-cache 中的既有缓存；新机器/新模型首次运行前需预热缓存卷，
///     或将 Vllm:HfOffline 设为 false 走网络下载。
///   - HF_ENDPOINT 仅在非离线模式下生效（默认 https://hf-mirror.com）。
///
/// ★ 硬件约束（GTX 1660 Ti / Turing / SM 7.5 / 6GB）：
///   - vLLM V1 引擎要求 SM 8.0+    → 必须用 VLLM_USE_V1=0 回退 V0
///   - Turing 不支持 bfloat16       → 必须 --dtype float16
///   - 6GB 装不下 FP16 4B 权重     → 必须用 AWQ 量化模型
///   - 必须用 v0.11 之前的版本      → v0.11.0 起已删除 V0 引擎
///   - 双容器共享同一块 6GB GPU    → 显存预算拆分：Chat 默认 0.60、
///     Embed 默认 0.30（配置键 Vllm:GpuMemoryUtilization /
///     Vllm:EmbedGpuMemoryUtilization），之和超过阈值时打印 OOM 风险告警
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

    /// <summary>6GB 单卡双容器场景下，两容器显存占比之和超过该值即告警。</summary>
    private const double GpuMemoryUtilizationSumLimit = 0.92;

    public static IDistributedApplicationBuilder AddVllm(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        // ─── 可配置项 ──────────────────────────────────────────
        var useGpu = builder.Configuration.GetValue("Vllm:UseGpu", true);

        // 仅当用户显式配置了 Vllm:ImageTag 才视为"固定自定义 tag"，
        // 否则回退到 TuringSafeImageTag（qwen3-tf451 非版本号，不做 V0 校验）。
        var configuredTag = builder.Configuration.GetValue<string?>("Vllm:ImageTag");
        var pinnedTag = !string.IsNullOrWhiteSpace(configuredTag);
        var imageTag = pinnedTag ? configuredTag!.Trim() : TuringSafeImageTag;

        var contextLength = builder.Configuration
            .GetValue("Vllm:ContextLength", RecommendedContextLength);

        // 双容器共享同一块 6GB GPU：各自申请 0.85 会并发 OOM，必须拆分预算。
        var gpuMemoryUtil = builder.Configuration
            .GetValue("Vllm:GpuMemoryUtilization", 0.60);
        var embedGpuMemoryUtil = builder.Configuration
            .GetValue("Vllm:EmbedGpuMemoryUtilization", 0.30);

        var hfToken = builder.Configuration.GetValue<string?>("Vllm:HfToken");

        var hfEndpoint = builder.Configuration
            .GetValue<string?>("Vllm:HfEndpoint") ?? "https://hf-mirror.com";

        // 默认离线：模型依赖共享卷已有缓存。首次预热或换模型时置为 false。
        var hfOffline = builder.Configuration.GetValue("Vllm:HfOffline", true);

        var forceV0Engine = builder.Configuration
            .GetValue("Vllm:ForceV0Engine", true);

        // ─── 启动前告警 ────────────────────────────────────────
        if (pinnedTag && IsV0UnsupportedTag(imageTag))
        {
            Console.WriteLine(
                $"[vLLM] ⚠️ 警告：镜像 v{imageTag} 已删除 V0 引擎，" +
                "Turing (SM 7.5) 将无法启动。请改用 v0.11 之前的版本（项目验证过 v0.7.3）。");
        }

        if (useGpu && gpuMemoryUtil + embedGpuMemoryUtil > GpuMemoryUtilizationSumLimit)
        {
            Console.WriteLine(
                "[vLLM] ⚠️ 警告：Chat 与 Embedding 的 gpu-memory-utilization 之和超过 " +
                $"{GpuMemoryUtilizationSumLimit:F2}，双容器共享同一块 GPU 时并发加载可能 OOM。");
        }

        // ══════════════════════════════════════════════════════
        // 1. Chat 容器
        // ══════════════════════════════════════════════════════
        var vllm = builder.AddContainer("vllm", "vllm/vllm-openai")
            .WithImageTag(imageTag)
            .WithEndpoint(port: ChatHostPort, targetPort: ContainerPort, scheme: "http", name: HttpEndpointName, isProxied: false)
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
            .WithEnvironment("HF_HUB_OFFLINE", hfOffline ? "1" : "0")
            .WithEnvironment("TRANSFORMERS_OFFLINE", hfOffline ? "1" : "0");

        // ★ Chat 自身仍不注册 WithHttpHealthCheck（见文件头注释），
        //   但必须等 Embed 健康后再启动——6GB 单卡双容器同时加载会竞争显存：
        //   Embed 加载中的占用会被 Chat 探活计入 non_torch_memory，
        //   导致 KV Cache 预算为负、Chat 引擎启动失败（已实测复现）。
        //   WaitFor 在目标有健康检查时等待其进入 Healthy（模型加载完毕），
        //   此时 Embed 进入常驻空闲态，Chat 探活时 non_torch ≈ 0，预算成立。

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
            .WithImageTag(imageTag)
            .WithEndpoint(port: EmbeddingHostPort, targetPort: ContainerPort, scheme: "http", name: EmbeddingHttpEndpointName, isProxied: false)
            .WithVolume(HuggingFaceCacheVolumeName, "/root/.cache/huggingface")
            .WithLifetime(ContainerLifetime.Persistent)
            .WithEntrypoint("python3")
            .WithArgs(
                "-m", "vllm.entrypoints.openai.api_server",
                "--model", EmbeddingModelName,
                "--served-model-name", ServedEmbeddingModelName,
                "--task", "embed",
                "--max-model-len", contextLength.ToString(),
                "--dtype", "float16",
                "--gpu-memory-utilization", embedGpuMemoryUtil.ToString("F2"),
                "--enforce-eager"
            )
            .WithEnvironment("VLLM_USE_V1", forceV0Engine ? "0" : "1")
            .WithEnvironment("HF_ENDPOINT", hfEndpoint)
            .WithEnvironment("HF_HUB_OFFLINE", hfOffline ? "1" : "0")
            .WithEnvironment("TRANSFORMERS_OFFLINE", hfOffline ? "1" : "0");

        // ★ Embed 注册 /health 健康检查，作为 Chat 串行启动的同步点。
        //   V0 引擎下 uvicorn 在模型加载完成后才开始对外服务，
        //   因此 /health 200 ≈ 模型就绪；探针失败期间只影响 Dashboard 显示，
        //   不影响 Embed 自身加载（与文件头"不塞探针"的折衷：Embed 模型小、
        //   加载窗口短，探针误报窗口可接受，换来 Chat 启动确定性）。
        vllmEmbed = vllmEmbed.WithHttpHealthCheck(
            path: "/health",
            statusCode: 200,
            endpointName: EmbeddingHttpEndpointName);

        if (useGpu)
        {
            vllmEmbed = vllmEmbed.WithContainerRuntimeArgs("--gpus=all");
        }

        if (!string.IsNullOrWhiteSpace(hfToken))
            vllmEmbed = vllmEmbed.WithEnvironment("HF_TOKEN", hfToken);

        resourceContext.VllmEmbed = vllmEmbed;

        // ★ 串行启动：Chat 等 Embed Healthy（模型加载完、显存占用稳定）后才启动
        vllm = vllm.WaitFor(vllmEmbed);
        resourceContext.Vllm = vllm;

        // ══════════════════════════════════════════════════════
        // 3. 启动摘要
        // ══════════════════════════════════════════════════════
        Console.WriteLine(
            $"[vLLM] Chat={ChatModelName} (:{ChatHostPort}→{ContainerPort}, gpu-mem={gpuMemoryUtil:F2}), " +
            $"Embed={EmbeddingModelName} (:{EmbeddingHostPort}→{ContainerPort}, gpu-mem={embedGpuMemoryUtil:F2}), " +
            $"Context={contextLength}, Gpu={useGpu}, Offline={hfOffline}, Tag={imageTag}");

        return builder;
    }

    /// <summary>
    /// 判断镜像 tag 是否属于已删除 V0 引擎的版本（v0.11.0 起，含 latest）。
    /// 自定义 tag（如 qwen3-tf451）无法解析为版本号时返回 false。
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

        // v0.11.0 起删除 V0 引擎（与文件头注释一致）；v1.x 同理。
        return major > 0 || minor >= 11;
    }
}
