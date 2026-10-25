# 任务清单 — 搜索页结果行直开宿主节点操作对话框

依赖顺序执行；每步完成后再进行下一步。

## T1 清理现场
- [x] 删除 `TreeGraph.Blazor.Shared\NodeEavSky\Components\Search\TreeHostActionsDialog.razor`
- [x] `NodeEavDialogSmokeTests.cs`：删 `SearchResultActionsDialog_TreeHost_FullActions` /
      `SearchResultActionsDialog_LineNode_HostEntryOnly` / `SearchResultActionsDialog_EntityMatch_MinimalActions`
      及私有辅助 `SearchItem`、`ButtonTexts`

## T2 服务端 GET {id}（TreeGraph.Api 栈）
- [x] `TreeGraph.Api\TreeEavSky\Services\ITreeService.cs`：加 `GetNodeByIdAsync`
- [x] `Services\UnitTreeService.cs` / `CategoryTreeService.cs` / `KnowledgeTreeService.cs` / `SpaceTreeService.cs`：
      各加实现（Include Node + ToNodeDto + HasChildren 重算 + 填 Parent/Value.Parent）
- [x] `Controllers\TreeControllerBase.cs`：加 `[HttpGet("{id}")] GetById`（200/404/500 镜像现有风格）
- [x] `TreeGraph.Blazor.Shared\TreeEavSky\Services\TreeApiClient.cs`：加 `GetNodeByIdAsync`（404→null）

## T3 共享编排器
- [x] 新建 `TreeGraph.Blazor.Shared\TreeEavSky\Services\TreeNodeActionOrchestrator.cs`：
      `TreeNodeHostOutcome` / `TreeNodeActionExecResult` / `TreeNodeActionUi<TItem>` /
      `TreeNodeActionOrchestrator<TItem>`（OperateAsync 循环 + ExecuteAsync 分发 + Handle* 逐行搬迁）
- [x] `TreeEavSkyServiceCollectionExtensions.cs`：注册 `TreeNodeActionOrchestrator<>`

## T4 TreeEavSky 薄壳化
- [x] `TreeEavSky.razor.Action.cs` 重写：注入 NodeOps、BuildActionUi()、
      ShowNodeActionsAsync→OperateAsync、ExecuteNodeActionAsync→ExecuteAsync；
      删除 HandleNodeActionAsync/Handle*/死代码 4 个；保留 RefreshTreeAsync 作回调体

## T5 EditTemplate 共享
- [x] 新建 `TreeEavSky\Trees\TreeEditTemplates.razor`（4 个 static readonly 模板，markup 原样搬走）
- [x] 4 个 DialogPage（Unit/Category/Knowledge/Space）改用共享模板

## T6 菜单接线
- [x] `SearchResultActionsMenu.razor` @code 重写：OpenMaintenanceAsync 分发
      （树宿主→GetById+TreeNodeActionsDialog+编排器；LineNode→OperateAsync；裸节点/实体→属性对话框）、
      恢复 HasTreeLocate 谓词、删除旧泛型操作方法

## T7 测试
- [x] `NodeEavDialogSmokeTests.cs` 新增 `TreeNodeActionsDialog_TreeHost_Renders` 冒烟用例

## T8 验证
- [x] 停后台 shell（97c9c429…、54ad4846…）+ 杀 TreeGraph*/APromisedLand* 进程
- [x] `dotnet build` 0 错误（期间修复：TreeEditTemplates 静态字段内 @bind 依赖 this → 拆出 XxxTreeEditFields 组件薄包装）
- [x] `dotnet test` 全部通过（Blazor.Shared 243+1跳；其余 366/71/65/153 不回归；E2E 需运行环境不在门禁内）
- [x] 重启 AppHost，浏览器回归：树内操作全链 + 搜索页 ⋯ 三类宿主直开（裸节点分支开发库无数据，复用既有链路未浏览器覆盖）
- [x] 向用户汇报，待指示再提交
