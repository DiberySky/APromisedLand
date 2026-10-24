# R4 · Edge 简化重构（review）

> 对照 `specs/node-sky/spec.md` §8.6（2026-10-24 定稿）逐项评审。
> 结论：**通过**。全解决方案构建 0 错误，五套测试全绿且不低于基线。未 commit / push。

## 1. 终态结构核对

`edge_instances` 现仅保存「两个节点 + 有向/无向 + 类型名」：

| 列 | 类型/约束 | 状态 |
| --- | --- | --- |
| `edge_id` | PK，同时为 `nodes.id` 共享主键（host_type=`EdgeInstance`，name=NULL） | ✅ |
| `from_node_id` | FK → `nodes.id`，`Restrict`，`ix_ei_from_node` | ✅ |
| `to_node_id` | FK → `nodes.id`，`Restrict`，`ix_ei_to_node` | ✅ |
| `type_name` | 裸列 `varchar(200)`，唯一索引 `uq_edge_instance (type_name, from_node_id, to_node_id)` | ✅ |
| `is_directed` | `bool`，默认 `true` | ✅ |
| `created_at` / `updated_at` | 时间戳 | ✅ |

旧的 `weight` / `display_name` / `edge_type_id` / `display_name` 列、`edge_definitions` 表、`edge_type` 三件套均已移除。业务属性一律走 EAV：`entity_type = type_name`、`entity_id = edge_id`。

## 2. 删除清单核对

- `EdgeDefinition.cs`（含旧 `EdgeInstance` + 18 条种子）— 已删。
- `EdgeInstancesController` / `EdgeMetadataController` / `EdgeMetadataService` / `EdgeNodeValidator` — 已删。
- `LineService` / `LineController` / `LineDtos`（含 `LinePathResult`/`LineValidationResult`/`LineConnectedComponent`）— 已删。
- 前端 `EdgeMetadata.razor`、`EdgeDefinitionCreateDialog.razor`、`EdgeDefinitionEditDialog.razor` — 已删；`SpaceListPage` 的「关系设置」入口 — 已移除。
- 边类型元数据 API 全部移除后，`EavMetadataController` 的实体类型存在性检查收敛到 `entity_type_catalog`。

## 3. API 路由（全局、无类型段）

| 方法 | 路由 | 语义 |
| --- | --- | --- |
| GET | `/api/eav/edges?typeName=&nodeIds=&search=` | 全局列表；`search` 匹配 `type_name` 或端点 `nodes.name` |
| POST | `/api/eav/edges` | 创建；两端节点必须已在 `nodes` 注册，否则 400 |
| GET | `/api/eav/edges/{edgeId}` | 详情（含 EAV 属性） |
| PUT | `/api/eav/edges/{edgeId}` | 仅改 `IsDirected`；改为无向前检查反向重复 |
| DELETE | `/api/eav/edges/{edgeId}` | 守卫式删除（见 §4） |
| GET | `/api/eav/edges/from/{nodeId}?typeName=` | 出边（无向边双向计入） |
| GET | `/api/eav/edges/to/{nodeId}?typeName=` | 入边（无向边双向计入） |
| POST | `/api/eav/edges/traverse` | BFS，`out`/`in`/`both` |

## 4. 服务语义核对

- **创建注册**：同一 `SaveChanges` 内先 `nodes.Add(host_type="EdgeInstance", name=null)` 再 `edge_instances.Add`，共享主键成立。
- **重复判定**：同向重复 409；无向边反向重复 409（有向反向允许共存）。
- **删除守卫**（全部 409，不级联）：自身属性值 / 位置属性值存在、`inode_entity` 存在、被其他边作为端点引用。仅声明（`inode_entitytype`）可删且随删。
- **EAV 类型**：`type_name` 未登记进 `entity_type_catalog` 时仍可建边；需要属性时再登记实体类型与属性定义。
- **DTO 名称**：端点名称经 `InodeOwnerGuardRegistry.ResolveNodeNamesAsync` 解析；测试环境移除守卫时为空串，不影响 ID 语义。

## 5. LineSky 解耦

- `line_structure_segments` 自存 `line_id/edge_id/from_node_id/to_node_id/weight/display_name`，`edge_id` 仅作段标识（Guid），**不再有到 `edge_instances` 的外键**。
- `line_structures.edge_type_id` 列/FK/索引已移除；`LineStructureRequest`/`LineStructureDto`/`LineTemplateDto` 同步删除 EdgeType 字段。
- 建结构/加段/删段/删结构均不创建、不联动任何通用边；线路拓扑校验（Linear/Radial/Network、端子、游离节点）保持闭环。
- 线路模板只预填拓扑模式与端子默认，不再预填边类型。

## 6. 图同步

- 语义边类型来源改为 `edge_instances.type_name` 去重；边 schema 属性为 `edge_id` + `is_directed`；`SyncEdgeAsync` 直接用 `TypeName`。
- `ResolveNodeAsync` 改为 `nodes` 单查 + `host_type` 分派；树顶点名称统一从 `nodes.name` 读取，修掉宿主 `Name` 已 `NotMapped` 的潜在运行时问题；`EdgeInstance` 宿主不作为图顶点解析。
- `GraphNaming` 新增 `NodeTag = "node"`（`name/node_type/is_archived`）；全量重建补充 `host_type="Node"` 顶点。

## 7. 迁移风险

迁移 `20261024031103_RebuildEdgeModule` 为**不可逆、破坏性**：

1. 先删除 `line_structure_segments → edge_instances`、`line_structures → edge_definitions`、`edge_instances → edge_definitions` 三条旧 FK。
2. 删除旧边数据（含 EAV 值、`inode_entitytype`/`inode_entity`、属性目录、`entity_type_catalog`、`edge_instances`、`nodes` 中 `host_type='EdgeInstance'` 行）。
3. 删旧表/旧列/旧索引，再新增 `line_structure_segments.display_name/weight`、`edge_instances.is_directed/type_name`，最后建唯一索引与两条 nodes FK。

风险点：
- 清库 SQL 依赖旧列名（`edge_definitions.type_name`），Down 不恢复数据（已在迁移注释中声明）。
- 升级库前需确认无外部系统按旧 `edge_type_id` 读 `edge_instances`；本次明确不做兼容层。
- 旧线路结构段的 `weight/display_name` 无法从边回填，升级后为空（业务可后续在段上补录）。

## 8. 测试与验证

| 套件 | 基线 | 实测 | 结果 |
| --- | --- | --- | --- |
| TreeGraph.Api.Tests | 322 | 322 通过 | ✅ |
| TreeGraph.Blazor.Shared.Tests | 235 + 1 skip | 236 通过 + 1 skip | ✅ |
| TreeGraph.MafWfApi.Tests | 65 | 65 通过 | ✅ |
| TreeGraph.FileStorageApi.Tests | 153 | 153 通过 | ✅ |
| TreeGraph.Blazor.Tests | 71 | 71 通过 | ✅ |

- 全解决方案 `dotnet build APromisedLand.sln`：0 错误。
- 测试改动：重写 `EdgeTests`（新全局路由 + nodes FK 400 + 有向/无向重复 409 + 引用删除 409 + EAV `type_name` + 无向对称查询 + 遍历）、`EdgeHostTests`（宿主声明/挂载/删除守卫）、`DeletePrincipleTests` 边段、`LineStructureTests`（去 EdgeTypeId/IsLine，新增段自存与解耦用例）、`SourceTextBuilderTests` 边用例；删除 `EdgeNodeGuardTests`/`EdgeNodeValidatorTests` 及对应 fixture；Blazor 两个 smoke test 适配 `PreFillTypeName` 与无 EdgeType 表单。

## 9. 剩余欠账（不在 R4 范围）

1. **线路入图**：Line/LineNode 的图顶点与线路拓扑入 NebulaGraph 仍按旧线路模块独立处理，尚未与边简化后的 `edge_instances` 反向对齐。
2. **EdgeInstance 顶点**：边作为 `nodes` 注册行，目前 `ResolveNodeAsync` 显式返回 null（不建边顶点）；是否需要在图中表达「边即顶点」待定。
3. **EAV 关系类型目录重建**：`entity_type_catalog` 中旧的边类型行已随迁移清空；若后续需要「关系类型」元数据（展示名、分组、约束），应在 EAV 元数据层重新建模，而不是回到 `edge_definitions`。
