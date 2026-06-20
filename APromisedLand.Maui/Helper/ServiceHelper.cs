using APromisedLand.Maui.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using System;
using System.Collections.Generic;
using System.Text;

namespace APromisedLand.Maui.Helper;

public static class ServiceHelper
{
    public static void AuthenticationServices(MauiAppBuilder builder)
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
