using APromisedLand.AppHost;
using APromisedLand.AppHost.Extensions;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Xunit;

namespace APromisedLand.AppHost.Tests;

/// <summary>
/// 语义搜索编排接线回归测试（模型级，不启动 Docker/DCP）：
/// 核心 API 只保留业务依赖，SyncServices 独立承载搜索/同步并等待核心健康，
/// MafWfApi 与 Blazor 同时获得核心和 SyncServices 两个不同地址。
/// </summary>
public class SemanticSearchWiringTests
{
    private sealed record Wiring(
        IResource EavApi,
        IResource SyncServices,
        IResource MafWfApi,
        IResource Blazor,
        QdrantServerResource Qdrant,
        IResourceWithConnectionString RabbitMq,
        ContainerResource VllmEmbed);

    private static Wiring Build()
    {
        var builder = DistributedApplication.CreateBuilder();
        var context = new AppHostResourceContext();

        builder.AddPostgres(context);
        builder.AddRedis(context);
        builder.AddRabbitMq(context);
        builder.AddQdrant(context);
        builder.AddVllm(context);
        builder.AddTreeGraphEavApi(context);
        builder.AddTreeGraphSyncServices(context);
        builder.AddTreeGraphMafWfApi(context);
        builder.AddTreeGraphBlazor(context);

        return new Wiring(
            builder.Resources.Single(r => r.Name == "treegrapheavapi"),
            builder.Resources.Single(r => r.Name == "treegraphsyncservices"),
            builder.Resources.Single(r => r.Name == "TreeGraphMafWfApi"),
            builder.Resources.Single(r => r.Name == "treegraphblazor"),
            builder.Resources.OfType<QdrantServerResource>().Single(r => r.Name == "Qdrant"),
            builder.Resources.OfType<IResourceWithConnectionString>()
                .Single(r => r.Name == "RabbitMQ"),
            builder.Resources.OfType<ContainerResource>().Single(r => r.Name == "vllm-embed"));
    }

    [Fact]
    public void Qdrant_固定端口6333与6334()
    {
        var wiring = Build();

        var endpoints = wiring.Qdrant.Annotations.OfType<EndpointAnnotation>().ToList();
        Assert.Contains(endpoints, e => e.Name == "grpc" && e.Port == 6334);
        Assert.Contains(endpoints, e => e.Name == "rest" && e.Port == 6333);
    }

    [Fact]
    public void Qdrant_REST端口挂healthz探针_Persistent生命周期()
    {
        var wiring = Build();

        Assert.Contains(wiring.Qdrant.Annotations.OfType<HealthCheckAnnotation>(),
            h => h.Key.Contains("healthz"));
        Assert.Contains(wiring.Qdrant.Annotations.OfType<ContainerLifetimeAnnotation>(),
            l => l.Lifetime == ContainerLifetime.Persistent);
    }

    [Fact]
    public void EavApi_保留RabbitMQ_不再等待Qdrant与VllmEmbed或注入Embedding()
    {
        var wiring = Build();
        var waits = wiring.EavApi.Annotations.OfType<WaitAnnotation>()
            .Select(w => w.Resource).ToList();

        Assert.Contains(waits, r => ReferenceEquals(r, wiring.RabbitMq));
        Assert.DoesNotContain(wiring.Qdrant, waits);
        Assert.DoesNotContain(wiring.VllmEmbed, waits);
        Assert.DoesNotContain("Embedding__Endpoint", EnvVars(wiring.EavApi).Keys);
    }

    [Fact]
    public void SyncServices_注册独立项目资源_固定端口5775与健康探针()
    {
        var wiring = Build();

        Assert.IsType<ProjectResource>(wiring.SyncServices);
        Assert.Contains(wiring.SyncServices.Annotations.OfType<EndpointAnnotation>(),
            e => e.Name == "http" && e.Port == 5775);
        Assert.Contains(wiring.SyncServices.Annotations.OfType<HealthCheckAnnotation>(),
            h => h.Key.Contains("/health"));
    }

    [Fact]
    public void SyncServices_等待核心健康及RabbitMQ_Qdrant_VllmEmbed()
    {
        var wiring = Build();
        var waits = wiring.SyncServices.Annotations.OfType<WaitAnnotation>().ToList();

        Assert.Contains(waits, w => ReferenceEquals(w.Resource, wiring.EavApi)
            && w.WaitType == WaitType.WaitUntilHealthy);
        Assert.Contains(waits, w => ReferenceEquals(w.Resource, wiring.RabbitMq));
        Assert.Contains(waits, w => ReferenceEquals(w.Resource, wiring.Qdrant));
        Assert.Contains(waits, w => ReferenceEquals(w.Resource, wiring.VllmEmbed));
    }

    [Fact]
    public void SyncServices_注入Embedding端点_带v1后缀()
    {
        var wiring = Build();
        var env = EnvVars(wiring.SyncServices);

        Assert.True(env.TryGetValue("Embedding__Endpoint", out var embed));
        var text = embed switch
        {
            ReferenceExpression re => re.Format,
            _ => embed?.ToString()
        };
        Assert.Contains("/v1/", text);
    }

    [Fact]
    public void MafWfApi_分别等待并注入核心与SyncServices地址()
    {
        var wiring = Build();
        var waits = wiring.MafWfApi.Annotations.OfType<WaitAnnotation>()
            .Select(w => w.Resource).ToList();
        var env = EnvVars(wiring.MafWfApi);

        Assert.Contains(wiring.EavApi, waits);
        Assert.Contains(wiring.SyncServices, waits);
        Assert.True(env.TryGetValue("TreeGraphApi__BaseAddress", out var core));
        Assert.True(env.TryGetValue("TreeGraphSyncServices__BaseAddress", out var sync));
        Assert.NotSame(core, sync);
    }

    [Fact]
    public void Blazor_等待SyncServices并获得独立服务发现地址()
    {
        var wiring = Build();
        var env = EnvVars(wiring.Blazor);

        Assert.Contains(wiring.Blazor.Annotations.OfType<WaitAnnotation>(),
            w => ReferenceEquals(w.Resource, wiring.SyncServices));
        Assert.Contains("services__treegrapheavapi__http__0", env.Keys);
        Assert.Contains("services__treegraphsyncservices__http__0", env.Keys);
    }

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
