using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Activity;
using AView = Android.Views.View;
using AViewGroup = Android.Views.ViewGroup;
using AWebView = Android.Webkit.WebView;

namespace TreeGraph.Blazor.Maui;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    WindowSoftInputMode = Android.Views.SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Activity 特性上的 WindowSoftInputMode 会被 MAUI 宿主运行时覆盖为 adjustPan
        // （dumpsys 实测 sim={adjust=pan}），软键盘弹出时 WebView 视口不收缩，
        // 位于屏幕下半部的输入框会被键盘遮挡。base.OnCreate 前后各设一次（MAUI 在
        // base.OnCreate 中会重写该模式），强制走 AdjustResize。
        Window!.SetSoftInputMode(Android.Views.SoftInput.AdjustResize);

        base.OnCreate(savedInstanceState);

        // 退出 .NET MAUI 默认 edge-to-edge：实测（Pixel 7 / API 33）BlazorWebView 会铺满
        // 整个窗口，而 WebView 内 CSS env(safe-area-inset-*) 取不到值（insets 被宿主根
        // 布局消费），导致全屏对话框（手机端 PageDialogSky 默认形态）标题栏被不透明
        // 状态栏（@color/colorPrimaryDark）物理遮挡、标题按钮无法点按。
        // 退回经典 fitsSystemWindows：状态栏保留不透明占位，WebView 从状态栏下方开始
        // 布局，AdjustResize 也继续按经典路径工作。
        if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
        {
            Window!.SetDecorFitsSystemWindows(true);
        }

        Window!.SetSoftInputMode(Android.Views.SoftInput.AdjustResize);

        // 物理返回键：当前 MAUI 版本默认直接 moveTaskToBack，不会自动走 BlazorWebView 的
        // 浏览器历史。经 OnBackPressedDispatcher 挂一个回调（API33+ 非过时路径），
        // 先问 JS 栈（wwwroot/js/platform-back.js）：
        //   有打开的对话框 → JS 关闭栈顶，拦截本次返回；
        //   无对话框       → 禁用本回调后转发给调度器，执行系统默认（后退/退到桌面）。
        OnBackPressedDispatcher.AddCallback(this, new DialogBackCallback(this));
    }

    /// <summary>在视图树中递归查找 BlazorWebView 对应的原生 WebView。</summary>
    internal AWebView? FindWebView()
    {
        var root = FindViewById<AView>(Android.Resource.Id.Content);
        return root is null ? null : FindWebViewRecursive(root);
    }

    private static AWebView? FindWebViewRecursive(AView view)
    {
        if (view is AWebView webView)
        {
            return webView;
        }

        if (view is AViewGroup group)
        {
            for (var i = 0; i < group.ChildCount; i++)
            {
                var found = FindWebViewRecursive(group.GetChildAt(i)!);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private sealed class DialogBackCallback : OnBackPressedCallback
    {
        private readonly MainActivity _activity;

        public DialogBackCallback(MainActivity activity) : base(enabled: true)
        {
            _activity = activity;
        }

        public override void HandleOnBackPressed()
        {
            var webView = _activity.FindWebView();
            if (webView is null)
            {
                InvokeDefaultBack();
                return;
            }

            webView.EvaluateJavascript(
                "(window.__treegraph_handleBack && window.__treegraph_handleBack()) || false",
                new JsValueCallback(this));
        }

        private void InvokeDefaultBack()
        {
            // 暂时禁用自身再转发，调度器才会继续走到系统默认（退到桌面等）
            Enabled = false;
            try
            {
                _activity.OnBackPressedDispatcher.OnBackPressed();
            }
            finally
            {
                Enabled = true;
            }
        }

        private sealed class JsValueCallback : Java.Lang.Object, Android.Webkit.IValueCallback
        {
            private readonly DialogBackCallback _owner;

            public JsValueCallback(DialogBackCallback owner) => _owner = owner;

            public void OnReceiveValue(Java.Lang.Object? value)
            {
                // EvaluateJavascript 对 boolean 的返回是 JSON 文本 "true"/"false"，回调在 UI 线程
                var handled = string.Equals(value?.ToString(), "true", StringComparison.OrdinalIgnoreCase);
                if (!handled)
                {
                    _owner.InvokeDefaultBack();
                }
            }
        }
    }
}
