# 同步刷新对话框（space/tree/line/edge → 向量/图 过程进度）

## Context

需求：在 空间管理 =》 空间维护 Tab 增加 icon refresh，点击打开「同步刷新」对话框，分别列出 space / tree / line / edge 四类数据同步到 向量(Qdrant)/图(Nebula) 的过程进度。

现状事实：
- 后端无任何后台任务/进度上报设施，现有同步端点均为同步请求-响应
- 向量对账 `VectorSynchronizer.ReconcileAsync` 仅支持全量或按 spaceId 范围（补缺+删孤）
- 图侧仅 `GraphSynchronizer.RebuildAllAsync` 全量 DROP SPACE 重建；`NebulaGraphClient.BulkAsync` 为幂等覆盖写
- 向量库只有节点向量+实体向量，边（EdgeInstance）在向量库无对应数据

用户已确认：**方案一（前端分步编排）**；**边→向量格显示「不适用」**。

类别映射（与图装载顺序一致）：

| 类别值 | 含义 | 数据源 | → 向量 | → 图 |
|---|---|---|---|---|
| space | 空间树 | SpaceTrees | 节点向量+挂载实体向量 | 顶点 space_tree + parent_of 边 |
| tree | 单位/分类/知识树 | UnitTrees∪CategoryTrees∪KnowledgeTrees | 同上 | 顶点三 tag + parent_of 边 |
| line | 线路 | LineNodes | 同上 | 顶点 line_node |
| edge | 语义边 | EdgeInstances join EdgeDefinitions | **无（显示不适用）** | 语义边 |

图侧按类别为幂等覆盖推送（不 DROP）；图内残留清理仍靠事件同步/全量重建（用户已接受）。

## 后端改动（全部增量添加，不回退既有未提交改动；不改 csproj / NebulaGraphClient.cs）

1. **新增** `TreeGraph.Api\GraphSyncSky\SyncRefreshCategory.cs`
   - `enum SyncCategory { Space, Tree, Line, Edge }` + 字符串解析/校验辅助

2. `TreeGraph.Api\NodeEavSky\SemanticSearch\VectorSyncService.cs`（VectorSynchronizer）
   - 新增 `ReconcileCategoryAsync(SyncCategory, ct)`：解析类别 inode 范围后走既有 `ReconcileAsync` 主体（scope 过滤逻辑不变）
   - 新增 `ResolveCategoryScopeAsync`：space=SpaceTrees Id（排除 Spaces 表内 Id，与现逻辑一致）；tree=Unit∪Category∪Knowledge 树 Id；line=LineNodes Id
   - 既有 `ReconcileAllAsync` / `ReconcileSpaceAsync` 不动

3. `TreeGraph.Api\NodeEavSky\SemanticSearch\SearchController.cs`
   - `POST api/search/reconcile` 增加可选 query 参数 `category`（space|tree|line）；与 `spaceId` 互斥（同时提供返回 400）
   - 响应形状不变（active/existing/missing/orphaned/fixedCount/deletedCount）

4. `TreeGraph.Api\GraphSyncSky\GraphSynchronizer.cs`
   - 新增 `SyncCategoryAsync(SyncCategory, ct)`：按类别构建 vertices/edges（复用 RebuildAllAsync 中的查询投影与 `MakeVertex`），经既有 `BulkWithSchemaRetryAsync` 幂等写入（不 DROP SPACE）
   - 返回复用 `GraphRebuildResult`（Vertices/ParentEdges/SemanticEdges/VerticesWritten/EdgesWritten/Errors）

5. `TreeGraph.Api\GraphSyncSky\GraphSyncController.cs`
   - 新增 `POST api/graph-sync/category/{category}`，校验类别后调用 `SyncCategoryAsync`
   - 既有 `POST api/graph-sync/rebuild` 不动

6. **新增** DTO 到 `TreeGraph.Shared\NodeEavSky\Dtos\SemanticSearchDtos.cs`（或同层新建文件）
   - `GraphCategorySyncResultDto(int Vertices, int Edges, int VerticesWritten, int EdgesWritten, IReadOnlyList<string> Errors)`，控制器与 Blazor 共用

## 前端改动

7. `TreeGraph.Blazor.Shared\NodeEavSky\Services\EavApiClient.cs`
   - `ReconcileVectorsAsync` 增加可选 `category` 参数（拼 URL query）
   - 新增 `SyncGraphCategoryAsync(string category, ct)` → `POST api/graph-sync/category/{category}` → `GraphCategorySyncResultDto?`

8. `TreeGraph.Blazor.Shared\SpaceSky\Components\SpaceManagePanel.razor`（入口）
   - 表头操作列「新建 +」旁新增 `Icons.Material.Filled.Refresh` 图标（Color.Info，title=同步刷新）
   - OnClick 用 `DialogService.ShowExAsync` 打开 SyncRefreshDialog（TopCenter / Small / FullWidth / MaximizeButton，与 OpenEntityTypeDefsAsync 一致）

9. **新增** `TreeGraph.Blazor.Shared\SpaceSky\Dialogs\SyncRefreshDialog.razor`（DialogSky 外壳）
   - 内容：MudTable 4 行（空间树 / 单位·分类·知识树 / 线路 / 语义边）× 列（向量 / 图）；边→向量格显示「—（无边向量）」
   - 单元格状态机：等待 → 同步中（MudProgressLinear Indeterminate）→ 完成（向量：新增 X · 清理 Y；图：顶点 X · 边 Y，Errors>0 时 Warning 色并提示错误数）→ 失败（Error 色错误消息）
   - `OnInitializedAsync` 自动开始执行：向量 space→tree→line，图 space→tree→line→edge，顺序调用，每步更新格子并 StateHasChanged；异常捕获落到对应格子，不中断后续步骤
   - Actions：左侧运行中/完成摘要文本；提交按钮「开始同步」运行中禁用，结束后变「重新同步」（重置全部格子再执行）；取消=关闭

## 验证

1. `dotnet build` TreeGraph.Api 与 TreeGraph.Blazor 通过
2. AppHost 已在后台运行（job-c3b858a5de6148fbbfb3c866b588d84d，Dashboard http://localhost:15030）；eavapi 动态端口从 `%LOCALAPPDATA%\Temp\aspire-dcp*` 最新目录 `*_out` 日志的 "Now listening on" 获取
3. 浏览器打开 /spaces → 空间维护 → 点 refresh 图标 → 对话框自动执行 7 步（边→向量跳过）→ 核对各格统计与 eavapi 日志（向量对账/图类别同步日志）
4. 注意：Blazor SSR 中文输出为 HTML 实体属既有现象；既有未提交改动（Wolverine 迁移等）保持原样
