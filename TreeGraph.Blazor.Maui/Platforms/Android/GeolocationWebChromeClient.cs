using Android.Content;
using Android.Webkit;
using AndroidX.Activity;
using AndroidX.Activity.Result;
using AndroidX.Activity.Result.Contract;

namespace TreeGraph.Blazor.Maui;

/// <summary>
/// BlazorWebView 自定义 WebChromeClient（Android）。
///
/// MAUI 内置 BlazorWebChromeClient 只处理文件选择，不处理地理定位授权，
/// navigator.geolocation 在 WebView 中会被默认拒绝。替换时两个能力都必须保留：
/// 1) 定位授权：App 已获定位权限（MainActivity 启动时申请）→ 放行 JS 定位；
/// 2) 文件选择：InputFile（FileField 上传）依赖，替代内置实现。
/// </summary>
internal sealed class GeolocationWebChromeClient : WebChromeClient
{
    private readonly ComponentActivity? _activity;
    private readonly ActivityResultLauncher? _fileChooserLauncher;
    private Action<Android.Net.Uri[]?>? _pendingFileCallback;

    public GeolocationWebChromeClient(ComponentActivity? activity)
    {
        _activity = activity;
        if (activity is not null)
        {
            _fileChooserLauncher = activity.RegisterForActivityResult(
                new ActivityResultContracts.StartActivityForResult(),
                new FileChooserResultCallback(this));
        }
    }

    /// <summary>JS 定位授权询问：App 持定位权限则放行，否则拒绝（JS 侧走 error 分支）。</summary>
    public override void OnGeolocationPermissionsShowPrompt(
        string? origin, GeolocationPermissions.ICallback? callback)
    {
        if (callback is null || _activity is null)
        {
            callback?.Invoke(origin, false, false);
            return;
        }

        var granted =
            _activity.CheckSelfPermission(Android.Manifest.Permission.AccessFineLocation!)
                == Android.Content.PM.Permission.Granted
            || _activity.CheckSelfPermission(Android.Manifest.Permission.AccessCoarseLocation!)
                == Android.Content.PM.Permission.Granted;
        callback.Invoke(origin, granted, false);
    }

    public override bool OnShowFileChooser(
        Android.Webkit.WebView? webView,
        IValueCallback? filePathCallback,
        FileChooserParams? fileChooserParams)
    {
        if (_fileChooserLauncher is null || filePathCallback is null || fileChooserParams is null)
        {
            return false;
        }

        _pendingFileCallback = uris => filePathCallback.OnReceiveValue(uris);
        _fileChooserLauncher.Launch(fileChooserParams.CreateIntent());
        return true;
    }

    private void OnFileChooserResult(ActivityResult result)
    {
        var callback = _pendingFileCallback;
        _pendingFileCallback = null;
        callback?.Invoke(FileChooserParams.ParseResult(
            (int)result.ResultCode, result.Data));
    }

    private sealed class FileChooserResultCallback
        : Java.Lang.Object, IActivityResultCallback
    {
        private readonly GeolocationWebChromeClient _owner;

        public FileChooserResultCallback(GeolocationWebChromeClient owner) => _owner = owner;

        public void OnActivityResult(Java.Lang.Object? result)
        {
            if (result is ActivityResult activityResult)
            {
                _owner.OnFileChooserResult(activityResult);
            }
            else
            {
                _owner.OnFileChooserResult(new ActivityResult(0, null));
            }
        }
    }
}
