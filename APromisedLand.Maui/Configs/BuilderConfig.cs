using APromisedLand.Maui.Authentication;
using APromisedLand.Maui.DiberyTree;
using APromisedLand.MauiBlazor.DiberyTree.Interfaces;
using APromisedLand.MauiBlazor.DiberyTree.Services;
using APromisedLand.Razor.DiberyTree.Navigation;
using APromisedLand.Razor.DiberyTree.Services;
using APromisedLand.Razor.Helper.Blazor;
using APromisedLand.Razor.Services;
using APromisedLand.Shared.DiberyTree.Models;
using APromisedLand.Shared.Interfaces;
using APromisedLand.Shared.Services.Solution;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Http.Resilience;
using MudBlazor;
using MudBlazor.Extensions;
using MudBlazor.Services;

namespace APromisedLand.Maui.Configs;

public static class BuilderConfig
{
    extension(MauiAppBuilder builder)
    {
        public void AddBuilderConfig()
        {
            builder.Services.AddMudServices();
            builder.AddMudBlazorServices();

            builder.Services.AddScoped<BlazorService>();
            builder.Services.AddScoped<SolutionService>();

            builder.AddPlatformInfo();

            builder.Services.AddScoped<MessageService>();

            builder.AddAuthenticationServices();
            builder.AddKeycloakClient();

            // builder.AddDiberyTreeClient<CategoryTree>();
            // builder.AddDiberyTreeClient<UnitTree>();

            builder.TreeClient<CategoryTree>();
            builder.TreeClient<UnitTree>();
            builder.AttributeClient();

            builder.Services.AddScoped<TreeNodeDialogService<CategoryTree>>();
            builder.Services.AddScoped<TreeNodeDialogService<UnitTree>>();

            builder.Services.AddSingleton<ITreeNavigationHistoryService, TreeNavigationHistoryService>();

            builder.Services.AddSingleton<ITreeClientService<CategoryTree>, CategoryTreeClientService>();
            builder.Services.AddSingleton<ITreeClientService<UnitTree>, UnitTreeClientService>();
        }

        private void AttributeClient()
        {
            //http://localhost:5085
            // 注意：AddServiceDefaults() 会给所有 HttpClient 挂 AddStandardResilienceHandler()，
            // 其默认重试策略不区分方法——会对 POST/PATCH 在 5xx/超时 时自动重试，
            // 从而把一次 AddValue/AddRow 请求放大成多次服务端调用并产生重复数据。
            // 此处移除这两个值类客户端的韧性重试：
            //   - 401 刷新重试由 JwtAuthorizationMessageHandler 处理（仅 GET/PUT/DELETE）；
            //   - 重复提交由服务端 SHA256 指纹幂等去重兜底；
            //   - 客户端 _isSubmitting 锁与 ButtonLoadingSky 入口守卫拦截重入。
#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers 为实验性 API，此处需用其移除全局标准韧性重试
            builder.Services.AddHttpClient<AttributeApiClient>(client =>
                {
                    client.BaseAddress = new Uri("http://localhost:5085");
                })
                .AddHttpMessageHandler<JwtAuthorizationMessageHandler>()
                .RemoveAllResilienceHandlers();

            builder.Services.AddHttpClient<TableValueApiClient>(client =>
                {
                    client.BaseAddress = new Uri("http://localhost:5085");
                })
                .AddHttpMessageHandler<JwtAuthorizationMessageHandler>()
                .RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

            // builder.Services.AddHttpClient<AttributeLocationValueApiClient>(client =>
            //     {
            //         client.BaseAddress = new Uri("http://localhost:5085");
            //     })
            //     .AddHttpMessageHandler<JwtAuthorizationMessageHandler>();
        }

        private void TreeClient<T>()
        {
            // builder.Services.AddHttpClient<UnitsOfMeasureApiClient>(client =>
            //     {
            //         client.BaseAddress = new Uri("http://localhost:5085");
            //     })
            //     .AddHttpMessageHandler<JwtAuthorizationMessageHandler>();    

            //http://localhost:5085
            builder.Services.AddHttpClient<DiberyTreeApiClient<T>>(client =>
            {
                client.BaseAddress = new Uri("http://localhost:5085");
            });
            // .AddHttpMessageHandler<JwtAuthorizationMessageHandler>();    
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

            builder.Services.AddMudServicesWithExtensions();
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