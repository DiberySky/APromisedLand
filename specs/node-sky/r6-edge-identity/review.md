# R6 关系身份重构 — 实施回顾

## 结论

关系/边已从独立 `EdgeInstance` 模块收敛为 EAV 的第二种独立值表 `relation`（`relation_attribute_values`，
一条记录 = 一条有向边）。`EdgeSky` 全量下线，`/api/eav/edges` 等端点与 `EdgeChangedEvent` 管道移除，
图投影改为按 `attribute_catalog.relation_type` 动态建边。构建 0 错误，五套测试全绿。

## 主要变更

### 数据模型与迁移
- `EavDataTypes.Relation` 加入 `All`；`AttributeDefinition.RelationType`（`varchar(100)`，可空，创建后不可改）。
- 新增 `RelationAttributeValue`：`ValueId(图边实例 id) / EntityId / EntityType / AttributeId / RelationType / ToNodeId / Name / CreatedAt / UpdatedAt`。
- `TreeGraphDbContext`：`uq_rav_entity_attr (entity_id, entity_type, attribute_id)` 单值语义；
  `uq_rav_type_endpoints (relation_type, entity_id, to_node_id)` 防 Nebula `(src,dst,type,rank=0)` 覆盖；
  `ix_rav_entity`、`ix_rav_to_node`；`attribute_id` 与 `to_node_id` 均 Restrict。
- 删除历史迁移后重新生成单一 `20261024090008_InitialCreate`，并补回
  `CreateAuditPartitionTable` / `DropAuditPartitionTable`。`dotnet ef migrations script` 已确认
  新表、两个唯一键、两个索引、外键与分区表 DDL 均在，且无 `edge_instances`。

### 元数据
- `RelationNaming`：解析优先级「显式 → 用户英文属性名驼峰转 snake → 自动名 400」；
  校验转小写、`^[a-z][a-z0-9_]*$`、≤100、拒绝 `parent_of / line_contains / connects_to / line_segment`。
- 非 relation 填写 `relationType` → 400；relation 的 `isSearchable/isSortable` 强制 false；
  更新请求无该字段（不可改）；`AttributeDetailDto` 与 schema 均透出 `relationType`。
- 前端：属性创建对话框增加「关系」类型与 `relation_type` 输入（snake 命名器）；列表/详情显示图边类型。

### 值读写
- 请求体 `{ toNodeId, name? }`，读取体 `{ valueId, toNodeId, name }`；缺 `toNodeId` → 400。
- `EavWriteService.ApplyRelation`：null → 删除；无旧值 → Insert（`ValueId = Guid`）；有旧值 → 更新；
  端点冲突 → **409「同一图边类型下，该起点已指向该终点」**；写前批量校验目标节点存在（400）。
- 乐观锁 `UpdatedAt` 上界合并三张值表；审计载荷 `{ valueId, toNodeId, name }`。
- 删除实体时同步收敛 relation 行并发布 Delete 事件。

### 删除守卫
- 属性：仍有关系值 → 400（与既有值检查同文案）。
- 节点：自身 EAV 值 → 409；作为起点 → 409；被指向 → 409；全部清空后方可删除。
- 修复：`NodeController.Delete` 使用 `CreateExecutionStrategy()` 包裹事务，
  解决 Npgsql 重试策略不支持用户事务导致的 500。

### 事件与图投影
- `RelationChangedEvent { ValueId, AttributeId, RelationType, FromNodeId, ToNodeId, Name, Kind }`，
  队列 `treegraph.relation-changed`；`SaveChanges` 成功后逐条发布。
- `GraphSchemaInitializer` 按关系属性动态 `CREATE EDGE`，列 `value_id / attribute_id / name`，并补列旧图空间。
- `GraphSynchronizer.SyncRelationAsync`（upsert/delete）；`SyncCategory(Edge)` 与全量重建载荷包含关系边，
  计入 `SemanticEdges`。
- `RelationGraphSyncConsumer` 去重键 `graph-sync-relation:{ValueId}:{del|upsert}`。

### EdgeInstance 下线
- 后端：`EdgeSky` 实体/服务/控制器、`DbSet`、`ConfigureEdges`、`SyncEdgeAsync` 等全部删除。
- 前端：`EdgeSky` 组件/页面/ApiClient、节点关系 Tab、线路节点关系入口删除。
- 代码级 `grep -i edgeinstance|api/eav/edges|EdgeChangedEvent` 清零（历史 Notes/.trae 文档除外）。

## 测试

| 套件 | 结果 |
| --- | --- |
| TreeGraph.Api.Tests | 339 通过（≥330） |
| TreeGraph.Blazor.Shared.Tests | 236 通过 + 1 跳过（≥235+1skip） |
| TreeGraph.MafWfApi.Tests | 65 通过 |
| TreeGraph.FileStorageApi.Tests | 153 通过 |
| TreeGraph.Blazor.Tests | 71 通过 |

新增覆盖：
- `RelationMetadataTests`（18）：显式/回退/拒绝/保留名/不可改/schema。
- `RelationValueTests`（11）：往返、单值覆盖、目标校验、409 冲突、共存、自环、null/省略删除、乐观锁、审计三段。
- `RelationDeleteGuardTests`（5）：属性与节点三类守卫。
- `RelationEventTests`（4）：Insert/Update/Delete/删除实体。
- `RelationSyncTests`（8）：单条 upsert/delete、类别与全量重建、schema 建边/补列、消费者去重。
- `RelationNamingTests`（23）：解析与校验规则。
- `RelationFieldTests`（8）：渲染、只读回显、清空、DynamicForm 往返。

## 说明

- 本地开发库（`localhost:8433`）未运行，未执行实际 drop；迁移按「单 InitialCreate」从零重建，
  开发环境下次启动 `database update` 即得到干净 schema。
- 未提交 git；如需入库请自行提交。
