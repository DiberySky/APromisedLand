using APromisedLand.AppHost;
using APromisedLand.AppHost.Extensions;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Xunit;

namespace APromisedLand.AppHost.Tests;

/// <summary>
/// TreeGraphMafWfExtension 编排接线回归测试：模型级断言（不启动 Docker/DCP），
/// 守护端口、WaitFor 依赖链、环境变量注入键、健康探针——这些改坏编译不报错，
/// 只有运行时才发现。
/// </summary>
public class TreeGraphMafWfExtensionTests
{
    private const string ResourceName = "TreeGraphMafWfApi";

    /// <summary>构建完整组合：Redis + vLLM 双容器 + MafWfApi。</summary>
    private static (IResource Api, ContainerResource Chat, ContainerResource Embed, RedisResource Redis) Build()
    {
        var builder = DistributedApplication.CreateBuilder();
        var context = new AppHostResourceContext();

        builder.AddRedis(context);
        builder.AddVllm(context);
        builder.AddTreeGraphMafWfApi(context);

        Assert.NotNull(context.TreeGraphMafWfApi);

        var api = builder.Resources.Single(r => r.Name == ResourceName);
        var chat = builder.Resources.OfType<ContainerResource>().Single(r => r.Name == "vllm");
        var embed = builder.Resources.OfType<ContainerResource>().Single(r => r.Name == "vllm-embed");
        var redis = builder.Resources.OfType<RedisResource>().Single(r => r.Name == "Redis");

        return (api, chat, embed, redis);
    }

    // ══════════════════════════════════════════════════════
    // 项目声明与端口
    // ══════════════════════════════════════════════════════

    [Fact]
    public void 资源已注册_为ProjectResource_固定端口5323()
    {
        var (api, _, _, _) = Build();

        Assert.IsAssignableFrom<IResourceWithEndpoints>(api);
        Assert.Contains(api.Annotations.OfType<EndpointAnnotation>(),
            e => e.Port == 5323 && e.Name == "http");
    }

    // ══════════════════════════════════════════════════════
    // WaitFor 依赖链
    // ══════════════════════════════════════════════════════

    [Fact]
    public void WaitFor链_覆盖Chat_Embed_Redis()
    {
        var (api, chat, embed, redis) = Build();

        var waits = api.Annotations.OfType<WaitAnnotation>()
            .Select(w => w.Resource)
            .ToList();

        Assert.Contains(chat, waits);
        Assert.Contains(embed, waits);
        Assert.Contains(redis, waits);
    }

    [Fact]
    public void 串行启动_Chat仍等EmbedHealthy_MafWfApi等Chat即可间接排队()
    {
        var (api, chat, embed, _) = Build();

        // Chat → Embed 串行边未被 MafWfApi 注册破坏
        Assert.Contains(chat.Annotations.OfType<WaitAnnotation>(),
            w => w.Resource == embed && w.WaitType == WaitType.WaitUntilHealthy);

        // MafWfApi → Chat 只需等 Running（模型就绪由 VllmWarmupService 保证）
        Assert.Contains(api.Annotations.OfType<WaitAnnotation>(),
            w => w.Resource == chat && w.WaitType == WaitType.WaitUntilHealthy);
    }

    // ══════════════════════════════════════════════════════
    // 环境变量注入键（Program.cs 的端点解析优先级依赖这些键）
    // ══════════════════════════════════════════════════════

    [Fact]
    public void 环境变量_Chat端点双键注入_VLLM_HTTP与Agent_Endpoint()
    {
        var (api, _, _, _) = Build();
        var env = EnvKeys(api);

        Assert.Contains("VLLM_HTTP", env);
        Assert.Contains("Agent__Endpoint", env);
        Assert.Contains("Agent__ChatModel", env);
    }

    [Fact]
    public void 环境变量_Embedding注入_模型名与维度引用常量()
    {
        var (api, _, _, _) = Build();
        var env = EnvVars(api)
            .ToDictionary(kv => kv.Key, kv => kv.Value?.ToString());

        Assert.True(env.ContainsKey("VLLM_EMBEDDING_HTTP"));
        Assert.True(env.ContainsKey("Embedding__Endpoint"));
        Assert.Equal(VllmExtension.ServedEmbeddingModelName, env["Embedding__Model"]);
        Assert.Equal(VllmExtension.EmbeddingDimension.ToString(), env["Embedding__Dimension"]);
        Assert.Equal(VllmExtension.ServedModelName, env["Agent__ChatModel"]);
    }

    // ══════════════════════════════════════════════════════
    // 健康探针
    // ══════════════════════════════════════════════════════

    [Fact]
    public void 健康探针_注册_api_health()
    {
        var (api, _, _, _) = Build();

        Assert.Contains(api.Annotations.OfType<HealthCheckAnnotation>(),
            c => c.Key.Contains("/api/health"));
    }

    private static HashSet<string> EnvKeys(IResource resource) =>
        EnvVars(resource).Select(kv => kv.Key).ToHashSet();

    /// <summary>
    /// 执行环境变量回调收集最终键值（WithEnvironment 的各重载统一落地为
    /// EnvironmentCallbackAnnotation；EnvironmentAnnotation 本身为 internal）。
    /// </summary>
    private static Dictionary<string, object> EnvVars(IResource resource)
    {
        Assert.True(resource.TryGetEnvironmentVariables(out var annotations));

        var context = new EnvironmentCallbackContext(
            new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run),
            resource);

        foreach (var ann in annotations)
            ann.Callback(context).GetAwaiter().GetResult();

        return new Dictionary<string, object>(context.EnvironmentVariables);
    }
}
