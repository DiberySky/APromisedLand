# R4 · Edge 简化重构（spec）

> 依据：`specs/node-sky/spec.md` §8.6（2026-10-24 定稿）。
> 用户指令：「仅实现两个 Node 的关系，有向无向，其他交由 EAV」；「清空 edge 全部 seeds，不做兼容」。
> 约束：不 commit / 不 push；不改旧项目；构建 0 错误；五套测试基线不得下降。

## 1. 目标与终态

### 1.1 edge_instances（重建）

| 列 | 类型 | 说明 |
|---|---|---|
| `edge_id` | text PK | 注册进 `nodes`（`host_type="EdgeInstance"`, `name=null`），共享主键 FK |
| `from_node_id` | text FK → `nodes(id)` Restrict | 起点，任意 Node |
| `to_node_id` | text FK → `nodes(id)` Restrict | 终点，任意 Node |
| `type_name` | text 非空 | 裸列，兼 EAV `entity_type` 键、唯一约束判别列、NebulaGraph 边类型名 |
| `is_directed` | bool 非空 | 有向/无向，实例内核语义 |
| `created_at` / `updated_at` | timestamptz | 时间戳 |

索引：
- `uq_edge_instance (type_name, from_node_id, to_node_id)` 唯一；
- `ix_ei_from_node (from_node_id)`、`ix_ei_to_node (to_node_id)`；
- 无 `edge_type_id`、无部分唯一索引、无 `weight`/`display_name`/`enforce_single_incoming`。

### 1.2 删除清单

- 表 `edge_definitions` 与 `EdgeDefinition` 实体、`EdgeDefinition.SeedData()` 18 条种子；
- `EdgeMetadataService`、`EdgeMetadataController`、`/api/eav/metadata/edge-types` 全部端点；
- `EdgeNodeValidator` / `IEdgeNodeValidator`（端点存在性改由 nodes FK 强制）；
- `IsGlobal` / `IsActive` / `FromNodeType` / `ToNodeType` / `IsLine` / `IsLinear` / `EnforceSingleIncoming`；
- `EdgeInstanceService` 内全部线路/拓扑逻辑（`ValidateTopologyAsync`、`AnalyzeTopologyAsync`、`WouldCreateCycleAsync`、`AnalyzeTopology`）；
- `LineService`、`LineController`、`LineDtos` 中按 edgeTypeId 的路径/校验/连通分量 DTO（前端无调用，成为死代码）；
- 前端 `EdgeMetadata.razor`、`EdgeDefinitionCreateDialog.razor`、`EdgeDefinitionEditDialog.razor`。

## 2. API 决策（R4 简化）

边不再有类型表，路由改为以 `edgeId` 为主键的全局端点；类型名是请求字段/查询参数。

| 方法 | 路由 | 说明 |
|---|---|---|
| GET | `/api/eav/edges?typeName=&nodeIds=&search=` | 全局列表；`nodeIds` 命中 from/to；`search` 匹配端点 `nodes.name` 或 `type_name` |
| POST | `/api/eav/edges` | 创建边（body 含 `TypeName`/`FromNodeId`/`ToNodeId`/`IsDirected`/`Properties`） |
| GET | `/api/eav/edges/{edgeId}` | 单条 |
| PUT | `/api/eav/edges/{edgeId}` | 仅更新 `IsDirected` |
| DELETE | `/api/eav/edges/{edgeId}` | 显式清理语义，409 拒绝 |
| GET | `/api/eav/edges/from/{nodeId}?typeName=` | 出边；无向实例按双向计入 |
| GET | `/api/eav/edges/to/{nodeId}?typeName=` | 入边；无向实例按双向计入 |
| POST | `/api/eav/edges/traverse` | body `{ TypeName, StartNodeId, Direction, MaxDepth }` |

合并原 `EdgeController` + `EdgeInstancesController` 为单控制器。

## 3. 服务语义（EdgeInstanceService）

- **创建**：
  1. `TypeName` 非空、from/to 非空且不要求不同（自环允许，边模块不再有线路规则）；
  2. 单查 `nodes` 校验两端存在（不存在 → 400 `EavValidationException` 或 ArgumentException）；
  3. 重复判定：同 `TypeName` + 同向 from/to；若新边 `IsDirected=false`，反向亦视为重复 → 409；
  4. 同一 `SaveChanges`：先加 `nodes` 行（`host_type="EdgeInstance"`, `name=null`），再加 `edge_instances` 行；
  5. `Properties` 走 `EavWriteService.SaveAsync(edgeId, TypeName, ...)`；
  6. 发 `EdgeChangedEvent { EdgeTypeName = TypeName }`。
- **更新**：仅 `IsDirected` 可改；改前做反向重复检查。
- **删除**：按顺序拒绝/清理——EAV 属性值（`entity_type=TypeName`）→ 挂载实体 → 被其他边作为端点引用 → 删除类型声明 → 删 edge 行 → 删 nodes 行；单次 `SaveChanges`。
- **DTO 组装**：显示名通过 `InodeOwnerGuardRegistry.ResolveNodeNamesAsync`（nodes 单表）；不再有 `Weight`/`DisplayName`/`EdgeTypeId`/`EdgeTypeDisplayName`。
- **遍历**：有向实例按 from/to；无向实例双向；`both` 恒定双向展开。

## 4. LineSky 归位

- `line_structures` 删除 `edge_type_id` 列、FK 与索引；仅保留 `line_id`/`topology_mode`/`updated_at`。
- `line_structure_segments`：
  - 自存 `line_id`/`edge_id`/`from_node_id`/`to_node_id`/`weight`/`display_name`；
  - **删除到 `edge_instances` 的 FK**；保留 `(line_id, from_node_id)/(line_id, to_node_id)` 复合 FK；
  - 段不再创建 `EdgeInstance`，`EdgeId` 仅作段标识（`Guid.NewGuid()`）。
- `LineStructureService`：移除 `EdgeInstanceService` 依赖与所有 `CreateLineEdgeInternalAsync` / `DeleteInternalAsync` 调用；入度/成环/同线校验全部在本服务内闭环。
- `LineStructureRequest` 删除 `EdgeTypeId`；`LineStructureDto` 删除 `EdgeTypeId/EdgeTypeName/EdgeTypeDisplayName`；`LineTemplateDto` 删除 `DefaultEdgeTypeName`。
- 删除 `LineService` / `LineController` / 相关路径类 DTO 与 `LineApiClient` 对应方法。
- 线路拓扑不进入 NebulaGraph（R4 不发线路图同步事件）。

## 5. 图同步

- `GraphSynchronizer.SyncEdgeAsync`：直接读 `inst.TypeName`，删除 `EdgeDefinitions` 查询。
- `RebuildAllAsync` / `SyncCategoryAsync(Edge)`：直接投影 `edge_instances`，不再 join `edge_definitions`。
- 边属性：`edge_id`、`is_directed`；移除 `weight`/`display_name`。
- `GraphSchemaInitializer`：语义边类型来源改为 `edge_instances.type_name` 去重；建边 schema `(\`edge_id\` string NULL, \`is_directed\` bool NULL)`。
- 顶点解析（`ResolveNodeAsync`）：先单查 `nodes`，按 `host_type` 映射 tag 并只查对应扩展表取附加属性；不再逐一试守卫。
  - 新增 `GraphNaming.NodeTag = "node"` 承载 `host_type="Node"` 的自由节点。
  - `host_type="EdgeInstance"` 暂不入图（避免边作为顶点与超边语义混淆，R4 不做）。

## 6. 前端

- 删除边类型管理页/两个定义对话框；`SpaceListPage` 移除对应选项卡。
- `EdgeApiClient`：删除 5 个类型元数据方法，实例方法改为全局路由。
- `EdgeInstances.razor`：类型筛选改为 `typeName` 文本输入；搜索保留。
- `EdgeRelationPanel.razor`：从已加载边中汇总 `TypeName` 选项；不再加载元数据。
- `EdgeDialog.razor`：类型改为文本输入（可带已有类型 datalist）；新增「有向」开关；移除 Weight/DisplayName 字段；属性仍走 EAV。
- `LineStructureDialog.razor`：移除边类型下拉与 `EdgeTypeId`，模板预填仅保留拓扑模式。

## 7. 迁移 `RebuildEdgeModule`

单迁移，不回填、不兼容：

1. 删除 `line_structure_segments` → `edge_instances` FK；
2. `line_structure_segments` 增加 `weight numeric(18,4) NULL`、`display_name varchar(500) NULL`；
3. 删除 `line_structures` → `edge_definitions` FK、`ix_line_structures_edge_type`，删除 `edge_type_id` 列；
4. 删除 `edge_instances` 的既有 FK/索引与表；删除 `edge_definitions` 表；
5. 清空 edge 相关 EAV（`attribute_values` / `inode_entities` / `inode_entitytypes`，按 edge ids/type names）、删除 `nodes` 中 `host_type='EdgeInstance'` 行；
6. 重建 `edge_instances` 新结构 + 三个索引 + 两条 nodes FK + 共享主键 FK；
7. `TreeGraphDbContext` 删除 `EdgeDefinitions` DbSet、`HasData(EdgeDefinition.SeedData())`、EdgeDefinition 配置。

Down：不可逆（按项目惯例抛 `NotSupportedException` 或尽力恢复表结构；不恢复数据）。

## 8. 验收标准

- `dotnet build` 全解决方案 0 错误；
- 后端行为：
  - 创建边自动注册 nodes；任一端点不存在 → 400；
  - `(type_name, from, to)` 重复 → 409；无向反向重复 → 409；
  - 删除被其他边引用的边 → 409（FK Restrict 兜底）；
  - EAV 属性以 `type_name` 为 `entity_type` 可读写；
  - 无向实例的 from/to 查询对称；
  - 线路结构段不再产生边实例，段端点 FK 仍校验同线；
- 测试：`TreeGraph.Api.Tests`、`TreeGraph.Blazor.Shared.Tests`、`TreeGraph.MafWfApi.Tests`、`TreeGraph.FileStorageApi.Tests`、`TreeGraph.Blazor.Tests` 全绿；重写/新增用例抵消删除用例，保持不低于既有基线。
