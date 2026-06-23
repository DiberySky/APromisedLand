using APromisedLand.Maui.Authentication;
using APromisedLand.Maui.Helper;
using APromisedLand.Razor.Services;
using APromisedLand.Shared.Clients.Weather;
using APromisedLand.Shared.Helper;
using APromisedLand.Shared.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;

namespace DiberySky;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => { fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"); });

        builder.Services.AddMauiBlazorWebView();

        builder.Services.AddScoped<BlazorService>();
        builder.Services.AddScoped<ProjectService>();

        //AuthenticationServices(builder);
        MauiHelper.AuthenticationServices(builder);
        HttpClientHelper.AuthHttpClient(builder);

        //HttpClientServices(builder);
        HttpClientHelper.WeatherHttpClient(builder);

        // 添加 Blazor 授权核心服务
        builder.Services.AddAuthorizationCore();
        // 添加级联认证状态（在组件中可通过 CascadingAuthenticationState 获取）
        builder.Services.AddCascadingAuthenticationState();

        //builder.Services.AddMudServices();
        MauiHelper.MudBlazorServices(builder);

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    private static void HttpClientServices(MauiAppBuilder builder)
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

        // 用于调用受保护的后端 API（自动携带 JWT）
        builder.Services.AddHttpClient("WebApi", client =>
        {
            client.BaseAddress = new Uri(AuthHelper.YarpHttpHostAddress);
        }).AddHttpMessageHandler<JwtAuthorizationMessageHandler>();

        // 注册一个便利的 HttpClient 工厂或直接注入 IHttpClientFactory
        builder.Services.AddTransient(sp => sp.GetRequiredService<IHttpClientFactory>()
            .CreateClient("WebApi"));

        // 注册类型化 HttpClient（自动应用 JwtAuthorizationMessageHandler）
        builder.Services.AddHttpClient<WeatherApiClient>(client =>
        {
            client.BaseAddress = new Uri(AuthHelper.YarpHttpHostAddress);
        })
        .AddHttpMessageHandler<JwtAuthorizationMessageHandler>();

        // AuthService 使用 AuthClient
        builder.Services.AddScoped<AuthService>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var client = factory.CreateClient("AuthClient");
            var authState = sp.GetRequiredService<JwtAuthenticationStateProvider>();
            return new AuthService(client, authState);
        });
    }

    private static void AuthenticationServices(MauiAppBuilder builder)
    {
        // 注册 SecureStorage
        builder.Services.AddSingleton<ISecureStorage>(SecureStorage.Default);

        // 添加 Blazor 授权核心服务
        builder.Services.AddAuthorizationCore();
        // 添加级联认证状态（在组件中可通过 CascadingAuthenticationState 获取）
        builder.Services.AddCascadingAuthenticationState();

        // 注册自定义 AuthenticationStateProvider
        builder.Services.AddScoped<JwtAuthenticationStateProvider>();
        builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
            sp.GetRequiredService<JwtAuthenticationStateProvider>());

        // 注册 HttpClient 并添加自动附加令牌的处理程序
        builder.Services.AddScoped<JwtAuthorizationMessageHandler>();
    }
}