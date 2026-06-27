using APromisedLand.Maui.Authentication;
using APromisedLand.Shared.Clients;
using APromisedLand.Shared.Helper;
using APromisedLand.Shared.Intefaces;
using APromisedLand.Shared.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace APromisedLand.Maui.Helper;

public static class HttpClientHelper
{
    public static void WeatherHttpClient(MauiAppBuilder builder)
    {
        // 注册一个便利的 HttpClient 工厂或直接注入 IHttpClientFactory
        builder.Services.AddTransient(sp => sp.GetRequiredService<IHttpClientFactory>()
            .CreateClient("WebApi"));

        // 用于调用受保护的后端 API（自动携带 JWT）
        builder.Services
            .AddHttpClient("WebApi", client =>
            {
                client.BaseAddress = new Uri(ProjectService.YarpHostBaseUrl); 
                
            })
            .AddHttpMessageHandler<JwtAuthorizationMessageHandler>();


        // 注册类型化 HttpClient（自动应用 JwtAuthorizationMessageHandler）
        builder.Services.AddHttpClient<WeatherApiClient>(client =>
            {
                client.BaseAddress = new Uri(ProjectService.YarpHostBaseUrl);
            })
            .AddHttpMessageHandler<JwtAuthorizationMessageHandler>();
    }

    public static void AuthHttpClient(MauiAppBuilder builder)
    {
        // RefreshClient
        builder.Services.AddHttpClient("RefreshClient", client =>
            {
                client.BaseAddress = new Uri(ProjectService.KeyCloakHttpsBaseUrl); // Keycloak 地址
            })
            .ConfigurePrimaryHttpMessageHandler(CreateHttpClientHandler);

        // 用于调用 Keycloak 登录/注册等无需 JWT 的请求
        builder.Services.AddHttpClient("AuthClient", client =>
            {
                client.BaseAddress = new Uri(ProjectService.KeyCloakHttpsBaseUrl); // Keycloak 地址
            })
            .ConfigurePrimaryHttpMessageHandler(CreateHttpClientHandler);

        // AuthService 使用 AuthClient
        builder.Services.AddScoped<IAuthService, AuthService>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var client = factory.CreateClient("AuthClient");
            var authState = sp.GetRequiredService<JwtAuthenticationStateProvider>();
            return new AuthService(client, authState);
        });
    }

    // 辅助方法：创建 HttpClientHandler，Debug 模式忽略 SSL 验证
    private static HttpClientHandler CreateHttpClientHandler()
    {
        var handler = new HttpClientHandler();
#if DEBUG
        // 仅开发环境使用，切勿在生产环境启用！
        handler.ServerCertificateCustomValidationCallback =
            (message, cert, chain, errors) => true;
#endif
        return handler;
    }
}