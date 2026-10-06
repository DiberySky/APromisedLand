# StringTree 组件家族 — 设计笔记与排查记录

> 本文档记录 StringTreeSky（T=string）组件家族从设计到落地过程中的**真实缺陷修复**、**文档 vs 代码偏差**、以及**测试伪影教训**。
>
> 源文档（批次 1-4 + C + D）存于 IDE 会话缓存，未纳入 git；关键决策只存在于对话历史。本文档将其固化，供未来维护 StringTree 或类似组件时参考。

## 一、组件家族概览

| 层级 | 内容 | 位置 |
|---|---|---|
| 数据层 | `StringNodeMeta` / `StringNodeAction` 模型；`IStringTreeDataSource` / `IStringTreeActionHandler` 接口；`NoopStringTreeActionHandler` | `TreeGraph.Blazor.Shared/Trees/StringTree/Models+Services/` |
| 合同层 | `StringNodeActionResult` / `StringNodeTemplate` / `StringParentSelectResult` | `.../Contracts/` |
| 对话框层 | `StringTreeDialogService` + 5 对话框（Actions / View / Edit / Sort / ParentSelect） | `.../Services+Components/` |
| 主组件 | `StringTreeSky` + 4 partial（Action / Loading / Node / 参数）+ `AddLegacyTreeSky` | `.../Components/` + `.../Extensions/` |
| 内存示例 | `InMemoryStringTreeStore` / `DataSource` / `ActionHandler` | `TreeGraph.Blazor/Services/DemoTree/` |
| 演示页 | `/string-tree-demo` | `TreeGraph.Blazor/Components/Pages/StringTreeDemo.razor` |

验证状态：编译 0 错 0 警；Shared 137/137 + Blazor 118/118 单测；E2E 26/26 全绿。

---

## 二、三处真实缺陷修复（机制级原因）

### 缺陷 1：父选择对话框渲染崩溃 — `EventCallback<T>` 类型不匹配

**现象**：点击"移动"后对话框不弹出，控制台报类型转换异常。

**根因**：`StringParentSelectDialog` 用 `RenderTreeBuilder` 手动构建 `MudTreeViewItem<string>`，给 `OnClick` 传了 `EventCallback.Factory.Create<string>`。

但 MudBlazor 9.11 中 `MudTreeViewItem<T>.OnClick` 的真实类型是 `EventCallback<MouseEventArgs>`（经 `ilspycmd` 反编译 `MudTreeViewItem`1.dll` 核实）。`Create<string>` 产生的回调在运行时被强制转换为 `EventCallback<MouseEventArgs>`，**编译期无警告，运行时渲染即炸**。

**修复**：
```csharp
// 错误：Create<string>
EventCallback.Factory.Create<string>(this, _ => OnNodeClick(item.Id))

// 正确：Create<MouseEventArgs>，lambda 忽略参数
EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.MouseEventArgs>(
    this, _ => OnNodeClick(item.Id))
```

**教训**：手动 `RenderTreeBuilder` 传参**绕过编译期类型检查**——`AddAttribute("OnClick", callback)` 对 callback 的类型不做校验。声明式 `<MudTreeViewItem OnClick="@(...)" >` 则会在编译期检查 `EventCallback<T>` 的泛型匹配。**优先用声明式，手动构建时必须反编译核实参数类型。**

---

### 缺陷 2：`SelectedName` 提示条不反映真实选中

**现象**：点击节点后，alert 提示条始终显示 `CurrentParent?.Text`，选中状态不更新。

**根因**：`SelectedName` 的实现是：
```csharp
private string SelectedName
    => _filteredNodes?.FirstOrDefault(n => n.Id == _selectedId)?.Text
       ?? CurrentParent?.Text
       ?? "根节点";
```

当 `_filteredNodes` 为 null（无搜索时），`FirstOrDefault` 返回 null，于是 `??` 回退到 `CurrentParent?.Text`——**恒显示当前父节点，与 `_selectedId` 无关**。

**修复**：直接从 `AllNodes` 查 `_selectedId`：
```csharp
private string SelectedName
    => _selectedId is null
        ? "根节点"
        : AllNodes.FirstOrDefault(n => n.Id == _selectedId)?.Text ?? "根节点";
```

**教训**：`??` 链的回退顺序要谨慎——"搜索结果命中"和"无搜索回退"是两条独立路径，不应合并成一个 `??` 链。

---

### 缺陷 3：种子数据不回填 `HasChildren` — 第二层节点无法展开

**现象**：第一层（电子产品、服装）能展开，但第二层（手机、电脑）点击展开箭头无反应。

**根因**：`InMemoryStringTreeStore` 的种子构造用 `AddChild(parentId, child)` 建树，但 `AddChild` **不回填父节点的 `HasChildren`**。而 `ToTreeItemData` 把 `Expandable = meta.HasChildren`——手机/电脑的 `HasChildren=false` → `Expandable=false` → UI 上没有展开箭头。

对比：`Create()` 方法正确回填了 `parent.HasChildren = true`，但种子的 `AddChild` 漏了。

**修复**：`AddChild` 末尾补回填：
```csharp
var parent = _nodes.GetValueOrDefault(parentId);
if (parent is not null) parent.HasChildren = true;
```

**教训**：**建树的两条路径（种子 AddChild vs 业务 Create）必须语义一致**——子节点存在 ⇒ 父节点 `HasChildren=true`。否则懒加载的 `Expandable` 标志会错。

---

## 三、批次设计文档与实际代码的偏差（9 条）

| # | 文档假设 | 实际代码 | 修正 |
|---|---|---|---|
| 1 | `ShowActionsDialogAsync(Meta("1"))` 单参数 | 签名需 `parentNode` 参数 | 补 `parentNode: null` |
| 2 | 对话框组件缺 using | 组件在 `.Components` 命名空间 | 补 `@using ...StringTree.Components` |
| 3 | 祖先节点应被禁用（防环） | `IsNodeDisabled` 只禁自身+后代 | 断言改为 `IsNotDisabled`（祖先可作移动目标） |
| 4 | 空根 → 显示 `ProgressCircular` | `StringTreeSky` 空根渲染空 `MudTreeView` | 断言改为 `ShowsTreeViewNotProgress` |
| 5 | `GetChildrenCallCount == 1` | 初始加载不调 `GetChildren`（只调 `GetRootsAsync`） | 改为 `== 0` |
| 6 | 测试数据未设 `HasChildren` | 懒加载依赖 `HasChildren=true` 才显示展开箭头 | `Root` helper 加 `hasChildren` 参数 |
| 7 | 断言 `disabled` 属性 | MudBlazor 的 `MudButton` 用 `disabled` 属性（非 `aria-disabled`） | 用 `GetAttribute("disabled")` |
| 8 | `currentParent ?? 默认值` 覆盖 null | null 被错误替换为默认值 | 区分无参/带参重载，保留 null 语义 |
| 9 | 直接调 `StateHasChanged` 方法 | bUnit 需在 Dispatcher 线程 | 用 `InvokeAsync(() => ...)` 包裹 |

**说明**：偏差 3、4、5、8 是设计文档本身的错（AI 起草时未逐行核对源码），落盘阶段做了正确的"反纠正"。偏差 1、2、6、7、9 是文档遗漏/简化。

---

## 四、"选中不更新"排查的四类测试伪影教训

### 背景

多轮浏览器验证均报告"父选择对话框中点击节点，alert 不更新、selected 类不变、点击无反应"。最终经 bUnit 取证 + 无伪影浏览器终验，**确认组件无缺陷**——所有"故障"都是测试方法的伪影。

### 四类伪影

| # | 伪影 | 根因 | 识别方法 |
|---|---|---|---|
| 1 | **跨 evaluate 复用元素引用** | Playwright 代理在一次 evaluate 中查询元素，在下一次 evaluate 中点击——但 MudBlazor 重渲染后旧节点已脱离 DOM | 同一次 evaluate 内完成查询+点击；或每次重新查询 |
| 2 | **全局选择器污染** | `document.querySelectorAll('.mud-treeview-item-selected')` 抓到了**背景主页面上主树**的选中状态，而非对话框内 | 选择器限定 `.mud-dialog` |
| 3 | **禁用节点的选中类被抑制** | MudBlazor 在 `GetDisabled()` 时不渲染 `mud-treeview-item-selected` 类（反编译 `MudTreeViewItem.BuildRenderTree` 核实）——被点选的节点恰是禁用节点 | 确认点击的节点不是 `CurrentNode` 或其后代 |
| 4 | **代理点击精度** | 代理把 MoreHoriz 当展开箭头、或点到禁用节点 | 限定选择器到具体按钮 class，而非文本 |

### 取证方法

1. **bUnit 取证测试**：真实 MudBlazor + 真实 DialogParameters 传递链，3 项测试全部通过 → 组件无缺陷
2. **无伪影浏览器终验**：同一次 evaluate 内查询+点击、选择器限定 `.mud-dialog` → 完整闭环，"移动成功"

### 保留的修改

排查过程中做的三处修改全部保留（不只是排查，是真实改进）：
- `EventCallback<MouseEventArgs>`（缺陷 1，真实修复）
- `SelectedName` 直读（缺陷 2，语义改进）
- `ExpandOnClick` 移除（消除"箭头点击只展开不选中 vs 文本点击展开+选中"的两义性）

---

## 五、跨组件语义差异（TreeSky vs StringTreeSky）

详见 `docs/skills/tree-sky-doc-to-patch.md` 的"跨组件语义差异"章节。核心差异：空数据时 TreeSky 显示加载圈，StringTreeSky 渲染空树。

---

## 六、测试覆盖

| 批次 | 内容 | 测试数 |
|---|---|---|
| C 批次 | Shared.Tests 6 文件 + Blazor.Tests 3 文件 | 87 项 |
| D 批次 | E2E 2 项（懒加载展开 + 移动完整链路） | 2 项 |

E2E 场景 2 的 finally 清理段采用**双层保护**：外层 try-finally 保证清理必执行，内层 try-catch 吞掉清理异常不掩盖主体断言；清理逻辑幂等（命中 `newParentId == meta.ParentId` 时返回"父节点未变化"，不抛异常）。

---

## 七、变更历史

- 2026-10-04 初版：固化批次 1-4 + C + D 的缺陷修复、偏差、伪影教训
