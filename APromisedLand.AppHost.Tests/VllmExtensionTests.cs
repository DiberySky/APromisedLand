using APromisedLand.AppHost;
using APromisedLand.AppHost.Extensions;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Xunit;

namespace APromisedLand.AppHost.Tests;

/// <summary>
/// VllmExtension 资源模型单元测试：直接调用 AddVllm 构建模型（不启动 Docker/DCP），
/// 断言 6GB 单卡双容器的串行启动机制与健康检查约定。
/// </summary>
public class VllmExtensionTests
{
    /// <summary>构建资源模型并取出两个容器资源。</summary>
    private static (ContainerResource Chat, ContainerResource Embed) Build(
        Action<IDictionary<string, string?>>? configure = null)
    {
        var builder = DistributedApplication.CreateBuilder();

        // 配置覆盖（Configuration 是可变字典，直接写入生效）
        var overrides = new Dictionary<string, string?>();
        configure?.Invoke(overrides);
        foreach (var kv in overrides)
            builder.Configuration[kv.Key] = kv.Value;

        var context = new AppHostResourceContext();
        builder.AddVllm(context);

        var chat = builder.Resources
            .OfType<ContainerResource>()
            .Single(r => r.Name == "vllm");
        var embed = builder.Resources
            .OfType<ContainerResource>()
            .Single(r => r.Name == "vllm-embed");

        // context 必须被回填（下游扩展依赖）
        Assert.NotNull(context.Vllm);
        Assert.NotNull(context.VllmEmbed);

        return (chat, embed);
    }

    // ══════════════════════════════════════════════════════
    // 串行启动机制（本次新增的核心）
    // ══════════════════════════════════════════════════════

    [Fact]
    public void Chat容器_WaitFor_Embed容器_等其进入Healthy()
    {
        var (chat, embed) = Build();

        var wait = chat.Annotations.OfType<WaitAnnotation>()
            .SingleOrDefault(w => w.Resource == embed);

        Assert.NotNull(wait);
        // 目标有健康检查时，等待其 Healthy（模型加载完毕）而非仅 Running
        Assert.Equal(WaitType.WaitUntilHealthy, wait.WaitType);
    }

    [Fact]
    public void Embed容器_不等待任何资源()
    {
        var (_, embed) = Build();

        Assert.Empty(embed.Annotations.OfType<WaitAnnotation>());
    }

    [Fact]
    public void Embed容器_注册Health健康检查_作为串行启动同步点()
    {
        var (_, embed) = Build();

        // WithHttpHealthCheck 生成 HealthCheckAnnotation，Key 含探测 URL
        var checks = embed.Annotations.OfType<HealthCheckAnnotation>().ToList();

        Assert.NotEmpty(checks);
        Assert.Contains(checks, c => c.Key.Contains("/health"));
    }

    [Fact]
    public void Chat容器_不注册健康检查_避免长加载窗口误报()
    {
        var (chat, _) = Build();

        Assert.Empty(chat.Annotations.OfType<HealthCheckAnnotation>());
    }

    // ══════════════════════════════════════════════════════
    // 端点与生命周期
    // ══════════════════════════════════════════════════════

    [Fact]
    public void 端点_固定宿主端口_Chat5723_Embed5719()
    {
        var (chat, embed) = Build();

        Assert.Contains(chat.Annotations.OfType<EndpointAnnotation>(),
            e => e.Port == VllmExtension.ChatHostPort);
        Assert.Contains(embed.Annotations.OfType<EndpointAnnotation>(),
            e => e.Port == VllmExtension.EmbeddingHostPort);
    }

    [Fact]
    public void 双容器_均为Persistent生命周期()
    {
        var (chat, embed) = Build();

        Assert.Equal(ContainerLifetime.Persistent,
            chat.Annotations.OfType<ContainerLifetimeAnnotation>().Single().Lifetime);
        Assert.Equal(ContainerLifetime.Persistent,
            embed.Annotations.OfType<ContainerLifetimeAnnotation>().Single().Lifetime);
    }

    // ══════════════════════════════════════════════════════
    // 显存预算（appsettings 工作点 0.75/0.25 对应的配置键）
    // ══════════════════════════════════════════════════════

    [Fact]
    public void 显存预算_默认值为Chat060Embed030()
    {
        // 测试环境无 AppHost appsettings.json，读到的是代码默认值
        var (chat, embed) = Build();

        var chatArgs = GetArgs(chat);
        var embedArgs = GetArgs(embed);

        Assert.Contains("0.60", chatArgs);
        Assert.Contains("0.30", embedArgs);
    }

    [Fact]
    public void 显存预算_配置覆盖生效()
    {
        var (chat, embed) = Build(cfg =>
        {
            cfg["Vllm:GpuMemoryUtilization"] = "0.75";
            cfg["Vllm:EmbedGpuMemoryUtilization"] = "0.25";
            cfg["Vllm:ContextLength"] = "3072";
        });

        var chatArgs = GetArgs(chat).ToList();
        var embedArgs = GetArgs(embed).ToList();

        Assert.Contains("0.75", chatArgs);
        Assert.Contains("3072", chatArgs);
        Assert.Contains("0.25", embedArgs);
    }

    [Fact]
    public void 模型名_引用常量_禁止字面量漂移()
    {
        var (chat, embed) = Build();

        Assert.Contains(VllmExtension.ChatModelName, GetArgs(chat));
        Assert.Contains(VllmExtension.ServedModelName, GetArgs(chat));
        Assert.Contains(VllmExtension.EmbeddingModelName, GetArgs(embed));
        Assert.Contains(VllmExtension.ServedEmbeddingModelName, GetArgs(embed));
    }

    /// <summary>从资源注解收集命令行参数（解析静态字符串值）。</summary>
    private static IEnumerable<string> GetArgs(ContainerResource resource)
    {
        // WithArgs 的参数存于 CommandLineArgsCallbackAnnotation，
        // 需要执行回调才能拿到。构造一个 fake context 收集。
        var collected = new List<object>();
        foreach (var ann in resource.Annotations
                     .OfType<CommandLineArgsCallbackAnnotation>())
        {
            var ctx = new CommandLineArgsCallbackContext(collected, resource);
            ann.Callback(ctx).GetAwaiter().GetResult();
        }

        foreach (var a in collected)
        {
            if (a is string s) yield return s;
        }
    }
}
