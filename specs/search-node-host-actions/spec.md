# 搜索页结果行直开宿主节点操作对话框 — 重构 Spec

## 背景与目标

语义搜索结果行（`SemanticSearchItemDto`）的维护图标（⋯）当前指向已删除的
`SearchResultActionsDialog`（中间选择层），build 处于破坏状态。
用户已批准方案：**点 ⋯ 直接打开该节点类型对应的宿主操作对话框**，复用树自己的
`TreeNodeActionsDialog<TItem>` 与 LineNode 已有的 `LineNodeActions.OperateAsync`，
不做中间选择层。树内操作逻辑**原样搬迁**到共享编排器，树与搜索两侧复用。

## 已确认的关键事实（来自代码阅读）

- 活动后端为 **TreeGraph.Api**（Aspire，端口 5773），树控制器基类：
  `TreeGraph.Api\TreeEavSky\Controllers\TreeControllerBase.cs`；
  服务接口 `TreeGraph.Api\TreeEavSky\Services\ITreeService.cs`（无单节点读取）；
  4 个实现：UnitTreeService / CategoryTreeService / KnowledgeTreeService / SpaceTreeService，
  均含 `BuildText` + `ToNodeDto` + `Include(c => c.Node)` 模式（UnitTreeService.cs 为模板）。
  （`APromisedLand.Api\DiberyTree\*` 是另一套旧栈，不动。）
- 路由安全性：`GET {id}`（单段）与现有 `GET roots`/`GET full`（字面量优先）、
  `GET roots/{rootId}`、`GET children/{parentId}`、`GET {nodeId}/ancestors`（均双段）、
  `PUT {id}`/`DELETE {id}`（不同动词）均无冲突。
- 客户端 `TreeApiClient<T>`（TreeGraph.Blazor.Shared\TreeEavSky\Services\TreeApiClient.cs）
  已有 `SendAndGetDataOrNullAsync`（404→null）可用。
- 树内编排现状（TreeEavSky.razor.Action.cs）：`HandleNodeActionAsync` 循环——
  View/Edit/Attribute/Sort 执行后重开宿主；AddChild/Delete/Move 执行后退出；
  `ExecuteNodeActionAsync` 分发；Handle\* 私有方法依赖组件状态
  （`_items`、`_cts`、`RootId`、`SelectedValue`、`EditTemplate`、`GetParentNode`、
  `RefreshNodeChildrenAsync`、`RefreshTreeAsync`、`ReLoadingAsync`、`LoadChildrenAsync`）。
- 死代码（全仓库 grep 无调用方，私有可安全删除）：
  `ShowAddChildActionsAsync`、`AddRootNodeAsync`、`ExecuteTreeOperationAsync`、`OrderNodes`
  （TreeEavSky.razor.Action.cs 内）。`ExecuteNodeActionAsync` 为 public 但无外部调用方，
  保留为薄委托以维持公开面。
- `TreeNodeActionsDialog<TItem>` 参数：`NodeTemplate`（含 `Node.Value!.Parent?.Name` 工具条）、
  `ParentNode`、`IsBoot`；IsBoot 时隐藏移动/删除/编辑/属性。
- `TreeNodeEditDialog` 需要 `NodeTemplate.EditTemplate`（否则显示「未提供编辑模板」）；
  创建时标题用 `_node.Parent!.Name`。
- 4 棵树的 EditTemplate 目前内联在各自 DialogPage（Unit 含 Abbreviation；
  Category/Knowledge 含 IsArchived；Space 最简）。搜索侧要复用必须共享。
- LineNode 侧已就绪：`LineNodeActions.OperateAsync`（宿主循环 + 重拉 + Outcome 三态），
  菜单里 `OpenLineNodeActionsAsync` 已接好。
- 菜单消费页 2 处：`NodeEavSky\Pages\SemanticSearchDirect.razor`（OnChanged=重跑搜索，
  OnDeleted=移除行）与 `MafWfSky\Pages\SemanticSearch.razor`（仅 3 回调，无 OnChanged——
  EventCallback 可选，不受影响）。
- 测试：`NodeEavDialogSmokeTests.cs` 中 3 个 `SearchResultActionsDialog_*` 用例
  （L282-350，含私有辅助 SearchItem/ButtonTexts）随对话框一并删除。
- 构建前须停掉运行中的 AppHost/杀 TreeGraph*/APromisedLand* 进程防 DLL 锁；
  完成后重启 AppHost 做浏览器回归。

## 设计决策

### D1. 服务端补单节点读取（TreeGraph.Api 栈）

1. `ITreeService<T>` 增加：
   `Task<TreeNodeDto<T>?> GetNodeByIdAsync(string id, CancellationToken cancellationToken = default);`
2. 4 个实现各加同形方法（以 UnitTreeService 为模板）：
   - `Include(c => c.Node)` 按 Id 取实体，null → 返回 null；
   - `entity.ToNodeDto(BuildText(entity))`；
   - `HasChildren` 用 `AnyAsync(c => c.ParentId == id)` 重算（与 GetChildrenAsync 一致）；
   - `ParentId` 非空时取父实体，填 `dto.Parent` 与 `dto.Value.Parent`
     （宿主对话框工具条与移动确认信息需要父名）；
   - `Children` 不填（删除守卫用 Value.HasChildren 标志，排序对话框走懒加载——与树内未展开节点行为一致）。
3. `TreeControllerBase<T>`（TreeGraph.Api）在 GetChildren 后加：
   `[HttpGet("{id}")]` → 200 Ok(node) / 404 NotFound / 500；异常路径镜像现有写法。
4. `TreeApiClient<T>` 加 `GetNodeByIdAsync`（GET `{_basePath}/{id}`，404→null）。

### D2. 抽共享编排器 `TreeNodeActionOrchestrator<TItem>`

新文件 `TreeGraph.Blazor.Shared\TreeEavSky\Services\TreeNodeActionOrchestrator.cs`：

- `public enum TreeNodeHostOutcome { Canceled, Changed, Deleted }`
- `public enum TreeNodeActionExecResult { None, Changed, Deleted }`（单次执行结果）
- `public sealed class TreeNodeActionUi<TItem>`（两侧差异全部以回调注入）：
  - `RenderFragment<TItem>? EditTemplate`
  - `Func<TItem, bool> IsRoot`
  - `Func<string, TItem?> GetParentNode`（宿主工具条父名）
  - `Func<string?, TreeNodeItem<TItem>?> FindLoadedNode`（删除后定向刷新父；搜索侧恒 null）
  - `Func<TItem?, CancellationToken, Task<IReadOnlyCollection<TreeNodeItem<TItem>>>> LoadChildrenAsync`（排序懒加载）
  - `Func<TreeNodeItem<TItem>, CancellationToken, Task> RefreshNodeChildrenAsync`
  - `Func<CancellationToken, Task> RefreshTreeAsync`
  - `Func<TreeNodeItem<TItem>, CancellationToken, Task> ReloadAndExpandAsync`（移动成功后）
- `public sealed class TreeNodeActionOrchestrator<TItem>(
    TreeNodeDialogService<TItem>, ITreeActionHandler<TItem>, MessageService, IDialogService)`：
  - `Task<TreeNodeHostOutcome> OperateAsync(NodeTemplate<TItem>, TreeNodeActionUi<TItem>, CancellationToken)`
    —— 原 HandleNodeActionAsync 循环原样搬迁；累加 changed；Delete 成功 → Deleted；
    AddChild/Delete/Move 后退出；View/Edit/Attribute/Sort 后重开宿主。
  - `Task<TreeNodeActionExecResult> ExecuteAsync(NodeAction, TreeNodeItem<TItem>, TreeNodeActionUi<TItem>, CancellationToken)`
    —— 原 ExecuteNodeActionAsync 分发原样搬迁。
  - 私有 HandleAddChild/Edit/Delete/Move/Attribute/Sort —— **逐行搬迁**，
    仅把组件状态访问替换为 ui 回调、`_cts.Token` 替换为参数 ct；
    成功路径返回 Changed/Deleted 供 Outcome 计算（树侧忽略返回值，行为不变）；
    原 try/catch（含吞 OperationCanceledException）原样保留。
- DI：`TreeEavSkyServiceCollectionExtensions.AddTreeEavSky` 增加
  `services.AddScoped(typeof(TreeNodeActionOrchestrator<>));`

### D3. TreeEavSky 改为委托调用

`TreeEavSky.razor.Action.cs` 重写为薄壳：
- `[Inject] TreeNodeActionOrchestrator<TItem> NodeOps`
- `BuildActionUi()`：IsRoot=`v => v.Id == RootId`、GetParentNode/FindLoadedNode/LoadChildren/
  RefreshNodeChildren/RefreshTree/ReloadAndExpand 全部指向现有私有方法（原位保留）。
- `ShowNodeActionsAsync` → 组 NodeTemplate（含 ActionTemplate）→ `NodeOps.OperateAsync`。
- `public ExecuteNodeActionAsync` 保留 → `NodeOps.ExecuteAsync`（单次执行，无循环）。
- 删除：HandleNodeActionAsync、全部 Handle\*、死代码 4 个（见上）。
- `RefreshTreeAsync` 保留在 Action.cs（现作为 ui 回调体）。

### D4. EditTemplate 共享

新文件 `TreeGraph.Blazor.Shared\TreeEavSky\Trees\TreeEditTemplates.razor`
（namespace `...TreeEavSky.Trees`，仅 @code，4 个 `public static readonly RenderFragment<T>` 字段）：
- `UnitEdit`/`CategoryEdit`/`KnowledgeEdit`/`SpaceEdit`——markup 从 4 个 DialogPage **原样搬走**。
- 4 个 DialogPage 删除本地 `editTemplate` 变量，改为 `EditTemplate="@TreeEditTemplates.UnitEdit"` 等
  （子命名空间可直接见到父命名空间类型，无需额外 @using）。

### D5. SearchResultActionsMenu 接线（最终形态，无占位）

`SearchResultActionsMenu.razor`：
- 标记区不变（⋯ 维护 / 树定位条件显示 / ✨ AI 综合），仅 `@code` 重写 + 补 @using。
- 谓词：`IsTreeHost = MatchKind != "Entity" && HostType ∈ {UnitTree,CategoryTree,KnowledgeTree,SpaceTree}`；
  `HasTreeLocate = IsTreeHost && !string.IsNullOrEmpty(Item.RootId)`。
- `OpenMaintenanceAsync` 分发：
  - 树宿主 → `OpenTreeHostAsync<TItem>(TreeEditTemplates.XxxEdit)`（4 支 switch）：
    1. `Services.GetRequiredService<TreeApiClient<TItem>>()` → `GetNodeByIdAsync(Item.InodeId)`；
       异常 → Snackbar Error 返回；null/Value null → Snackbar 警告「节点不存在或已被删除」+ `OnDeleted`。
    2. `dto.ToTreeNodeItem()` 物化 → `NodeTemplate { Node = item }`（ActionTemplate 为 null）。
    3. ui 回调：EditTemplate=入参；IsRoot=`v => v.Id == Item.RootId || v.ParentId is null`；
       GetParentNode=`_ => node.Value?.Parent`；FindLoadedNode=`_ => null`；
       LoadChildrenAsync=`TreeApiClient.GetChildrenAsync` + `ToTreeNodeItem` 映射；
       三个刷新回调=空操作（搜索侧无可视树）。
    4. `Services.GetRequiredService<TreeNodeActionOrchestrator<TItem>>().OperateAsync(...)`；
       Outcome：Deleted→`OnDeleted`、Changed→`OnChanged`、Canceled→无操作。
  - `("LineNode", Node)` → 现有 `OpenLineNodeActionsAsync`（不动）。
  - 其他（裸节点 TreeNode / 实体命中）→ 现有 `AttributesAsync`（属性对话框直开，不动）。
- 删除：OpenActionsAsync、RenameAsync、CreateChildAsync、MoveAsync、DeleteAsync 及 4 个
  RenameTreeAsync/CreateChildTreeAsync/MoveTreeAsync/DeleteTreeAsync 泛型方法、
  不再使用的 NodeRenameDialog/NodeCreateChildDialog/TreeNodePickDialog 相关 @using。

### D6. 误建文件与测试清理

- 删除 `NodeEavSky\Components\Search\TreeHostActionsDialog.razor`（误建）。
- `NodeEavDialogSmokeTests.cs`：删 3 个 `SearchResultActionsDialog_*` 用例 + 私有辅助
  `SearchItem`/`ButtonTexts`；新增 1 个 `TreeNodeActionsDialog<UnitTree>` 冒烟用例
  （物化 NodeTemplate 直开，断言标题「节点操作」与按钮集：移动/子项排序/设置修改/属性/创建子项/删除）。
  若 NodeAttributeItemsSky 在桩 HttpClient 下渲染失败，则去掉该断言改为仅验证外壳与按钮。

## 影响面与回归点

- 树内操作全链行为不变（搬代码不改逻辑）：重命名(编辑)/建子/移动/删除/属性/排序。
- 4 个树对话框页：仅 EditTemplate 来源变化（同一 markup）。
- 搜索页：⋯ 直开宿主对话框；树定位/AI 综合不变。
- API 新增 `GET {Controller}/{id}` ×4 控制器。
- DI 新增 open-generic scoped，无破坏性。

## 验证步骤

1. 停后台 AppHost 两个 shell + 杀 TreeGraph*/APromisedLand* 进程。
2. `dotnet build` 全解决方案 0 错误。
3. `dotnet test` 各 *.Tests（Blazor.Shared 243+1跳 / 其余 366/71/65/153 不回归）。
4. 重启 AppHost，浏览器回归：
   - 树对话框内：重命名、建子、移动、删除、属性、排序全链；
   - 搜索页 ⋯：树宿主直开 TreeNodeActionsDialog（连续操作：编辑后回宿主、删除后关）；
     LineNode 直开 LineNodeActionsDialog；裸节点直开属性对话框；
   - 树定位/AI 综合图标正常。
5. 完成后向用户汇报，待指示再提交。
