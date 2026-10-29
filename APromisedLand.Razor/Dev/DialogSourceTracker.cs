#if DEBUG
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Extensions.Core;
using MudBlazor.Extensions.Options;
using MudBlazor.Extensions.Services;
using MudBlazor.Interop;

namespace APromisedLand.Razor.Dev;

/// <summary>
/// DEBUG 专用：IDialogService 装饰器，拦截 ShowAsync 记录「弹窗引用 → 内容组件类型」，
/// 供 <see cref="DevDialogSourceButton"/> 反查当前弹窗对应的 .razor 源文件。
/// 仅 Dev 环境注册（DiberyMauiSky.MauiProgram），Release 不编译。
/// 同时实现 <see cref="IMudExDialogService"/> 并委托 inner：MudBlazor.Extensions 的
/// ShowExAsync 以「dialogService is IMudExDialogService」为运行时判定来设置 JSRuntime
/// 并经 JS 注入最大化/最小化按钮，纯 IDialogService 包装会导致该能力静默失效
/// （Blazor WebView / Server 下 WASM 静态回退取不到 JSRuntime）。
/// </summary>
public sealed class DialogSourceTracker : IMudExDialogService
{
    private readonly IDialogService _inner;
    private readonly IMudExDialogService? _innerEx;

    /// <summary>按引用 Id 索引的内容组件类型（Id 匹配为主策略）。</summary>
    private static readonly ConcurrentDictionary<Guid, Type> ById = new();

    /// <summary>打开中的弹窗栈（Id 匹配失败时的 LIFO 回退；弹窗通常单开，栈顶即当前）。</summary>
    private static readonly List<(Guid Id, Type Type)> OpenStack = new();
    private static readonly object StackLock = new();

    public DialogSourceTracker(IDialogService inner)
    {
        _inner = inner;
        _innerEx = inner as IMudExDialogService;
        IsActive = true;
        _inner.OnDialogCloseRequested += OnInnerCloseRequested;
        _inner.DialogInstanceAddedAsync += OnInnerDialogInstanceAddedAsync;
    }

    // ───────── IMudExDialogService 委托（inner 为 MudExDialogService 时透传） ─────────
    // 关键：ShowExAsync 据此设置 options.JsRuntime 并经 JS 注入最大化/最小化按钮。

    public IJSRuntime JSRuntime => InnerEx.JSRuntime;

    public IServiceProvider ServiceProvider => InnerEx.ServiceProvider;

    public MudExAppearanceService AppearanceService => InnerEx.AppearanceService;

    public IDialogReference GetDialogReference(Guid dialogGuid)
        => InnerEx.GetDialogReference(dialogGuid);

    public DialogOptionsEx GetDialogUsedDialogOptions(Guid dialogGuid)
        => InnerEx.GetDialogUsedDialogOptions(dialogGuid);

    // ───────── JS 回调透传（MudBlazor.Extensions 9.11.6+） ─────────
    // JS 模块以 DotNetObjectReference 包装「当前 IDialogService 实例」回调拖拽/非模态关闭事件；
    // DEBUG 下该实例是本装饰器，必须暴露同名 JSInvokable 并转发 inner，否则报
    // "does not contain a public invokable method with [JSInvokableAttribute(\"PublishEvent\")]"。

    [JSInvokable]
    public Task<object> PublishEvent(string eventName, string dialogId, DotNetObjectReference<ComponentBase> dialog, BoundingClientRect rect)
        => ((MudExDialogService)InnerEx).PublishEvent(eventName, dialogId, dialog, rect);

    [JSInvokable]
    public Task HandleNonModalClose(string dialogId, DotNetObjectReference<ComponentBase> dialog)
        => ((MudExDialogService)InnerEx).HandleNonModalClose(dialogId, dialog);

    /// <summary>inner 必须是 MudExDialogService；否则 ShowExAsync 链路本身不可用，快速失败优于静默返回 null。</summary>
    private IMudExDialogService InnerEx => _innerEx
        ?? throw new InvalidOperationException("内部 IDialogService 不是 IMudExDialogService，ShowExAsync 不可用。");

    /// <summary>装饰器是否已进入 DI 链（构造即置位）。供按钮区分「未注册」与「未命中」。</summary>
    public static bool IsActive { get; private set; }

    // ───────── 静态查询（供 DevDialogSourceButton） ─────────

    /// <summary>
    /// 反查弹窗内容组件类型。instance 为 null（标题栏区域拿不到级联值）或
    /// Id 未命中时回退到「最近打开」栈顶——弹窗通常单开，栈顶即当前弹窗。
    /// </summary>
    public static Type? Resolve(IMudDialogInstance? instance)
    {
        if (instance is not null && ById.TryGetValue(instance.Id, out var exact))
        {
            return exact;
        }

        lock (StackLock)
        {
            if (OpenStack.Count > 0)
            {
                return OpenStack[^1].Type;
            }
        }

        // 次级来源：最近渲染弹窗的内容组件实例（渲染后由 MudBlazor 回填）
        return LastReference?.Dialog?.GetType();
    }

    // ───────── 记录 ─────────

    private void Track(IDialogReference reference, Type componentType)
    {
        ById[reference.Id] = componentType;
        lock (StackLock)
        {
            OpenStack.Add((reference.Id, componentType));
        }
    }

    private async Task<IDialogReference> Track(Task<IDialogReference> task, Type componentType)
    {
        var reference = await task;
        Track(reference, componentType);
        return reference;
    }

    private void OnInnerCloseRequested(IDialogReference reference, DialogResult? result)
    {
        ById.TryRemove(reference.Id, out _);
        lock (StackLock)
        {
            OpenStack.RemoveAll(e => e.Id == reference.Id);
        }

        DialogCloseRequested?.Invoke(reference, result);
    }

    private Task OnInnerDialogInstanceAddedAsync(IDialogReference reference)
    {
        // 次级来源：即使 ShowAsync 未被拦截（绕过装饰器的极端情况），
        // 弹窗渲染时 reference.Dialog 会回填内容组件实例，点击时可直接取类型。
        lock (StackLock)
        {
            LastReference = reference;
        }

        return DialogInstanceAdded?.Invoke(reference) ?? Task.CompletedTask;
    }

    /// <summary>最近一个已渲染弹窗的引用（Dialog 属性在渲染后回填内容组件实例）。</summary>
    public static IDialogReference? LastReference { get; private set; }

    // ───────── 事件转发 ─────────

    private event Func<IDialogReference, Task>? DialogInstanceAdded;
    private event Action<IDialogReference, DialogResult?>? DialogCloseRequested;

    public event Func<IDialogReference, Task>? DialogInstanceAddedAsync
    {
        add => DialogInstanceAdded += value;
        remove => DialogInstanceAdded -= value;
    }

    public event Action<IDialogReference, DialogResult?>? OnDialogCloseRequested
    {
        add => DialogCloseRequested += value;
        remove => DialogCloseRequested -= value;
    }

    // ───────── 拦截泛型 ShowAsync ─────────

    public Task<IDialogReference> ShowAsync<TComponent>() where TComponent : IComponent
        => Track(_inner.ShowAsync<TComponent>(), typeof(TComponent));

    public Task<IDialogReference> ShowAsync<TComponent>(string title) where TComponent : IComponent
        => Track(_inner.ShowAsync<TComponent>(title), typeof(TComponent));

    public Task<IDialogReference> ShowAsync<TComponent>(string title, DialogOptions options) where TComponent : IComponent
        => Track(_inner.ShowAsync<TComponent>(title, options), typeof(TComponent));

    public Task<IDialogReference> ShowAsync<TComponent>(DialogOptions options) where TComponent : IComponent
        => Track(_inner.ShowAsync<TComponent>(options), typeof(TComponent));

    public Task<IDialogReference> ShowAsync<TComponent>(DialogParameters parameters) where TComponent : IComponent
        => Track(_inner.ShowAsync<TComponent>(parameters), typeof(TComponent));

    public Task<IDialogReference> ShowAsync<TComponent>(string title, DialogParameters parameters) where TComponent : IComponent
        => Track(_inner.ShowAsync<TComponent>(title, parameters), typeof(TComponent));

    public Task<IDialogReference> ShowAsync<TComponent>(string title, DialogParameters parameters, DialogOptions options) where TComponent : IComponent
        => Track(_inner.ShowAsync<TComponent>(title, parameters, options), typeof(TComponent));

    public Task<IDialogReference> ShowAsync<TComponent>(DialogParameters parameters, DialogOptions options) where TComponent : IComponent
        => Track(_inner.ShowAsync<TComponent>(parameters, options), typeof(TComponent));

    // ───────── 拦截 Type 版 ShowAsync ─────────

    public Task<IDialogReference> ShowAsync(Type componentType)
        => Track(_inner.ShowAsync(componentType), componentType);

    public Task<IDialogReference> ShowAsync(Type componentType, string title)
        => Track(_inner.ShowAsync(componentType, title), componentType);

    public Task<IDialogReference> ShowAsync(Type componentType, string title, DialogOptions options)
        => Track(_inner.ShowAsync(componentType, title, options), componentType);

    public Task<IDialogReference> ShowAsync(Type componentType, string title, DialogParameters parameters)
        => Track(_inner.ShowAsync(componentType, title, parameters), componentType);

    public Task<IDialogReference> ShowAsync(Type componentType, string title, DialogParameters parameters, DialogOptions options)
        => Track(_inner.ShowAsync(componentType, title, parameters, options), componentType);

    // ───────── 其余成员原样委托 ─────────

    public IDialogReference CreateReference() => _inner.CreateReference();

    public void Close(IDialogReference dialog) => _inner.Close(dialog);

    public void Close(IDialogReference dialog, DialogResult result) => _inner.Close(dialog, result);

    public Task<bool?> ShowMessageBoxAsync(string title, string message, string yesText = "OK",
        string? cancelText = null, string? noText = null, DialogOptions? options = null)
        => _inner.ShowMessageBoxAsync(title, message, yesText, cancelText, noText, options);

    public Task<bool?> ShowMessageBoxAsync(string title, MarkupString markupMessage, string yesText = "OK",
        string? cancelText = null, string? noText = null, DialogOptions? options = null)
        => _inner.ShowMessageBoxAsync(title, markupMessage, yesText, cancelText, noText, options);

    public Task<bool?> ShowMessageBoxAsync(MessageBoxOptions messageBoxOptions, DialogOptions? options = null)
        => _inner.ShowMessageBoxAsync(messageBoxOptions, options);
}

/// <summary>注册扩展：用 <see cref="DialogSourceTracker"/> 装饰 MudBlazor 的 IDialogService。</summary>
public static class DialogSourceTrackerServiceCollectionExtensions
{
    /// <summary>须在 AddMudServices() 之后调用（后注册覆盖前者）。
    /// 兼容工厂/实例/类型三种注册形态；一律移除原注册再包装饰器。</summary>
    public static IServiceCollection AddDialogSourceTracker(this IServiceCollection services)
    {
        var existing = services.Where(d => d.ServiceType == typeof(IDialogService)).ToList();
        foreach (var d in existing)
        {
            services.Remove(d);
        }

        // 原有注册形态 → 还原 inner 实例的方式
        Func<IServiceProvider, IDialogService> innerFactory = existing switch
        {
            { Count: 0 } => sp => ActivatorUtilities.CreateInstance<DialogService>(sp),
            _ => sp =>
            {
                var last = existing[^1];
                if (last.ImplementationInstance is IDialogService inst)
                {
                    return inst;
                }

                if (last.ImplementationFactory is not null)
                {
                    return (IDialogService)last.ImplementationFactory(sp);
                }

                return (IDialogService)ActivatorUtilities.CreateInstance(sp,
                    last.ImplementationType ?? typeof(DialogService));
            },
        };

        services.Add(new ServiceDescriptor(
            typeof(IDialogService),
            sp => new DialogSourceTracker(innerFactory(sp)),
            existing.Count > 0 ? existing[^1].Lifetime : ServiceLifetime.Scoped));
        return services;
    }
}
#endif
