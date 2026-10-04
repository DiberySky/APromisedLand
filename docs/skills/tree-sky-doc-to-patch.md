---
name: tree-sky-doc-to-patch
description: 将设计文档/补丁方案落地到真实代码库时，强制"先核实再编辑"，避免凭记忆重写。适用于 TreeSky / TreeGraph 等有多个 partial class 和泛型抽象的项目。
---

# Skill: 设计文档 → 增量补丁

## 触发场景

用户提供"设计文档"、"补丁方案"、"优化建议"或"批次 N 交付"，要求落地到某个代码库。

## 强制工作流（顺序执行，不可跳步）

### 1. 落地前核实（最重要）

**规则**：任何"文件修改"提案，**必须先读该文件的当前真实内容**。

**逐项核实清单**：

| 项 | 核实内容 | 为什么 |
|---|---|---|
| 方法签名 | 是否已有 `CancellationToken ct = default`？| 文档常漏 |
| 方法名 | 类型名、变量名逐字核对 | 不凭记忆 |
| 路由 | `[Route("[controller]")]` 还是显式路由？| 影响 URL |
| 返回类型 | 是否需要 `OrderBy` / `Select`？| 排序语义 |
| 排序语义 | 对话框返回的列表顺序是不是用户拖拽后的新顺序？后端按什么编号？ | 实测后再决定，别照抄文档的重排建议 |
| Razor 可选参数 | 方法组能否绑定带默认值的参数？| 需显式 lambda |
| Moq 匹配 | `CancellationToken.None` vs `It.IsAny<>()` | 精确匹配 |

### 2. 输出格式：增量 diff，禁止整文件替换

**正确**：
```diff
-private async Task LoadAsync()
+private async Task LoadAsync(CancellationToken ct = default)
 {
-    var items = await Api.GetAsync();
+    var items = await Api.GetAsync(ct);
```

**错误**：
```
这里是完整的新版本 LoadAsync 方法，请替换：
private async Task LoadAsync(CancellationToken ct = default) { ... }
```

**理由**：整文件替换会静默回退用户此前的本地修改（如批次 1 的非泛型 `IList` / `clearSelection: false`）。

### 3. 逐项标注

每处改动必须包含：
- **文件路径**（精确）
- **改动前**（原文）
- **改动后**（新文）
- **改动理由**（1 行）

### 4. 闭环验证

编译 + 跑所有相关测试 **通过后** 才交付。

**未验证的交付必须显式声明**："本批次未验证运行时行为"。

### 5. 回退预案

用户指出错误时：
1. 先承认（不辩解）
2. 记录到本文档的"反例黑名单"
3. 修

---

## 错误黑名单（本人实际犯过）

| # | 错误 | 根因 | 正确做法 |
|---|---|---|---|
| 1 | 凭记忆写类型名 `StringTreeNodeClientService` | 未核对真实文件 | 逐字对照 |
| 2 | 把非泛型 `IList<TreeItemData>` 改为泛型 `IList<TreeItemData<T>>` | 整文件替换时未保留批次 1 修复 | 增量编辑 |
| 3 | 擅自删 `clearSelection: false` | 同上 | 增量编辑 |
| 4 | #5 论断"ServerData 接管 Items" | 基于 GitHub 主分支源码（版本领先于运行时 DLL）| 用 `ilspycmd` 反编译本地 NuGet DLL |
| 5 | 文档建议排序前 `OrderBy(i => i.SortOrder)` / `Select(i => i.SortOrder)` 重排 | 未核实对话框输出语义；#6 实证：返回列表顺序即用户拖拽后的新顺序，后端按列表位置重新编号 SortOrder | 列表顺序直传，不做任何重排 |
| 6 | Razor 里 `ServerData="@LoadChildrenAsync"` 加参数后编译失败 | 未考虑 Razor 方法组绑定限制 | 改显式 lambda `@(p => LoadChildrenAsync(p, ct))` |
| 7 | Moq `Setup(s => s.Add(3 参))` 匹配失败 | 未考虑可选参数的精确匹配 | 用 `It.IsAny<Action<...>>()` + 显式类型 |
| 8 | "文件路径 X 有 bug"（未读该文件）| 凭记忆定位 | 先读源码再断言 |
| 9 | tag/提交信息声称"E2E 21/21"但本会话没跑 | 把历史数字当现状 | 起栈实测拿到通过证据后再写入不可篡改的 tag |

---

## 已有代码的特殊约束（TreeSky / TreeGraph）

### TreeSky 泛型约束

```csharp
where TItem : class, ITreeNodeBase<TItem>, new()
```

- 三约束必须齐备；`new()` 用于 `HandleAddChildAsync` 里 `new TItem()`
- `ITreeNodeBase<TItem>` 接口**无** `Children` 属性——反射是兜底，`ChildrenAccessor` 参数是首选

### MudBlazor 9.11 实测事实（反编译核实）

| 组件 | 事实 | 应对 |
|---|---|---|
| `MudExpansionPanel` | **不渲染** `aria-expanded` | 用 `.mud-collapse-entered` |
| `MudDrawer` Temporary | 关闭后**保留** DOM | 内容锚定（标题文本），不靠 `.First` |
| `MudTreeView` | `Items` 与 `ServerData` **可共存**，`Items != null` 时优先渲染 Items | 不用"接管"论断 |
| `MudTreeView` | `@bind-Items="context.Children"` **双向绑定**，MudBlazor 回写的是同一引用 | `_items` 与渲染树同步 |
| `DialogResult` | 构造函数 `protected` | 用 `DialogResult.Ok(x)` / `Cancel()` |
| `ISnackbar.Add` | 4 参（多 `key`）| Mock 时按实际签名 |
| `SnackbarOptions.OnClick` | `Func<Snackbar, Task>` | 不是 `EventArgs` |

### 后端契约（易误判）

| 项 | 事实 |
|---|---|
| `EfTreeService.UpdateNodeAsync` | **明确保留原 `ParentId`**——移动必须走 `MoveNodeAsync`（`POST {T}/move`，含防环，业务失败返回 400） |
| `EfTreeService.ToLazyDtoAsync` | 不填 `Parent`（与批量路径一致） |
| `EfTreeService.UpdateChildrenAsync` | 按**入参列表位置**重新编号 SortOrder——前端必须透传拖拽后的列表顺序 |
| `TreeControllerBase<T>` | `[Route("[controller]")]` → URL = 控制器名（`StringTreeNodeController` → `/StringTreeNode`） |
| `DiberyTreeApiClient<T>._basePath` | 优先 `[TreeRoute("xxx")]`，回退 `typeof(T).Name` |

---

## 反模式示例（禁止）

### ❌ "整文件替换"当"增量编辑"

```
这里是完整的新版本 TreeSky.razor.Node.cs：
[整份文件]
请替换。
```
**问题**：会静默回退用户此前修改。

### ❌ 凭记忆论断

```
ServerData 完全接管，Items 被忽略。
```
**问题**：未核实本地 DLL；用 `ilspycmd` 5 分钟就能证伪。

### ❌ 未声明验证状态

```
本方案已完整，请编译。
```
**问题**：未承诺跑过测试；用户需自行承担风险。

**正确**："本方案基于以下假设：[...]；编译 + 测试后如有偏差，我调整。"

---

## 变更历史

- 2026-10-04 初版：源于 TreeSky 批次 1-2 优化过程中 8 次文档 vs 代码偏差；#6/#8 落地时追加排序直传、move 路由、TreeRoute 与 E2E 实证条目
