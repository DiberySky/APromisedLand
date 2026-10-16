using APromisedLand.AppHost;
using APromisedLand.AppHost.Extensions;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Xunit;

namespace APromisedLand.AppHost.Tests;

/// <summary>
/// 语义搜索编排接线回归测试（模型级，不启动 Docker/DCP）：
///   - Qdrant 资源：固定端口、数据卷、/healthz 探针、Persistent
///   - TreeGraphEavApi：RabbitMQ / Qdrant / VllmEmbed 依赖链与环境变量注入
///   - TreeGraphMafWfApi：TreeGraphEavApi 端点注入（SemanticSearchNodes 工具）
/// 这些接线改坏编译不报错，只有运行时才发现。
/// </summary>
public class SemanticSearchWiringTests
{
    private static (
        IResource EavApi,
        IResource MafWfApi,
        QdrantServerResource Qdrant,
        IResourceWithConnectionString RabbitMq,
        ContainerResource VllmEmbed) Build()
    {
        var builder = DistributedApplication.CreateBuilder();
        var context = new AppHostResourceContext();

        builder.AddPostgres(context);
        builder.AddRedis(context);
        builder.AddRabbitMq(context);
        builder.AddQdrant(context);
        builder.AddVllm(context);
        builder.AddTreeGraphEavApi(context);
        builder.AddTreeGraphMafWfApi(context);

        var eavApi = builder.Resources.Single(r => r.Name == "treegrapheavapi");
        var mafWfApi = builder.Resources.Single(r => r.Name == "TreeGraphMafWfApi");
        var qdrant = builder.Resources.OfType<QdrantServerResource>().Single(r => r.Name == "Qdrant");
        var rabbitMq = builder.Resources.OfType<IResourceWithConnectionString>()
            .Single(r => r.Name == "RabbitMQ");
        var vllmEmbed = builder.Resources.OfType<ContainerResource>()
            .Single(r => r.Name == "vllm-embed");

        return (eavApi, mafWfApi, qdrant, rabbitMq, vllmEmbed);
    }

    // ══════════════════════════════════════════════════════
    // Qdrant 资源
    // ══════════════════════════════════════════════════════

    [Fact]
    public void Qdrant_固定端口6333与6334()
    {
        var (_, _, qdrant, _, _) = Build();

        var endpoints = qdrant.Annotations.OfType<EndpointAnnotation>().ToList();
        Assert.Contains(endpoints, e => e.Name == "grpc" && e.Port == 6334);
        Assert.Contains(endpoints, e => e.Name == "rest" && e.Port == 6333);
    }

    [Fact]
    public void Qdrant_REST端口挂healthz探针_Persistent生命周期()
    {
        var (_, _, qdrant, _, _) = Build();

        Assert.Contains(qdrant.Annotations.OfType<HealthCheckAnnotation>(),
            h => h.Key.Contains("healthz"));

        Assert.Contains(qdrant.Annotations.OfType<ContainerLifetimeAnnotation>(),
            l => l.Lifetime == ContainerLifetime.Persistent);
    }

    // ══════════════════════════════════════════════════════
    // TreeGraphEavApi 依赖链
    // ══════════════════════════════════════════════════════

    [Fact]
    public void EavApi_WaitFor链_覆盖RabbitMQ_Qdrant_VllmEmbed()
    {
        var (eavApi, _, qdrant, rabbitMq, vllmEmbed) = Build();

        var waits = eavApi.Annotations.OfType<WaitAnnotation>()
            .Select(w => w.Resource).ToList();

        Assert.Contains(qdrant, waits);
        Assert.Contains(vllmEmbed, waits);
        Assert.Contains(waits, r => ReferenceEquals(r, rabbitMq));
    }

    [Fact]
    public void EavApi_注入Embedding端点_带v1后缀()
    {
        var (eavApi, _, _, _, _) = Build();
        var env = EnvVars(eavApi);

        Assert.True(env.TryGetValue("Embedding__Endpoint", out var embed));

        // 模型级未分配端口时值为 ReferenceExpression（Format 形如 "{0}/v1/"），
        // 已分配时为解析后的字符串——两种形态都校验 /v1/ 后缀
        var text = embed switch
        {
            ReferenceExpression re => re.Format,
            _ => embed?.ToString()
        };
        Assert.Contains("/v1/", text);
    }

    // ══════════════════════════════════════════════════════
    // MafWfApi → EavApi（语义搜索工具）
    // ══════════════════════════════════════════════════════

    [Fact]
    public void MafWfApi_WaitFor并注入TreeGraphEavApi端点()
    {
        var (eavApi, mafWfApi, _, _, _) = Build();

        Assert.Contains(mafWfApi.Annotations.OfType<WaitAnnotation>(),
            w => ReferenceEquals(w.Resource, eavApi));

        Assert.Contains("TreeGraphApi__BaseAddress", EnvVars(mafWfApi).Keys);
    }

    /// <summary>执行环境变量回调，收集最终注入的键值。</summary>
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
