# R1 · DiberyTree 解绑（TreeEavSky 树节点包装器本地化）

> 上游蓝图：`specs/node-sky/spec.md` §8.3 第 1 步 / §8.4。本文是 R1 的执行规格（决策完备，可施工）。
> 日期：2026-10-24

## 1. 目标

TreeGraph.Blazor.Shared 业务代码中所有 `ITreeItemData<T>` / `TreeItemData<T>` 引用替换为本地自有类型 `TreeNodeItem<T>`；MudTreeView 边界处因 `TreeNodeItem<T>` 实现 `ITreeItemData<T>` 继续可用。UI 行为零变化。

## 2. 摸底结论（已核实，勿重复调研）

1. **外部类型来源是 MudBlazor 9.11 自带**（非 DiberyTree NuGet 包；TreeGraph.Blazor.Shared.csproj 无 DiberyTree 引用）。MudBlazor 本身保留（UI 库），解绑含义 = 业务代码不再直接消费 MudBlazor 的树数据契约，改用本地类型。
2. **MudBlazor 9.11 接口形状**（反射 + Rider SourcesCache 反编译双重确认）：
   - `ITreeItemData<T>`：`string? Text {get;set;}` / `string? Icon {get;set;}` / `T? Value {get;}`（接口仅 getter）/ `bool Expanded/Expandable/Selected/Visible {get;set;}` / `IReadOnlyCollection<ITreeItemData<T>>? Children {get;set;}` / `bool HasChildren {get;}`（仅 getter）
   - `TreeItemData<T>` 默认实现：`Expandable=true`、`Visible=true` 默认；`HasChildren => Children is {Count:>0}`；**重写 Equals/GetHashCode（按 Value 比较）**，实现 `IEquatable<TreeItemData<T>>`。
3. **边界约束**：`TreeEavSky.razor` 中 `@bind-Items="context.Children"` 经接口 setter 双向绑定 → 本地类型的 `Children` 声明类型必须与接口**完全同型**（`IReadOnlyCollection<ITreeItemData<T>>?`），不能用 `List<TreeNodeItem<T>>`（属性签名不匹配则无法隐式实现接口）。
4. **ItemTemplate 的 `context` 类型是 `ITreeItemData<TItem>`**（MudBlazor 强制），razor 内属性直读保留；传给业务方法处强转 `(TreeNodeItem<TItem>)context`（运行时实例全是本地类型，转换安全）。
5. `MudTreeView.ServerData` 的确切签名反射受 ASP.NET 依赖加载限制未能直接读出；但本方案保持**形状完全等价替换**（同接口实现、同声明返回类型 `Task<IReadOnlyCollection<TreeNodeItem<TItem>>>` 替换 `Task<IReadOnlyCollection<TreeItemData<TItem>>>`），与今日可编译代码结构一一对应，编译行为一致。
6. 旧项目（`APromisedLand.Razor/DiberyTree/`、`APromisedLand.SharedRazor/Components/TreeGraph/`、`APromisedLand.Shared/DiberyTree/`）**不在本期范围**（spec §8.4 注明「旧项目是否仍在服役待确认」，确认前不动）。
7. 解决方案内 TreeGraph.* 其余项目（Blazor/Server/Maui/Api/Shared/Tests 非 Blazor.Shared.Tests）**零引用**这两个类型（全仓 grep 确认）。

## 3. 决策

| # | 决策 | 理由 |
|---|---|---|
| D1 | `Children` 与接口**完全同型**（`IReadOnlyCollection<ITreeItemData<T>>?`），内部遍历保留既有 `OfType<TreeNodeItem<T>>()` 模式 | `@bind-Items` 经接口 setter；零行为差异、纯机械改名 |
| D2 | 业务方法签名一律改 `TreeNodeItem<T>`；razor 边界 `context` 保留接口类型，仅 2 处 OnClick 调用强转 | 业务解绑 + MudBlazor 边界保留 |
| D3 | `TreeNodeItem<T>` 镜像 MudBlazor 的 Equals/GetHashCode（按 Value）+ `IEquatable<TreeNodeItem<T>>` | 保留 MudTreeView 内部可能依赖的相等语义，防隐性行为变化 |
| D4 | 扩展方法 `ToTreeItemData` → `ToTreeNodeItem`；常量 `TreeItemIcons` 名称不变 | 名实相符；最小改动 |
| D5 | ~~`Value` 用 `{get;set;}`~~（被 D8 取代：继承后沿用基类 `{get;init;}`，全仓无构造后赋值） | — |
| D6 | 新类型放 `TreeEavSky/TreeNodeItem.cs`，命名空间 `TreeGraph.Blazor.Shared.TreeEavSky` | 所有消费方（含 razor 子命名空间 Trees.X/Nodes/Dialogs）经外层命名空间解析自动可见，`_Imports.razor` 与 using 零改动 |
| D7 | 不增删任何 `using MudBlazor;`（个别文件改后变为未使用也保留） | 机械改名，不夹带清理 |
| D8 | **（构建实证修正，取代 D1/D3/D5 的接口实现路线）`TreeNodeItem<T>` 改为继承 MudBlazor `TreeItemData<T>` 的空子类**；`TreeEavSky.razor` 的 ServerData 改 `async p => await LoadChildrenAsync(p)` 走 `IReadOnlyCollection` 协变 | 首轮构建实证：MudBlazor 9.11 `MudTreeView.ServerData` 签名锚定**具体类** `TreeItemData<T>`（`Task` 不变性使接口实现无法协变喂入）。继承后：运行时实例恒为 TreeNodeItem（ItemTemplate 强转安全）、Equals/HasChildren/默认值全继承零差异 |

## 4. 改动清单

### 4.1 新建（1）

`TreeGraph.Blazor.Shared/TreeEavSky/TreeNodeItem.cs`（最终形态按 D8 继承，Equals/默认值继承基类）：

```csharp
using MudBlazor;

namespace TreeGraph.Blazor.Shared.TreeEavSky;

/// <summary>TreeEavSky 本地树节点包装器（spec §8.4 解绑）：业务代码一律消费本类型；
/// 继承 TreeItemData<T> 的原因见 D8（ServerData 锚定具体类）。</summary>
public class TreeNodeItem<T> : TreeItemData<T>
{
}
```

### 4.2 机械替换（TreeGraph.Blazor.Shared，15 文件）

统一规则：`TreeItemData<T>` → `TreeNodeItem<T>`；业务签名中 `ITreeItemData<T>` → `TreeNodeItem<T>`；`ToTreeItemData` → `ToTreeNodeItem`；razor 的 `context.*` 属性直读与 `@bind-Items="context.Children"` **不动**。

| 文件 | 要点 |
|---|---|
| `TreeEavSky/TreeHelper.cs` | 5 个扩展方法签名/返回/构造替换；`OfType<TreeItemData<T>>()` → `OfType<TreeNodeItem<T>>()`；注释更新 |
| `TreeEavSky/TreeEavSky.razor.cs` | `_items` → `List<TreeNodeItem<TItem>>?` |
| `TreeEavSky/TreeEavSky.razor.Loading.cs` | 两个 Load 方法返回类型 + `ToTreeNodeItem`；`RefreshNodeChildrenAsync`/`ReLoadingAsync` 参数；`new TreeNodeItem<TItem>{…}` |
| `TreeEavSky/TreeEavSky.razor.Node.cs` | `ClickItemText`/`FindNodeById`/`RemoveNodeFromParent` 等签名；内部栈/队列可保留接口类型（协变兼容）；`OfType<TreeNodeItem<TItem>>()` |
| `TreeEavSky/TreeEavSky.razor.Action.cs` | 8 个方法签名；`new TreeNodeItem<TItem>` |
| `TreeEavSky/TreeEavSky.razor` | L67/L72 参数类型；L32/L46 两处 `OnClick` 强转 `(TreeNodeItem<TItem>)context`；其余不动 |
| `TreeEavSky/TreeDialogPageSky.razor` | L60 `Func<TreeNodeItem<TItem>, Task>?`；L76 参数 |
| `TreeEavSky/TreeSelectDialogSky.razor` | L47/L55/L72 三处 |
| `TreeEavSky/Contracts/NodeTemplate.cs` | L11 `Node` 属性类型 |
| `TreeEavSky/Dialogs/TreeNodeDialogService.cs` | L162/L234 两处参数 |
| `TreeEavSky/Dialogs/BlazorService.cs` | L35/52/69/86 四个方法参数 |
| `TreeEavSky/Nodes/TreeNodeSortDialog.razor` | L38 `Node` 参数 |
| `TreeEavSky/Trees/Unit/UnitTreeDialogPage.razor` | L52/L54 |
| `TreeEavSky/Trees/Space/SpaceTreeDialogPage.razor` | L44/L52 |
| `TreeEavSky/Trees/Category/CategoryTreeDialogPage.razor` | L47/L52 |
| `TreeEavSky/Trees/Knowledge/KnowledgeTreeDialogPage.razor` | L47/L52 |

### 4.3 测试同步（TreeGraph.Blazor.Shared.Tests，4 文件）

`TreeHelperTests.cs`、`TreeHelperIterationTests.cs`、`Components/TreeEavSkyComponentTests.cs`、`Services/TreeNodeDialogServiceTests.cs`：同一套机械替换（编辑前逐个 Read）。

### 4.4 明确不做

- 不动 `Text()` 方法、`TreeNodeDto<T>`、服务层投影（R2 议题）；
- 不动旧项目任何文件；不改 csproj（MudBlazor 引用保留）；
- 不 commit / push（用户未要求）。

## 5. 验证

1. 确认无 `TreeGraph.*` 进程残留锁 DLL（上次因此构建失败），必要时 `Stop-Process`。
2. `dotnet build APromisedLand.sln` → 0 错误（既有 7 个 NuGet 版本冲突警告可接受）。
3. `dotnet test TreeGraph.Blazor.Shared.Tests` → **235 通过 + 1 跳过**（基线）。
4. 完工后 grep 确认：`TreeGraph.Blazor.Shared` 内 `TreeItemData` 仅可出现在 `TreeNodeItem.cs`（D8 基类）与注释；`ITreeItemData` 仅允许出现在 `TreeNodeItem.cs`、`TreeEavSky.razor` 边界、内部遍历栈/队列处。（已执行，符合）

## 6. 风险

| 风险 | 缓解 |
|---|---|
| razor 泛型组件改名后 Razor 源生成器报错 | 全量构建立即暴露；两处强转语法简单 |
| Equals 语义差异（引用相等 vs 按 Value） | D3 已镜像 MudBlazor 实现 |
| 测试基线偏移 | 以 235+1 为门禁，任何偏差视为回归必须修复 |
