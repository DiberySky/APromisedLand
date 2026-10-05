# MAUI Blazor 适配 + PageDialogSky 重构实施计划（Phase 0-3，修订版）

> 本版已完整吸收《计划评估报告》的 13 项修订意见。每项修订在对应章节标注 `[评估#N]` 以便对照。

## Context

TreeGraph.Blazor.Shared 作为 RCL，当前仅服务于 Blazor Server 宿主。为支持未来 MAUI Blazor Hybrid 接入，需提前完成：
1. **平台抽象层**：`IPlatformContext` 接口 + **JS 视口检测**（Web 端）+ `Changed` 事件，让组件能真实感知设备形态
2. **PageDialogSky 移动端适配**：全屏/动作区布局/自动检测，桌面端默认行为完全不变
3. **NodeEav 响应式改造**：DynamicForm 栅格化 + 页面级移动端布局

**范围限定**（用户已确认）：
- 暂不创建 MAUI 项目，仅做 RCL + Blazor Server 端改造
- FileField 保持 URL 输入，不实现文件选择功能
- 完成 Phase 0-3 后，MAUI 集成作为后续独立任务

**断点约定** `[评估#13]`：全计划统一使用 MudBlazor 默认断点，避免双标准：

| 形态 | 视口宽度 | MudBlazor 断点 | FormFactor |
|---|---|---|---|
| Phone | `< 600px` | `xs` | Phone |
| Tablet | `600–960px` | `sm` | Tablet |
| Desktop | `≥ 960px` | `md` 及以上 | Desktop（Web 宿主下 FormFactor 仍为 Web，但 IsCompact/IsMobile 按视口计算） |

## 现状分析

### PageDialogSky 当前参数（11 个参数族）
- 标题栏：Title, TitleDense, CustomTitle
- 导航按钮：ArrowBackVisible, ArrowBackClosesDialog, StartButtonVisible, StartButtonIcon, StartButtonText
- 宽度：DialogMaxWidth, ActionsMaxWidth
- 按钮：CancelButtonVisible, SubmitButtonVisible, IconsVisible, CancelButtonText, SubmitButtonText, SubmitButtonVariant, SubmitButtonColor
- 行为：SubmitButtonClosesDialog
- 样式：TitleClass, ContentClass, ActionsClass, ActionsContentClass
- 插槽：ToolBarContent, DialogContent, DialogLeftActions, DialogRightActions, DialogActions
- 回调：OnStartClick, OnCanceledClick, OnSubmitClick

### 调用方（14 处，改造后零改动享受移动端适配）
- 壳：TreeDialogPageSky, TreeSelectDialogSky, StringTreeDialogPageSky, DialogTreeSky
- TreeSky 节点对话框：TreeNodeActionsDialog, TreeNodeViewDialog, TreeNodeEditDialog, TreeNodeSortDialog, TreeNodeParentSelectDialog
- StringTree 对话框：StringNodeActionsDialog, StringNodeViewDialog, StringNodeEditDialog, StringNodeSortDialog, StringParentSelectDialog

### NodeEav 现状
- 无 `DialogService.ShowAsync<PageDialogSky>` 调用（仅 `ShowMessageBoxAsync`）→ 本计划不含对话框调用迁移
- DynamicForm 使用 `div.mb-4` 纵向布局（无栅格）
- EntityList 使用 MudTable；EntityEdit 为整页编辑
- FileField 为 URL 输入

### 测试现状
- bUnit 1.40.0 + xUnit + Moq；`JSInterop.Mode = Loose`
- **注意** `[评估#11]`：Loose 模式下 JS 调用返回默认值 0/null，涉及视口检测的测试必须注入 `FakePlatformContext`，不能依赖 JS Setup

---

## Phase 0：平台抽象层 + 视口检测基础设施

> 合并评估报告建议的 Phase 0 与 Phase 0.5：`IPlatformContext` 一次性建成"功能完整"形态（含 Changed 事件与 RefreshAsync），避免 Phase 1 返工。`[评估#1][评估#八-Phase0.5]`

### 0.1 新增 `TreeGraph.Blazor.Shared/Platform/IPlatformContext.cs`

**接口全部显式抽象，不使用默认实现** `[评估#8]`：

```csharp
namespace TreeGraph.Blazor.Shared.Platform;

/// <summary>平台形态因子。Web 宿主下按视口宽度映射（见断点约定）。</summary>
public enum PlatformFormFactor { Unknown, Phone, Tablet, Desktop, Web }

/// <summary>平台上下文抽象：让 RCL 组件感知宿主平台与设备形态。</summary>
public interface IPlatformContext
{
    /// <summary>当前设备形态。</summary>
    PlatformFormFactor FormFactor { get; }

    /// <summary>是否为手机形态（视口 &lt; 600px，或 MAUI Phone Idiom）。</summary>
    bool IsMobile { get; }

    /// <summary>是否为紧凑布局（手机或平板：视口 &lt; 960px）。</summary>
    bool IsCompact { get; }

    /// <summary>是否以触控为主要输入方式。</summary>
    bool IsTouchPrimary { get; }

    /// <summary>宿主类型标识："Server" | "Hybrid" | "WebAssembly"。</summary>
    string HostKind { get; }

    /// <summary>视口/形态发生变化时触发（如 window.resize 越过断点）。</summary>
    event Action? Changed;

    /// <summary>刷新平台信息。Web 实现通过 JS interop 读取 window.innerWidth；
    /// 首次渲染后由组件或宿主调用一次。</summary>
    Task RefreshAsync();
}
```

### 0.2 新增 `TreeGraph.Blazor.Shared/Platform/WebPlatformContext.cs`

**核心修订** `[评估#1]`：推翻"Server 端 IsMobile 恒 false"的假设，通过 JS interop 获取视口宽度。**不注入 NavigationManager** `[评估#9]`。

```csharp
using Microsoft.JSInterop;

namespace TreeGraph.Blazor.Shared.Platform;

/// <summary>
/// Blazor Server 平台上下文：HostKind=Server，设备形态通过 JS 读取 window.innerWidth 判定。
/// 初始（SSR/尚未 RefreshAsync）按桌面处理，避免首屏误触发布局。
/// </summary>
public class WebPlatformContext : IPlatformContext
{
    private readonly IJSRuntime _js;
    private PlatformFormFactor _formFactor = PlatformFormFactor.Web;

    public WebPlatformContext(IJSRuntime js) => _js = js;

    public PlatformFormFactor FormFactor => _formFactor;

    // Web 宿主下形态由视口宽度决定；SSR 首帧（_formFactor=Web）视为桌面
    public bool IsMobile => _formFactor == PlatformFormFactor.Phone;
    public bool IsCompact => _formFactor is PlatformFormFactor.Phone or PlatformFormFactor.Tablet;
    public bool IsTouchPrimary => IsCompact;
    public string HostKind => "Server";

    public event Action? Changed;

    /// <summary>读取 window.innerWidth 并按 MudBlazor 断点（600/960）映射形态。</summary>
    public async Task RefreshAsync()
    {
        int width;
        try
        {
            width = await _js.InvokeAsync<int>("eval", "window.innerWidth");
        }
        catch (InvalidOperationException)
        {
            // SSR 阶段 JS 不可用，保持当前形态
            return;
        }

        var next = width < 600 ? PlatformFormFactor.Phone
                 : width < 960 ? PlatformFormFactor.Tablet
                 : PlatformFormFactor.Desktop;

        if (next != _formFactor)
        {
            _formFactor = next;
            Changed?.Invoke();
        }
    }
}
```

### 0.3 新增 `TreeGraph.Blazor.Shared/Platform/FakePlatformContext.cs`（测试与 SSR 兜底）`[评估#11]`

```csharp
namespace TreeGraph.Blazor.Shared.Platform;

/// <summary>测试用平台上下文：属性可直接赋值，RaiseChanged 手动触发事件。</summary>
public class FakePlatformContext : IPlatformContext
{
    public PlatformFormFactor FormFactor { get; set; } = PlatformFormFactor.Desktop;
    public bool IsMobile { get; set; }
    public bool IsCompact { get; set; }
    public bool IsTouchPrimary { get; set; }
    public string HostKind { get; set; } = "Test";
    public event Action? Changed;
    public int RefreshCallCount { get; private set; }

    public Task RefreshAsync() { RefreshCallCount++; return Task.CompletedTask; }
    public void RaiseChanged() => Changed?.Invoke();
}
```

### 0.4 新增 `TreeGraph.Blazor.Shared/Platform/PlatformServiceCollectionExtensions.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace TreeGraph.Blazor.Shared.Platform;

public static class PlatformServiceCollectionExtensions
{
    /// <summary>注册默认 Web 平台上下文（Scoped）。Hybrid 项目可用 AddSingleton&lt;IPlatformContext, MauiPlatformContext&gt;() 覆盖。</summary>
    public static IServiceCollection AddTreeGraphPlatform(this IServiceCollection services)
    {
        services.TryAddScoped<IPlatformContext, WebPlatformContext>();
        return services;
    }
}
```

### 0.5 视口 resize 监听（宿主侧）

**修改 `TreeGraph.Blazor/Components/App.razor`**（或 MainLayout）：注入 `IPlatformContext`，在 `OnAfterRenderAsync(firstRender)` 中：
1. 调用 `Platform.RefreshAsync()` 完成首次检测；
2. 通过 JS 注册 `window.resize` 回调（`DotNetObjectReference`），回调内调用 `RefreshAsync()`（内部去抖 150ms，且仅在越过断点时触发 `Changed`）。

新增静态 JS 文件 `TreeGraph.Blazor/wwwroot/platform-resize.js`：
```js
window.treeGraphPlatform = {
  registerResize: function (dotNetRef) {
    let timer = null;
    window.addEventListener('resize', function () {
      clearTimeout(timer);
      timer = setTimeout(function () { dotNetRef.invokeMethodAsync('OnWindowResize'); }, 150);
    });
  }
};
```

### 0.6 Phase 0 测试

新增 `TreeGraph.Blazor.Shared.Tests/Platform/WebPlatformContextTests.cs`：
- `AddTreeGraphPlatform()` 后 `IPlatformContext` 可解析且为 `WebPlatformContext`
- TryAdd 语义：先注册 `FakePlatformContext` 再调 `AddTreeGraphPlatform()`，解析结果仍为 Fake
- `RefreshAsync` 在 JS 不可用时（SSR 模拟）不抛异常、不改变形态
- JS 返回 375 → `IsMobile=true`；返回 800 → `IsCompact=true 且 IsMobile=false`；返回 1280 → 均为 false
- 形态跨越断点时 `Changed` 触发一次；同形态重复刷新不触发

**Phase 0 出口**：`dotnet build` + 上述测试全绿。不触碰任何现有 UI。

---

## Phase 1：PageDialogSky 移动端能力扩展

**修改文件**：`TreeGraph.Blazor.Shared/Common/PageDialogSky.razor`（实现 `IDisposable` 以退订事件）

### 1.1 新增枚举 `TreeGraph.Blazor.Shared/Common/DialogActionsLayout.cs`

```csharp
namespace TreeGraph.Blazor.Shared.Common;

/// <summary>对话框操作区布局方式。</summary>
public enum DialogActionsLayout
{
    /// <summary>自动：移动端 Column，桌面端 Row。</summary>
    Auto,
    /// <summary>水平排列（桌面默认）。</summary>
    Row,
    /// <summary>垂直堆叠（移动端：主按钮在上、全部满宽）。</summary>
    Column
}
```

### 1.2 新增参数（全部可空/带默认值，向后兼容）

```csharp
// ───────── 移动端适配（新增） ─────────
/// <summary>全屏显示。null=自动（移动端全屏）；显式 true/false 强制覆盖。[评估#3]</summary>
[Parameter] public bool? FullScreen { get; set; }

/// <summary>撑满宽度。null=自动（移动端满宽）；显式 true/false 强制覆盖。
/// 生效时 DialogMaxWidth 被忽略（内部按 MaxWidth.False 处理）。[评估#3][评估#7]</summary>
[Parameter] public bool? FullWidth { get; set; }

/// <summary>操作区布局：Auto（移动端 Column / 桌面 Row）。默认 Auto。</summary>
[Parameter] public DialogActionsLayout ActionsLayout { get; set; } = DialogActionsLayout.Auto;

/// <summary>是否按 IPlatformContext 自动应用移动端默认值。默认 true。</summary>
[Parameter] public bool AutoDetectMobile { get; set; } = true;

/// <summary>移动端在标题栏右侧显示关闭图标（独立于 IconsVisible；
/// IconsVisible 仅控制操作区按钮图标）。[评估#6]</summary>
[Parameter] public bool ShowCloseIconInTitleOnMobile { get; set; } = true;

/// <summary>移动端追加到 ContentClass 的额外样式（追加语义，不替换主题类）。[评估#4]</summary>
[Parameter] public string MobileContentClass { get; set; } = "pa-3";

/// <summary>移动端追加到 ActionsClass 的额外样式（追加语义）。</summary>
[Parameter] public string MobileActionsClass { get; set; } = "px-3 pb-3";
```

### 1.3 `@code` 块修订 `[评估#五-关键代码]`

```csharp
[Inject] private IPlatformContext Platform { get; set; } = default!;

private bool _isMobile;
private bool _effectiveFullScreen;
private bool _effectiveFullWidth;
private DialogActionsLayout _effectiveActionsLayout;

private string EffectiveContentClass =>
    _isMobile ? $"{ContentClass} {MobileContentClass}".Trim() : ContentClass;   // [评估#4] 追加语义

private string EffectiveActionsClass =>
    _isMobile ? $"{ActionsClass} {MobileActionsClass}".Trim() : ActionsClass;

protected override void OnInitialized()
{
    Platform.Changed += OnPlatformChanged;
}

protected override void OnParametersSet()
{
    _isMobile = AutoDetectMobile && Platform.IsMobile;
    _effectiveFullScreen = FullScreen ?? _isMobile;    // [评估#3] 三态
    _effectiveFullWidth = FullWidth ?? _isMobile;
    _effectiveActionsLayout = ActionsLayout == DialogActionsLayout.Auto
        ? (_isMobile ? DialogActionsLayout.Column : DialogActionsLayout.Row)
        : ActionsLayout;
}

protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
        await Platform.RefreshAsync();   // SSR 后完成首次视口检测，Changed 触发重渲染
}

private void OnPlatformChanged() => InvokeAsync(StateHasChanged);

public void Dispose() => Platform.Changed -= OnPlatformChanged;
```

### 1.4 模板修订

**MudDialog 根元素** `[评估#7]`：
```razor
<MudDialog MaxWidth="@(_effectiveFullWidth ? MaxWidth.False : DialogMaxWidth)"
           FullScreen="@_effectiveFullScreen"
           FullWidth="@_effectiveFullWidth"
           TitleClass="@TitleClass"
           ContentClass="@EffectiveContentClass"
           ActionsClass="@EffectiveActionsClass">
```

**标题栏**：现有结构不变；在 `CustomTitle == null && Title 非空` 分支的 `MudToolBar` 末尾追加：
```razor
@if (_isMobile && ShowCloseIconInTitleOnMobile)
{
    <MudIconButton Icon="@Icons.Material.Filled.Close"
                   Color="Color.Default"
                   OnClick="@CancelAsync" />
}
```

**DialogActions 布局分支** `[评估#2][评估#5]`：
```razor
<DialogActions>
    @if (_effectiveActionsLayout == DialogActionsLayout.Column)
    {
        @* 移动端：竖排满宽；自定义 DialogActions 原样渲染（调用方自控布局） *@
        <MudStack Spacing="2" Class="pa-4">
            @if (DialogActions != null)
            {
                @DialogActions
            }
            else
            {
                @if (DialogLeftActions != null) { @DialogLeftActions }
                @if (SubmitButtonVisible)
                {
                    <MudButton Variant="@SubmitButtonVariant" Color="@SubmitButtonColor"
                               FullWidth="true" Size="Size.Large"
                               StartIcon="@(IconsVisible ? Icons.Material.Filled.SaveAs : null)"
                               OnClick="@SubmitAsync">@SubmitButtonText</MudButton>
                }
                @if (CancelButtonVisible)
                {
                    <MudButton Variant="Variant.Text" Color="Color.Default"
                               FullWidth="true" Size="Size.Large"
                               StartIcon="@(IconsVisible ? Icons.Material.Filled.Close : null)"
                               OnClick="@CancelAsync">@CancelButtonText</MudButton>
                }
                @if (DialogRightActions != null) { @DialogRightActions }
            }
        </MudStack>
    }
    else
    {
        @* 桌面端：现有 Row 布局原样保留 *@
        <MudContainer MaxWidth="@ActionsMaxWidth">
            <MudStack Row Class="@ActionsContentClass" Spacing="2" AlignItems="AlignItems.Center">
                @if (DialogActions != null) { @DialogActions }
                else
                {
                    @if (DialogLeftActions != null) { @DialogLeftActions }
                    <MudSpacer/>
                    @if (DialogRightActions != null) { @DialogRightActions }
                    @if (CancelButtonVisible) { ...现有取消按钮... }
                    @if (SubmitButtonVisible) { ...现有提交按钮... }
                }
            </MudStack>
        </MudContainer>
    }
</DialogActions>
```

### 1.5 Phase 1 测试（新增 `PageDialogSkyTests.cs`）

**全部通过 `FakePlatformContext` 注入，不依赖 JS** `[评估#11]`：

| # | 场景 | 断言 |
|---|---|---|
| 1 | 桌面 Fake（IsMobile=false）+ 全默认参数 | 无 FullScreen/FullWidth；Row 布局；与改造前快照一致 |
| 2 | 手机 Fake（IsMobile=true）+ 全默认参数 | FullScreen=true、FullWidth=true、Column 布局 |
| 3 | 手机 Fake + `FullScreen="false"` | 不全屏（显式覆盖生效）`[评估#3]` |
| 4 | 桌面 Fake + `FullScreen="true"` | 全屏（用户强制） |
| 5 | 手机 Fake + 提供 `DialogActions` | 自定义内容原样渲染，不渲染默认按钮 `[评估#2]` |
| 6 | 手机 Fake + 提供 `DialogLeftActions`/`DialogRightActions` | 两者均渲染在 Column 流中 `[评估#2]` |
| 7 | 手机 Fake + `ContentClass="mud-theme-dark"` | 最终类含 `mud-theme-dark` 和 `pa-3`（追加）`[评估#4]` |
| 8 | 手机 Fake + `IconsVisible=true` | Column 分支提交/取消按钮含 StartIcon `[评估#5]` |
| 9 | 手机 Fake + `ShowCloseIconInTitleOnMobile=false` | 标题栏无关闭图标 |
| 10 | 手机 Fake + `CustomTitle` 提供 | 不渲染默认 MudToolBar，也不渲染移动端关闭图标 |
| 11 | 手机 Fake + `AutoDetectMobile=false` | 不进入移动端分支 |
| 12 | 首渲染后调用 `Platform.RefreshAsync()` 一次 | Fake 的 RefreshCallCount == 1 |
| 13 | `Fake.RaiseChanged()` 后 | 组件重渲染且布局随新形态切换 |

**既有回归**：`TreeSkyComponentTests`、`StringTreeSkyComponentTests`、`StringParentSelectDialogTests` 等全部保持绿色；这些测试的 DI 容器需补 `Services.AddSingleton<IPlatformContext>(new FakePlatformContext())`（或调用 `AddTreeGraphPlatform()`，bUnit 下 JSInterop Loose 使 RefreshAsync 静默返回，形态保持 Web/桌面）。

**风险**：
- SSR 首帧按桌面渲染、JS 检测后切移动端 → 闪烁一帧。接受此行为（MudDialog 打开通常在交互之后，此时已完成 RefreshAsync）；在组件 XML 注释中说明。

---

## Phase 2：NodeEav 移动端表单/页面适配

### 2.1 DynamicForm 栅格化

**修改** `TreeGraph.Blazor.Shared/NodeEav/Components/DynamicForm.razor`：

- 外层 `div.mb-4` 改为：
```razor
<MudGrid>
    @foreach (var attr in SortedAttributes)
    {
        <MudItem xs="12" sm="6" md="4">
            @switch (attr.DataType) { ...字段渲染器调用不变... }
        </MudItem>
    }
</MudGrid>
```
- 断点说明 `[评估#13]`：`xs=12`（<600 单列）/ `sm=6`（600-960 双列）/ `md=4`（≥960 三列）。**取消原计划的 `lg=3`**（EAV 表单字段 Label 较长，四列过窄；保持三列上限）。
- 新增 `[Parameter] public bool CompactLayout { get; set; }`：为 true 时所有 `MudItem` 固定 `xs="12"`（供窄面板/对话框内复用）。
- 底部保存按钮包进 `<MudStack Row Justify="Justify.FlexEnd" Class="mt-4">`。

### 2.2 字段渲染器微调

- **`NumberField.razor` / `UnitNumberField.razor`**：注入 `IPlatformContext`，仅当 `Platform.IsMobile` 时给 `MudNumericField` 加 `InputMode="InputMode.numeric"`（唤起数字键盘）。**不设置 `Immediate`** `[评估#10]`——避免移动端每键触发 ValueChanged 导致中间态被误判；保持默认失焦提交语义。
- **`DateField.razor` / `DateTimeField.razor` / `TimeField.razor`**：`Platform.IsMobile` 时 `PickerVariant="PickerVariant.Dialog"`（移动端弹层选择器，避免内联日历被压缩）；桌面保持默认。
- **`FileField.razor`**：保持 URL 输入；`Platform.IsMobile` 时在输入框下方追加 `<MudText Typo="Typo.caption" Color="Color.Secondary">请输入可访问的文件 URL</MudText>`。

### 2.3 页面级适配（EntityEdit / EntityList）

**`EntityEdit.razor`**：
- 注入 `IPlatformContext`。
- 底部操作区（保存/重新加载/清空/删除）：`Platform.IsCompact` 时改为 `MudMenu`（MoreVert 图标）收纳次要操作，仅保留"保存"主按钮；桌面端按钮组不变。
- 属性区 `MudPaper` 在 `IsCompact` 时 `Class="pa-2 mb-3"`（收窄内边距）。

**`EntityList.razor`**：
- 注入 `IPlatformContext`。
- `Platform.IsCompact` 时 MudTable 替换为卡片列表：
```razor
<MudStack Spacing="3">
    @foreach (var item in _result.Items)
    {
        <MudCard Elevation="1" Outlined="true">
            <MudCardContent Class="pa-3">
                <MudText Typo="Typo.caption" Color="Color.Secondary"><code>@item.EntityId</code></MudText>
                @foreach (var col in _previewColumns)
                {
                    <MudText Typo="Typo.body2">@GetColumnDisplayName(col)：@FormatPreview(item.Properties.GetValueOrDefault(col))</MudText>
                }
            </MudCardContent>
            <MudCardActions Class="pa-2">
                <MudIconButton Icon="@Icons.Material.Filled.Edit" Size="Size.Small" Color="Color.Primary" OnClick="@(() => OpenEdit(item.EntityId))" />
                <MudIconButton Icon="@Icons.Material.Filled.Delete" Size="Size.Small" Color="Color.Error" OnClick="@(() => ConfirmDelete(item.EntityId))" />
            </MudCardActions>
        </MudCard>
    }
</MudStack>
```
- 分页控件在卡片模式下保留（页大小 Select + 前后翻页）。
- **InodeList/InodeDetail/InodeEntityEdit 本阶段不动**，待 EntityEdit/EntityList 验证通过后按同模式推广（列入后续任务，不在本计划）。

### 2.4 Phase 2 测试

新增 `DynamicFormResponsiveTests.cs`：
- 默认渲染：存在 `mud-grid`，`mud-item` 带 `xs="12" sm="6" md="4"`
- `CompactLayout=true`：`mud-item` 仅 `xs="12"`
- 保存按钮容器为 `mud-stack` 且 `Justify.FlexEnd`

新增 `EntityEditResponsiveTests.cs` / `EntityListResponsiveTests.cs`（FakePlatformContext）：
- 桌面：EntityEdit 渲染按钮组；EntityList 渲染 `mud-table`
- 紧凑（IsCompact=true）：EntityEdit 渲染 `mud-menu`；EntityList 渲染 `mud-card`，无 `mud-table`

**既有测试同步**：检查现有测试中是否有依赖 `DynamicForm` 旧 `div.mb-4` 结构的选择器，逐一更新为 `mud-grid`/`mud-item`。

---

## Phase 3：Blazor Server 端集成验证

### 3.1 宿主接线

- `TreeGraph.Blazor/Program.cs`：在 `AddMudServices()` 后加 `builder.Services.AddTreeGraphPlatform();`
- `App.razor`（或 MainLayout）：完成 0.5 节的首次 `RefreshAsync()` + resize 监听注册
- 确认 `_Host.cshtml`/`App.razor` 含 `<meta name="viewport" content="width=device-width, initial-scale=1" />`

### 3.2 自动化验证

- `dotnet build` 全解决方案 0 错 0 警
- `dotnet test TreeGraph.Blazor.Shared.Tests` 全绿（含新增 4 个测试类）
- `dotnet test TreeGraph.Blazor.Tests` 全绿（无回归）
- `dotnet test TreeGraph.Blazor.E2E.Tests` 全绿（26/26，无回归）

### 3.3 手工验证清单（Chrome DevTools 设备栏）`[评估#四-修订]`

| # | 验证项 | 预期 | 关联评估项 |
|---|---|---|---|
| 1 | iPhone 视口（375px）打开 TreeDialogPageSky | 全屏显示 | #1 |
| 2 | 同上，操作区按钮 | 竖排、满宽、主按钮在上 | |
| 3 | 自定义 DialogActions 的对话框（移动端） | 自定义内容原样渲染 | #2 |
| 4 | `FullScreen="false"` 显式关闭（移动端） | 不全屏 | #3 |
| 5 | 移动端 `ContentClass` | 主题类不丢失 | #4 |
| 6 | 移动端提交/取消按钮 | 含图标（IconsVisible=true 时） | #5 |
| 7 | 视口 375 → 1024 动态拖宽 | 布局 Column → Row 实时切换（Changed 事件） | #1 |
| 8 | 屏幕阅读器（NVDA/旁白）打开对话框 | 焦点进入对话框，Esc/关闭后焦点返回 | #12 |
| 9 | iPad 视口（768px） | IsCompact=true、IsMobile=false：EntityList 卡片化，但对话框保持非全屏 | #13 |
| 10 | EntityEdit 手机视口 | 单列、次要操作收进 MudMenu | |
| 11 | NumberField 手机视口聚焦 | 唤起数字键盘 | |
| 12 | DateField 手机视口点击 | 弹层选择器（Dialog 变体） | |

### 3.4 A11y 说明 `[评估#12]`

MudDialog 自带 `role="dialog"`、`aria-modal="true"` 与焦点管理；本计划不额外实现焦点陷阱，仅在验证清单 #8 确认全屏模式下 MudBlazor 默认行为可用。若验证发现全屏下焦点逃逸，再单独立项处理。

---

## 风险表（修订版）`[评估#六]`

| 风险 | 触发点 | 应对 | 状态 |
|---|---|---|---|
| Web 端 IsMobile 恒 false 导致 Phase 3 无法验证 | Phase 0 | JS 视口检测 + Changed 事件 | ✅ 已修订（0.2/0.5） |
| Column 分支忽略自定义操作区 | Phase 1 | Column 分支完整处理 DialogActions/Left/Right | ✅ 已修订（1.4） |
| FullScreen OR 语义无法关闭 | Phase 1 | 改 `bool?`（null=自动） | ✅ 已修订（1.2） |
| MobileContentClass 替换语义丢主题 | Phase 1 | 追加语义 | ✅ 已修订（1.3） |
| Column 分支丢 StartIcon | Phase 1 | 补齐 StartIcon | ✅ 已修订（1.4） |
| 接口默认实现与覆盖冲突 | Phase 0 | 全部显式抽象 | ✅ 已修订（0.1） |
| 断点阈值双标准 | 全局 | 统一 MudBlazor 600/960 | ✅ 已修订（断点约定） |
| bUnit Loose 模式 JS 返回默认值 | Phase 1/2 测试 | 注入 FakePlatformContext | ✅ 已修订（0.3） |
| NumberField Immediate 误触发验证 | Phase 2 | 不设置 Immediate | ✅ 已修订（2.2） |
| 移动端 A11y | Phase 3 | 验证清单 #8，依赖 MudDialog 默认行为 | ✅ 已修订（3.4） |
| 导航管理器未使用 | Phase 0 | 移除注入 | ✅ 已修订（0.2） |
| PageDialogSky 重构破坏 14 个调用方 | Phase 1 | 新参数全部带默认值；测试 #1 快照对比 | 保留 |
| Shared 项目被 MAUI 依赖污染 | Phase 0 | 禁止 Shared 引用 MAUI 包 | 保留 |
| SSR 首帧桌面→移动端闪烁 | Phase 1 | 接受（对话框打开前已完成检测）；注释说明 | 保留 |
| NodeEav 改动范围蔓延 | Phase 2 | 仅 EntityEdit/EntityList + DynamicForm；Inode* 后续 | 保留 |
| DynamicForm 栅格化破坏既有测试 | Phase 2 | 同步更新测试选择器 | 保留 |

---

## 文件清单

**新增（7 个）**：
- `TreeGraph.Blazor.Shared/Platform/IPlatformContext.cs`
- `TreeGraph.Blazor.Shared/Platform/WebPlatformContext.cs`
- `TreeGraph.Blazor.Shared/Platform/FakePlatformContext.cs`
- `TreeGraph.Blazor.Shared/Platform/PlatformServiceCollectionExtensions.cs`
- `TreeGraph.Blazor.Shared/Common/DialogActionsLayout.cs`
- `TreeGraph.Blazor/wwwroot/platform-resize.js`

**新增测试（4 个）**：
- `TreeGraph.Blazor.Shared.Tests/Platform/WebPlatformContextTests.cs`
- `TreeGraph.Blazor.Shared.Tests/Components/PageDialogSkyTests.cs`
- `TreeGraph.Blazor.Shared.Tests/Components/DynamicFormResponsiveTests.cs`
- `TreeGraph.Blazor.Shared.Tests/Components/EntityListResponsiveTests.cs`（EntityEdit 响应式断言并入此类）

**修改（10 个）**：
- `TreeGraph.Blazor.Shared/Common/PageDialogSky.razor`（7 新参数 + 事件订阅 + Column 分支）
- `TreeGraph.Blazor.Shared/NodeEav/Components/DynamicForm.razor`（MudGrid + CompactLayout）
- `TreeGraph.Blazor.Shared/NodeEav/Components/FieldRenderers/NumberField.razor`（InputMode）
- `TreeGraph.Blazor.Shared/NodeEav/Components/FieldRenderers/UnitNumberField.razor`（InputMode）
- `TreeGraph.Blazor.Shared/NodeEav/Components/FieldRenderers/DateField.razor`（PickerVariant）
- `TreeGraph.Blazor.Shared/NodeEav/Components/FieldRenderers/DateTimeField.razor`（PickerVariant）
- `TreeGraph.Blazor.Shared/NodeEav/Components/FieldRenderers/TimeField.razor`（PickerVariant）
- `TreeGraph.Blazor.Shared/NodeEav/Components/FieldRenderers/FileField.razor`（移动端提示）
- `TreeGraph.Blazor.Shared/NodeEav/Pages/Entities/EntityEdit.razor`（紧凑模式 MudMenu）
- `TreeGraph.Blazor.Shared/NodeEav/Pages/Entities/EntityList.razor`（紧凑模式卡片）
- `TreeGraph.Blazor/Program.cs`（注册 AddTreeGraphPlatform）
- `TreeGraph.Blazor/Components/App.razor`（首次 RefreshAsync + resize 监听）
- 既有测试文件：注入 FakePlatformContext；同步 DynamicForm 选择器

---

## 验证出口

1. `dotnet build` 全解决方案 0 错 0 警
2. `dotnet test TreeGraph.Blazor.Shared.Tests` 全绿（含 4 个新增测试类）
3. `dotnet test TreeGraph.Blazor.Tests` / `TreeGraph.Blazor.E2E.Tests` 无回归
4. 手工清单 3.3 共 12 项全部通过
