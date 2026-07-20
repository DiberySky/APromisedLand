using APromisedLand.Maui.Authentication;
using APromisedLand.Razor.Services;
using APromisedLand.Shared.DiberyTree.Models;
using APromisedLand.Shared.Interfaces;
using APromisedLand.Shared.Services.Solution;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using MudBlazor.Services;

namespace APromisedLand.Maui.Configs;

public static class DiberyConfig
{
    extension(MauiAppBuilder builder)
    {
        public void AddDiberyConfig()
        {
            builder.Services.AddMudServices();
            builder.AddMudBlazorServices();

            builder.Services.AddScoped<BlazorService>();
            builder.Services.AddScoped<SolutionService>();

            builder.AddPlatformInfo();

            builder.AddAuthenticationServices();
            builder.AddKeycloakClient();
        }

        public void AddAuthenticationServices()
        {
            // 注册 SecureStorage
            builder.Services.AddSingleton(SecureStorage.Default);

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
            
            // 新增：树节点权限处理器
            // builder.Services.AddScoped<
            //     ITreeNodeAuthorizationHandler<CategoryTree>,
            //     KeycloakTreeNodeAuthorizationHandler<CategoryTree>>();
        }

        public void AddMudBlazorServices()
        {
            builder.Services.AddMudServices(config =>
            {
                config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomLeft;
                config.SnackbarConfiguration.RequireInteraction = false;
                config.SnackbarConfiguration.PreventDuplicates = false;
                config.SnackbarConfiguration.NewestOnTop = false;
                config.SnackbarConfiguration.ShowCloseIcon = true;
                config.SnackbarConfiguration.VisibleStateDuration = 10000;
                config.SnackbarConfiguration.HideTransitionDuration = 500;
                config.SnackbarConfiguration.ShowTransitionDuration = 500;
                config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
            });
        }

        public void AddKeycloakClient()
        {
            var keyCloakHttpsBaseUrl = builder.Configuration["services:Keycloak:https:0"];
            if (keyCloakHttpsBaseUrl is null) keyCloakHttpsBaseUrl = SolutionService.KeyCloakHttpsBaseUrl;

            // RefreshClient
            builder.Services.AddHttpClient("RefreshClient", client =>
                {
                    client.BaseAddress = new Uri(keyCloakHttpsBaseUrl); // Keycloak 地址
                })
                .ConfigurePrimaryHttpMessageHandler(CreateHttpClientHandler);

            // 用于调用 Keycloak 登录/注册等无需 JWT 的请求
            builder.Services.AddHttpClient("AuthClient", client =>
                {
                    client.BaseAddress = new Uri(keyCloakHttpsBaseUrl); // Keycloak 地址
                })
                .ConfigurePrimaryHttpMessageHandler(CreateHttpClientHandler);

            // AuthService 使用 AuthClient
            builder.Services.AddScoped<IAuthenticationService, AuthenticationService>(sp =>
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                var client = factory.CreateClient("AuthClient");
                var authState = sp.GetRequiredService<JwtAuthenticationStateProvider>();
                return new AuthenticationService(client, authState);
            });
        }
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