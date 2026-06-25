using APromisedLand.Maui.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using System;
using System.Collections.Generic;
using System.Text;
using MudBlazor.Services;
using MudBlazor;

namespace APromisedLand.Maui.Helper;

public static class MauiHelper
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

    public static void MudBlazorServices(MauiAppBuilder builder)
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

}
