using Android.Webkit;
using Microsoft.AspNetCore.Components.WebView.Maui;

namespace TreeGraph.Blazor.Maui;

/// <summary>
/// 自定义 BlazorWebView Android 处理器：替换 WebChromeClient 为 GeolocationWebChromeClient，
/// 使 navigator.geolocation 在 WebView 中可用（保留文件选择能力）。
/// </summary>
public class CustomBlazorWebViewHandler : BlazorWebViewHandler
{
    protected override void ConnectHandler(Android.Webkit.WebView platformView)
    {
        base.ConnectHandler(platformView);

        // WebChromeClient 必须在平台视图初始化后替换
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity
                       as AndroidX.Activity.ComponentActivity;
        platformView.SetWebChromeClient(new GeolocationWebChromeClient(activity));
    }
}
