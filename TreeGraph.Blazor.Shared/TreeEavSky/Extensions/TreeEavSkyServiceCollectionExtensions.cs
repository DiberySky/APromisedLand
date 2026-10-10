using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Extensions;
using TreeGraph.Blazor.Shared.TreeEavSky.Dialogs;
using TreeGraph.Blazor.Shared.TreeEavSky.Navigation;
using TreeGraph.Blazor.Shared.TreeEavSky.Services;

namespace TreeGraph.Blazor.Shared.TreeEavSky.Extensions;
public static class TreeEavSkyServiceCollectionExtensions
{
    /// <summary>
    /// 注册 TreeSky 组件库所需服务：
    /// BlazorService、MessageService、TreeNodeDialogService&lt;&gt;、导航历史、TreeApiClient&lt;&gt;。
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="httpClientName">内部 HttpClient 名称（默认 TreeSky）</param>
    /// <param name="configureClient">可选：配置 API 基地址等</param>
    /// <remarks>
    /// 前置条件：宿主需已调用 <c>services.AddMudServices()</c>（内部 AddMudExtensions
    /// 依赖 MudBlazor 服务已注册，否则启动时抛 InvalidOperationException）。
    /// 泛型 <see cref="ITreeClientService{TTree}"/> 由宿主按具体节点类型自行注册实现。
    /// </remarks>
    public static IServiceCollection AddTreeEavSky(
        this IServiceCollection services,
        string httpClientName = "TreeEavSky",
        Action<HttpClient>? configureClient = null,
        Action<IHttpClientBuilder>? configureClientBuilder = null)
    {
        // MudBlazor.Extensions（ShowExAsync / DialogOptionsEx 运行时依赖）
        services.AddMudExtensions();

        services.AddScoped<BlazorService>();
        services.AddScoped<MessageService>();
        services.AddScoped(typeof(TreeNodeDialogService<>));
        services.AddScoped<ITreeNavigationHistoryService, TreeNavigationHistoryService>();

        // 泛型树 API 客户端（open generic，经命名 HttpClient 工厂获取 HttpClient）
        services.AddScoped(typeof(TreeApiClient<>));

        // 默认树节点写操作 Handler（宿主可注册同接口实现覆盖）
        services.AddScoped(typeof(ITreeActionHandler<>), typeof(DefaultTreeActionHandler<>));

        var builder = services.AddHttpClient(httpClientName);
        if (configureClient != null)
        {
            builder.ConfigureHttpClient(configureClient);
        }

        // 宿主可在此附加弹性策略 / 故障处理（写操作建议 NonIdempotentResilience）
        configureClientBuilder?.Invoke(builder);

        // 注意：必须用 AddTransient（非 TryAdd）。AddHttpClient(name) 内部已 TryAdd 注册过
        // 一个指向无名客户端的 HttpClient，TryAdd 会静默失效，导致 ApiClient 拿到
        // 没有 BaseAddress / 消息处理器的 HttpClient。
        services.AddTransient(sp =>
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(httpClientName));

        return services;
    }
}
