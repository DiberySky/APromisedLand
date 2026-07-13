// DiberyTreeHelper.cs (或直接放在 HttpClientHelper 中)

using APromisedLand.Maui.Authentication;
using APromisedLand.MauiBlazor.DiberyTree.Services;
using APromisedLand.Shared.DiberyTree.Interfaces;
using APromisedLand.Shared.Services;
using SolutionService = APromisedLand.Shared.Services.Solution.SolutionService;

namespace APromisedLand.Maui.DiberyTree;

public static class DiberyTreeClient
{
    /// <summary>
    /// 注册 DiberyTree 相关 HTTP 客户端和服务。
    /// </summary>
    /// <typeparam name="T">节点值的类型（通常为自定义 DTO）</typeparam>
    public static void AddDiberyTreeClient<T>(this MauiAppBuilder builder)
    {
        // 注册类型化 HTTP 客户端，自动添加 JWT 处理程序
        builder.Services.AddHttpClient<DiberyTreeApiClient<T>>(client =>
            {
                client.BaseAddress = new Uri(SolutionService.YarpHostBaseUrl);
            })
            .AddHttpMessageHandler<JwtAuthorizationMessageHandler>();

        // 注册 TreeServiceClient（实现 ITreeService<T>）
        builder.Services.AddScoped<ITreeService<T>, TreeServiceClient<T>>();
    }
}