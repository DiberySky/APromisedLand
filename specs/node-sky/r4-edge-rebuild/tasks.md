# R4 · Edge 简化重构（tasks）

> 执行顺序即依赖顺序；每步完成即构建验证。禁止 commit/push。

## T1 实体与 DTO
- [x] 删除 `TreeGraph.Api/EdgeSky/Entities/EdgeDefinition.cs`（含 `EdgeInstance`）。
- [x] 新建 `TreeGraph.Api/EdgeSky/Entities/EdgeInstance.cs`：`EdgeId`/`Node`/`TypeName`/`FromNodeId`/`ToNodeId`/`IsDirected`/`CreatedAt`/`UpdatedAt`。
- [x] 重写 `TreeGraph.Shared/EdgeSky/Dtos/EdgeDtos.cs`：保留/新增 `CreateEdgeRequest`、`UpdateEdgeRequest`、`EdgeInstanceDto`、`EdgeTraverseRequest/Result`；删除 EdgeType 三件套与 `EdgeDeleteResult`。
- [x] `TreeGraph.Shared/Messaging/EdgeChangedEvent.cs` 注释改为 `type_name`。

## T2 DbContext
- [x] 删除 `DbSet<EdgeDefinition>` 与 `ConfigureEdgeInstances` 中 definitions 配置/种子。
- [x] 重配 `edge_instances`：新列、唯一索引 `(type_name, from_node_id, to_node_id)`、from/to 索引、两条 nodes FK、共享主键 FK。
- [x] `line_structures`：删除 `EdgeTypeId` 列/FK/索引。
- [x] `line_structure_segments`：删除到 `edge_instances` 的 FK；新增 `Weight`/`DisplayName` 列配置。
- [x] `TreeGraph.Api/Data/Seeding/...` 如引用 EdgeDefinition 种子则同步清理。

## T3 服务与控制器
- [x] 重写 `EdgeInstanceService`（创建/更新/删除/列表/出边/入边/遍历/DTO 组装），删除全部拓扑方法。
- [x] 合并控制器：重写 `EdgeController` 为全局 `api/eav/edges` 路由；删除 `EdgeInstancesController`、`EdgeMetadataController`。
- [x] 删除 `EdgeMetadataService`、`EdgeNodeValidator.cs`。
- [x] `Program.cs` 删除 `IEdgeNodeValidator`/`EdgeMetadataService` 注册与 `LineService` 注册。

## T4 LineSky 解耦
- [x] `LineStructure` 删除 `EdgeTypeId`；`LineStructureSegment` 增加 `Weight`/`DisplayName` 文档注释（无 FK 语义）。
- [x] `LineStructureService`：移除 EdgeInstanceService 依赖；段直接落库并生成 Guid；删除段/清结构不级联边；读取不再查 EdgeDefinitions/EdgeInstances。
- [x] `LineStructureDtos`：删除 `EdgeTypeId/EdgeTypeName/EdgeTypeDisplayName/DefaultEdgeTypeName`。
- [x] 删除 `LineService.cs`、`LineController.cs`；`LineDtos` 删除 `LinePathResult/LineValidationResult/LineConnectedComponent` 等死 DTO；`LineApiClient` 删除对应方法。
- [x] `LinePackage`/模板（`RailwayFeedingTemplate` 等）删除 `DefaultEdgeTypeName`。

## T5 图同步
- [x] `GraphSynchronizer`：`SyncEdgeAsync` / 全量重建 / 按类别同步改用 `TypeName`，边属性改 `edge_id`+`is_directed`。
- [x] `ResolveNodeAsync` 改为 nodes 单查 + host_type 分派；`GraphNaming` 增加 `NodeTag`。
- [x] `GraphSchemaInitializer`：语义边类型来自 `edge_instances.type_name`；新属性列。

## T6 前端
- [x] 删除 `EdgeMetadata.razor`、`EdgeDefinitionCreateDialog.razor`、`EdgeDefinitionEditDialog.razor`；`SpaceListPage.razor` 移除入口。
- [x] `EdgeApiClient` 简化：删除类型方法，实例方法改全局路由。
- [x] `EdgeInstances.razor`、`EdgeRelationPanel.razor`、`EdgeDialog.razor` 适配 typeName/有向/无 Weight/DisplayName。
- [x] `LineStructureDialog.razor` 移除边类型下拉与 `EdgeTypeId`。

## T7 迁移
- [x] `dotnet ef migrations add RebuildEdgeModule`，人工校正顺序：先删 FK/列/旧表，清 edge 数据与 nodes 注册，再建新表/索引。
- [x] 删除/确认旧迁移链无 `EdgeDefinition` 种子残留（仅新迁移负责清空）。

## T8 测试重写
- [x] Api：重写 `EdgeTests`、`EdgeHostTests`、`EdgeNodeGuardTests`（并入 EdgeTests 或改 nodes FK 语义）、`DeletePrincipleTests` 边段、`LineStructureTests`（去 EdgeTypeId/IsLine）、`SourceTextBuilderTests` 边用例。
- [x] Blazor.Shared.Tests：重写 `EdgeDialogSmokeTests`、`LineStructureDialogSmokeTests`；删除对两个定义对话框的渲染测试。
- [x] 新增用例覆盖：nodes FK 400、有向/无向重复 409、边删除引用 409、EAV type_name、无向对称查询、线结构与边解耦、共享主键注册。
- [x] 分别 build 测试项目后跑五套测试（禁止 `--no-build` 用陈旧 DLL）。

## T9 验证与评审
- [x] `dotnet build` 全解决方案 0 错误。
- [x] 五套测试全绿且数量不低于基线（Blazor.Shared 235+1 skip；Api 322；MafWfApi 65；FileStorageApi 153；Blazor 71）。
- [x] 写 `specs/node-sky/r4-edge-rebuild/review.md`：逐项对照 §8.6、迁移风险、剩余欠账（线路入图、EdgeInstance 顶点、EAV 关系类型目录重建）。
