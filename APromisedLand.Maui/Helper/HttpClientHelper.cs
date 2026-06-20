using APromisedLand.Maui.Authentication;
using APromisedLand.Shared.Clients.Weather;
using APromisedLand.Shared.Helper;
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
        builder.Services.AddHttpClient("WebApi", client =>
        {
            client.BaseAddress = new Uri(AuthHelper.YarpHttpHostAddress);
        }).AddHttpMessageHandler<JwtAuthorizationMessageHandler>();


        // 注册类型化 HttpClient（自动应用 JwtAuthorizationMessageHandler）
        builder.Services.AddHttpClient<WeatherApiClient>(client =>
        {
            client.BaseAddress = new Uri(AuthHelper.YarpHttpHostAddress);
        })
        .AddHttpMessageHandler<JwtAuthorizationMessageHandler>();
    }

    public static void AuthHttpClient(MauiAppBuilder builder)
    {


        // RefreshClient
        builder.Services.AddHttpClient("RefreshClient", client =>
        {
            client.BaseAddress = new Uri(AuthHelper.KeyCloakHttpsHostsAddress); // Keycloak 地址
        });

        // 用于调用 Keycloak 登录/注册等无需 JWT 的请求
        builder.Services.AddHttpClient("AuthClient", client =>
        {
            client.BaseAddress = new Uri(AuthHelper.KeyCloakHttpsHostsAddress); // Keycloak 地址
        });

        // AuthService 使用 AuthClient
        builder.Services.AddScoped<AuthService>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var client = factory.CreateClient("AuthClient");
            var authState = sp.GetRequiredService<JwtAuthenticationStateProvider>();
            return new AuthService(client, authState);
        });
    }
}
