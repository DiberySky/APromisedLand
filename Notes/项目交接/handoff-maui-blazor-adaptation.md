# MAUI Blazor 适配 + PageDialogSky 重构 — 交接文档

> 生成时间：2026-10-05
> 状态：Plan Mode 已完成，计划已修订并锁定，待执行
> 计划文件：[maui-blazor-adaptation-plan.md](file:///d:/APromisedLand/.trae/documents/maui-blazor-adaptation-plan.md)

---

## 一、项目现状（换账号后必读）

### 1.1 代码库位置
```
d:\APromisedLand\
├── TreeGraph.Blazor.Shared\          ← RCL，改造主战场
│   ├── Common\PageDialogSky.razor     ← 统一对话框外壳（168行，11参数族）
│   ├── NodeEav\                       ← EAV 系统（页面+组件+字段渲染器）
│   ├── Trees\TreeSky\                 ← TreeSky 组件体系
│   ├── Trees\StringTree\              ← StringTree 组件体系
│   └── Platform\                      ← 【本次新建】平台抽象层
├── TreeGraph.Blazor.Shared.Tests\     ← bUnit 1.40.0 + xUnit + Moq
├── TreeGraph.Blazor\                  ← Blazor Server 宿主
│   ├── Program.cs
│   ├── Components\App.razor
│   └── wwwroot\                      ← 【本次新建】platform-resize.js
├── TreeGraph.Blazor.E2E.Tests\        ← Playwright E2E（26项全绿基线）
├── APromisedLand.Maui\                ← 纯 MAUI 应用（**本次不动**）
├── APromisedLand.MauiBlazor\          ← MAUI 共享类库（**本次不动**）
└── APromisedLand.sln                  ← 解决方案
```

### 1.2 关键现状
- **APromisedLand.Maui 是纯 MAUI 应用**（无 BlazorWebView），不是 Blazor Hybrid。本次改造**不碰它**。
- **PageDialogSky 已合并** DialogSky + DialogPageSky，当前有 14 个调用方（TreeSky/StringTree 对话框体系）。
- **NodeEav 没有使用 PageDialogSky**（仅 `ShowMessageBoxAsync`），本次改造是"让页面和表单自身在移动端可用"。
- **FileField 是 URL 输入**（非文件上传），本次不做文件选择功能。
- **DialogConfig 已有 `FullScreen`/`FullWidth`**，`BlazorService.DialogOptions` 使用 `DialogOptionsEx`（MudBlazor.Extensions）。
- **MudBlazor 版本**：9.11.0（TreeGraph.Blazor.Shared 和 APromisedLand.MauiBlazor 均一致）。

### 1.3 已有平台相关代码
- `APromisedLand.Maui/Configs/PlatformInfo.cs`：使用 `DeviceInfo.Current` 判断平台（纯 MAUI，本次不动）。

---

## 二、计划总览

**范围**：Phase 0-3（暂不创建 MAUI 项目，仅 RCL + Blazor Server 端改造）

| Phase | 内容 | 影响文件数 | 预计工作量 |
|---|---|---|---|
| **Phase 0** | 平台抽象层 + JS 视口检测 + Fake 测试替身 | 5 新文件 | 0.5 天 |
| **Phase 1** | PageDialogSky 新增 7 个移动端参数 + Column 布局分支 | 1 改 + 1 新枚举 + 1 新测试 | 1 天 |
| **Phase 2** | DynamicForm 栅格化 + EntityEdit/EntityList 响应式 + 字段渲染器微调 | 10 改 + 3 新测试 | 1-2 天 |
| **Phase 3** | Blazor Server 集成 + 全量回归测试 + 手工验证 | 3 改（宿主接线） | 0.5 天 |

**断点约定**（全计划统一使用 MudBlazor 默认断点）：

| 形态 | 视口宽度 | MudBlazor 断点 |
|---|---|---|
| Phone | `< 600px` | `xs` |
| Tablet | `600–960px` | `sm` |
| Desktop | `≥ 960px` | `md` 及以上 |

---

## 三、Phase 0：平台抽象层（详细代码）

### 3.1 新建 `TreeGraph.Blazor.Shared/Platform/IPlatformContext.cs`

```csharp
namespace TreeGraph.Blazor.Shared.Platform;

public enum PlatformFormFactor { Unknown, Phone, Tablet, Desktop, Web }

/// <summary>平台上下文抽象。全部显式抽象，不使用默认实现。</summary>
public interface IPlatformContext
{
    PlatformFormFactor FormFactor { get; }
    bool IsMobile { get; }        // Phone (<600px) 或 MAUI Phone Idiom
    bool IsCompact { get; }       // Phone || Tablet (<960px)
    bool IsTouchPrimary { get; }
    string HostKind { get; }      // "Server" | "Hybrid" | "WebAssembly"
    event Action? Changed;
    Task RefreshAsync();
}
```

### 3.2 新建 `TreeGraph.Blazor.Shared/Platform/WebPlatformContext.cs`

```csharp
using Microsoft.JSInterop;

namespace TreeGraph.Blazor.Shared.Platform;

public class WebPlatformContext : IPlatformContext
{
    private readonly IJSRuntime _js;
    private PlatformFormFactor _formFactor = PlatformFormFactor.Web;

    public WebPlatformContext(IJSRuntime js) => _js = js;

    public PlatformFormFactor FormFactor => _formFactor;
    public bool IsMobile => _formFactor == PlatformFormFactor.Phone;
    public bool IsCompact => _formFactor is PlatformFormFactor.Phone or PlatformFormFactor.Tablet;
    public bool IsTouchPrimary => IsCompact;
    public string HostKind => "Server";
    public event Action? Changed;

    public async Task RefreshAsync()
    {
        int width;
        try { width = await _js.InvokeAsync<int>("eval", "window.innerWidth"); }
        catch (InvalidOperationException) { return; } // SSR 阶段 JS 不可用

        var next = width < 600 ? PlatformFormFactor.Phone
                 : width < 960 ? PlatformFormFactor.Tablet
                 : PlatformFormFactor.Desktop;

        if (next != _formFactor) { _formFactor = next; Changed?.Invoke(); }
    }
}
```

### 3.3 新建 `TreeGraph.Blazor.Shared/Platform/FakePlatformContext.cs`（测试用）

```csharp
namespace TreeGraph.Blazor.Shared.Platform;

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

### 3.4 新建 `TreeGraph.Blazor.Shared/Platform/PlatformServiceCollectionExtensions.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace TreeGraph.Blazor.Shared.Platform;

public static class PlatformServiceCollectionExtensions
{
    public static IServiceCollection AddTreeGraphPlatform(this IServiceCollection services)
    {
        services.TryAddScoped<IPlatformContext, WebPlatformContext>();
        return services;
    }
}
```

### 3.5 新建 JS 文件 `TreeGraph.Blazor/wwwroot/platform-resize.js`

```javascript
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

### 3.6 宿主接线

**`TreeGraph.Blazor/Program.cs`**：在 `AddMudServices()` 后加：
```csharp
builder.Services.AddTreeGraphPlatform();
```

**`TreeGraph.Blazor/Components/App.razor`**（或 MainLayout）：
```csharp
@inject IPlatformContext Platform
@inject IJSRuntime JS
@implements IDisposable

@code {
    private DotNetObjectReference<App>? _dotNetRef;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await Platform.RefreshAsync();
            _dotNetRef = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("treeGraphPlatform.registerResize", _dotNetRef);
        }
    }

    [JSInvokable] public async Task OnWindowResize() => await Platform.RefreshAsync();

    public void Dispose() => _dotNetRef?.Dispose();
}
```

### 3.7 Phase 0 测试（新建 `WebPlatformContextTests.cs`）

| # | 场景 | 断言 |
|---|---|---|
| 1 | AddTreeGraphPlatform 后 IPlatformContext 可解析 | 类型为 WebPlatformContext |
| 2 | TryAdd 语义 | 先注册 Fake 再调 Add，解析结果仍为 Fake |
| 3 | RefreshAsync JS 不可用时 | 不抛异常、形态不变 |
| 4 | JS 返回 375 | IsMobile=true |
| 5 | JS 返回 800 | IsCompact=true, IsMobile=false |
| 6 | JS 返回 1280 | 均为 false |
| 7 | 跨越断点时 | Changed 触发一次 |
| 8 | 同形态重复刷新 | Changed 不触发 |

**出口**：`dotnet build` + 测试全绿。

---

## 四、Phase 1：PageDialogSky 移动端扩展（详细代码）

### 4.1 新增枚举 `TreeGraph.Blazor.Shared/Common/DialogActionsLayout.cs`

```csharp
namespace TreeGraph.Blazor.Shared.Common;

public enum DialogActionsLayout { Auto, Row, Column }
```

### 4.2 PageDialogSky 新增参数

```csharp
[Inject] private IPlatformContext Platform { get; set; } = default!;

// 三态：null=自动（移动端true/桌面false），显式值强制覆盖
[Parameter] public bool? FullScreen { get; set; }
[Parameter] public bool? FullWidth { get; set; }
[Parameter] public DialogActionsLayout ActionsLayout { get; set; } = DialogActionsLayout.Auto;
[Parameter] public bool AutoDetectMobile { get; set; } = true;
[Parameter] public bool ShowCloseIconInTitleOnMobile { get; set; } = true;
[Parameter] public string MobileContentClass { get; set; } = "pa-3";
[Parameter] public string MobileActionsClass { get; set; } = "px-3 pb-3";
```

### 4.3 `@code` 块核心逻辑

```csharp
private bool _isMobile;
private bool _effectiveFullScreen;
private bool _effectiveFullWidth;
private DialogActionsLayout _effectiveActionsLayout;

private string EffectiveContentClass =>
    _isMobile ? $"{ContentClass} {MobileContentClass}".Trim() : ContentClass;
private string EffectiveActionsClass =>
    _isMobile ? $"{ActionsClass} {MobileActionsClass}".Trim() : ActionsClass;

protected override void OnInitialized() { Platform.Changed += OnPlatformChanged; }

protected override void OnParametersSet()
{
    _isMobile = AutoDetectMobile && Platform.IsMobile;
    _effectiveFullScreen = FullScreen ?? _isMobile;
    _effectiveFullWidth = FullWidth ?? _isMobile;
    _effectiveActionsLayout = ActionsLayout == DialogActionsLayout.Auto
        ? (_isMobile ? DialogActionsLayout.Column : DialogActionsLayout.Row)
        : ActionsLayout;
}

protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender) await Platform.RefreshAsync();
}

private void OnPlatformChanged() => InvokeAsync(StateHasChanged);
public void Dispose() => Platform.Changed -= OnPlatformChanged;
```

### 4.4 模板关键变更

**MudDialog 根元素**：
```razor
<MudDialog MaxWidth="@(_effectiveFullWidth ? MaxWidth.False : DialogMaxWidth)"
           FullScreen="@_effectiveFullScreen"
           FullWidth="@_effectiveFullWidth"
           TitleClass="@TitleClass"
           ContentClass="@EffectiveContentClass"
           ActionsClass="@EffectiveActionsClass">
```

**标题栏**：在 `CustomTitle == null && Title 非空` 分支的 MudToolBar 末尾追加：
```razor
@if (_isMobile && ShowCloseIconInTitleOnMobile)
{
    <MudIconButton Icon="@Icons.Material.Filled.Close"
                   Color="Color.Default"
                   OnClick="@CancelAsync" />
}
```

**DialogActions Column 分支**（必须处理 DialogActions/Left/Right）：
```razor
@if (_effectiveActionsLayout == DialogActionsLayout.Column)
{
    <MudStack Spacing="2" Class="pa-4">
        @if (DialogActions != null) { @DialogActions }
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
// Row 分支保持原样
```

### 4.5 Phase 1 测试（13 项，全部通过 FakePlatformContext）

| # | 场景 | 断言 |
|---|---|---|
| 1 | 桌面 + 全默认 | 无 FullScreen/FullWidth；Row；与改造前快照一致 |
| 2 | 手机 + 全默认 | FullScreen=true、FullWidth=true、Column |
| 3 | 手机 + `FullScreen="false"` | 不全屏（显式覆盖） |
| 4 | 桌面 + `FullScreen="true"` | 全屏（用户强制） |
| 5 | 手机 + 提供 `DialogActions` | 自定义内容原样渲染 |
| 6 | 手机 + 提供 `DialogLeftActions`/`DialogRightActions` | 两者均渲染 |
| 7 | 手机 + `ContentClass="mud-theme-dark"` | 最终类含主题+pa-3（追加语义） |
| 8 | 手机 + `IconsVisible=true` | Column 按钮含 StartIcon |
| 9 | 手机 + `ShowCloseIconInTitleOnMobile=false` | 无关闭图标 |
| 10 | 手机 + `CustomTitle` 提供 | 不渲染默认 toolbar 和关闭图标 |
| 11 | 手机 + `AutoDetectMobile=false` | 不进入移动端分支 |
| 12 | 首渲染后 RefreshAsync 一次 | Fake.RefreshCallCount == 1 |
| 13 | Fake.RaiseChanged() 后 | 组件重渲染且布局切换 |

### 4.6 既有回归测试同步

所有现有测试的 DI 容器需补 `IPlatformContext`：
```csharp
// 在测试构造函数中
Services.AddSingleton<IPlatformContext>(new FakePlatformContext());
// 或
Services.AddTreeGraphPlatform(); // bUnit 下 JSInterop Loose，RefreshAsync 静默返回
```

受影响测试类：
- `TreeSkyComponentTests`
- `StringTreeSkyComponentTests`
- `StringParentSelectDialogTests`
- `TreeNodeDialogServiceTests`
- `StringTreeDialogServiceTests`
- `MessageServiceTests`

---

## 五、Phase 2：NodeEav 响应式改造（详细代码）

### 5.1 DynamicForm 栅格化

**修改** `TreeGraph.Blazor.Shared/NodeEav/Components/DynamicForm.razor`：

- 把外层 `div.mb-4` 改为：
```razor
<MudGrid>
    @foreach (var attr in SortedAttributes)
    {
        <MudItem xs="12" sm="6" md="4">
            @switch (attr.DataType) { ...字段渲染器不变... }
        </MudItem>
    }
</MudGrid>
```
- 新增参数：`[Parameter] public bool CompactLayout { get; set; }` → true 时 `MudItem xs="12"`
- 保存按钮：`MudStack Row Justify="Justify.FlexEnd" Class="mt-4"`

### 5.2 字段渲染器微调

**`NumberField.razor`**：
```razor
@inject IPlatformContext Platform
<MudNumericField T="decimal?" Label="@Attr.DisplayName"
                 Value="Value" ValueChanged="ValueChanged"
                 Required="@Attr.IsRequired" Variant="Variant.Outlined"
                 InputMode="@(Platform.IsMobile ? InputMode.numeric : InputMode.text)" />
```

**`DateField.razor`**：
```razor
@inject IPlatformContext Platform
<MudDatePicker Label="@Attr.DisplayName"
               Date="Value" DateChanged="ValueChanged"
               Required="@Attr.IsRequired" Variant="Variant.Outlined"
               PickerVariant="@(Platform.IsMobile ? PickerVariant.Dialog : PickerVariant.Inline)" />
```

**`DateTimeField.razor` / `TimeField.razor`**：同 DateField 模式。

**`FileField.razor`**：
```razor
@inject IPlatformContext Platform
@* ...现有 URL 输入框... *@
@if (Platform.IsMobile)
{
    <MudText Typo="Typo.caption" Color="Color.Secondary" Class="mt-1">
        请输入可访问的文件 URL
    </MudText>
}
```

### 5.3 EntityEdit 响应式

**修改** `TreeGraph.Blazor.Shared/NodeEav/Pages/Entities/EntityEdit.razor`：

- 注入 `IPlatformContext`
- 底部操作区：`IsCompact` 时收进 MudMenu
```razor
@if (Platform.IsCompact)
{
    <MudMenu Icon="@Icons.Material.Filled.MoreVert" Color="Color.Default">
        <MudMenuItem OnClick="SaveAsync">保存</MudMenuItem>
        <MudMenuItem OnClick="ReloadAsync">重新加载</MudMenuItem>
        <MudMenuItem OnClick="ClearAllAsync">清空所有属性</MudMenuItem>
        <MudMenuItem OnClick="DeleteEntityAsync">删除实体</MudMenuItem>
    </MudMenu>
}
else
{
    @* 现有按钮组 *@
}
```

### 5.4 EntityList 响应式

**修改** `TreeGraph.Blazor.Shared/NodeEav/Pages/Entities/EntityList.razor`：

- 注入 `IPlatformContext`
- `IsCompact` 时 MudTable 替换为卡片列表：
```razor
@if (Platform.IsCompact)
{
    <MudStack Spacing="3">
        @foreach (var item in _result.Items)
        {
            <MudCard Elevation="1" Outlined="true">
                <MudCardContent Class="pa-3">
                    <MudText Typo="Typo.caption" Color="Color.Secondary"><code>@item.EntityId</code></MudText>
                    @foreach (var col in _previewColumns)
                    {
                        <MudText Typo="Typo.body2">@GetColumnDisplayName(col)：@FormatPreview(...)</MudText>
                    }
                </MudCardContent>
                <MudCardActions Class="pa-2">
                    <MudIconButton Icon="@Icons.Material.Filled.Edit" Size="Size.Small" Color="Color.Primary" OnClick="..." />
                    <MudIconButton Icon="@Icons.Material.Filled.Delete" Size="Size.Small" Color="Color.Error" OnClick="..." />
                </MudCardActions>
            </MudCard>
        }
    </MudStack>
}
else
{
    @* 现有 MudTable *@
}
```

### 5.5 Phase 2 测试

- `DynamicFormResponsiveTests.cs`：断言 MudGrid/MudItem 断点属性
- `EntityListResponsiveTests.cs`：桌面→MudTable；紧凑→MudCard；无 MudTable

**注意**：检查现有测试中是否有依赖 `DynamicForm` 旧 `div.mb-4` 结构的选择器，逐一更新。

---

## 六、Phase 3：集成验证

### 6.1 自动化验证

```bash
dotnet build                    # 全解决方案 0 错 0 警
dotnet test TreeGraph.Blazor.Shared.Tests    # 全绿（含新增）
dotnet test TreeGraph.Blazor.Tests           # 无回归
dotnet test TreeGraph.Blazor.E2E.Tests       # 26/26 全绿
```

### 6.2 手工验证清单（Chrome DevTools 设备栏）

| # | 验证项 | 预期 |
|---|---|---|
| 1 | iPhone 视口打开 TreeDialogPageSky | 全屏显示 |
| 2 | 按钮竖排满宽，主按钮在上 | ✅ |
| 3 | 自定义 DialogActions 对话框（移动端） | 自定义内容原样渲染 |
| 4 | `FullScreen="false"`（移动端） | 不全屏 |
| 5 | `ContentClass` 移动端 | 主题类不丢失（追加语义） |
| 6 | 按钮图标（IconsVisible=true） | 移动端也显示 |
| 7 | 视口 375→1024 动态拖宽 | Column→Row 实时切换 |
| 8 | iPad 视口（768px） | IsCompact=true, IsMobile=false；EntityList 卡片化，对话框非全屏 |
| 9 | EntityEdit 手机视口 | 单列，次要操作收进 MudMenu |
| 10 | NumberField 手机视口 | 唤起数字键盘 |
| 11 | DateField 手机视口 | 弹层选择器 |
| 12 | 屏幕阅读器打开对话框 | 焦点进入，关闭后返回（MudDialog 默认行为） |

---

## 七、风险清单（已修订）

| 风险 | 状态 | 应对 |
|---|---|---|
| Web 端 IsMobile 恒 false → Phase 3 验证失败 | ✅ 已修复 | JS 视口检测 + Changed 事件 |
| Column 分支忽略自定义 DialogActions | ✅ 已修复 | Column 分支完整处理 DialogActions/Left/Right |
| FullScreen OR 语义无法关闭 | ✅ 已修复 | 改 `bool?`（null=自动） |
| MobileContentClass 替换丢主题 | ✅ 已修复 | 追加语义 `$"{ContentClass} {MobileContentClass}"` |
| Column 分支丢 StartIcon | ✅ 已修复 | 补齐 |
| 接口默认实现与覆盖冲突 | ✅ 已修复 | 全部显式抽象 |
| 断点阈值双标准 | ✅ 已修复 | 统一 MudBlazor 600/960 |
| bUnit Loose 模式 JS 返回默认值 | ✅ 已修复 | 注入 FakePlatformContext |
| NumberField Immediate 误触发验证 | ✅ 已修复 | 不设置 Immediate |
| SSR 首帧桌面→移动端闪烁 | ⚠️ 接受 | 对话框打开前已完成检测；注释说明 |
| 既有 14 调用方回归 | ⚠️ 可控 | 新参数全部带默认值；测试快照对比 |
| DynamicForm 栅格化破坏既有测试 | ⚠️ 可控 | 同步更新选择器 |

---

## 八、文件总清单

**新增（8 个）**：
1. `TreeGraph.Blazor.Shared/Platform/IPlatformContext.cs`
2. `TreeGraph.Blazor.Shared/Platform/WebPlatformContext.cs`
3. `TreeGraph.Blazor.Shared/Platform/FakePlatformContext.cs`
4. `TreeGraph.Blazor.Shared/Platform/PlatformServiceCollectionExtensions.cs`
5. `TreeGraph.Blazor.Shared/Common/DialogActionsLayout.cs`
6. `TreeGraph.Blazor/wwwroot/platform-resize.js`
7. `TreeGraph.Blazor.Shared.Tests/Platform/WebPlatformContextTests.cs`
8. `TreeGraph.Blazor.Shared.Tests/Components/PageDialogSkyTests.cs`
9. `TreeGraph.Blazor.Shared.Tests/Components/DynamicFormResponsiveTests.cs`
10. `TreeGraph.Blazor.Shared.Tests/Components/EntityListResponsiveTests.cs`

**修改（14 个）**：
1. `TreeGraph.Blazor.Shared/Common/PageDialogSky.razor` ← 核心
2. `TreeGraph.Blazor.Shared/NodeEav/Components/DynamicForm.razor`
3. `TreeGraph.Blazor.Shared/NodeEav/Components/FieldRenderers/NumberField.razor`
4. `TreeGraph.Blazor.Shared/NodeEav/Components/FieldRenderers/UnitNumberField.razor`
5. `TreeGraph.Blazor.Shared/NodeEav/Components/FieldRenderers/DateField.razor`
6. `TreeGraph.Blazor.Shared/NodeEav/Components/FieldRenderers/DateTimeField.razor`
7. `TreeGraph.Blazor.Shared/NodeEav/Components/FieldRenderers/TimeField.razor`
8. `TreeGraph.Blazor.Shared/NodeEav/Components/FieldRenderers/FileField.razor`
9. `TreeGraph.Blazor.Shared/NodeEav/Pages/Entities/EntityEdit.razor`
10. `TreeGraph.Blazor.Shared/NodeEav/Pages/Entities/EntityList.razor`
11. `TreeGraph.Blazor/Program.cs`
12. `TreeGraph.Blazor/Components/App.razor`（或 MainLayout）
13. 既有测试：注入 FakePlatformContext（约 6 个测试类）
14. 既有测试：同步 DynamicForm 选择器

---

## 九、换账号后快速开始

1. **读取计划文件**：[maui-blazor-adaptation-plan.md](file:///d:/APromisedLand/.trae/documents/maui-blazor-adaptation-plan.md)
2. **读取本交接文档**：`docs/handoff-maui-blazor-adaptation.md`
3. **从 Phase 0 开始执行**，按 Phase 0 → 1 → 2 → 3 顺序推进
4. **每 Phase 完成后**：`dotnet build` → `dotnet test` → 手工验证 → 提交
5. **遇到问题时**：对照本交接文档的"风险清单"和"评估报告修订点"排查

**关键参考**：`TreeGraph.Blazor.Shared/Common/PageDialogSky.razor`（当前完整内容在 session context 中已加载，换账号后需重新读取）。
