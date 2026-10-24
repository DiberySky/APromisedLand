# R6 · 关系属性（relation = 边的 EAV 化）Spec

## Why

位置（location）已经证明了一条路：**强结构数据不需要独立模块，做成 EAV 的一种属性类型即可**——属性定义声明类型，值存独立表，读写/校验/审计/前端全部复用既有链路。

边目前是反例：`edge_instances` 自建表、注册 `nodes`（`host_type='EdgeInstance'`）、自带服务/控制器/前端/DTO/事件/守卫，且 `type_name` 是自由文本。结果是**边既不是节点又必须以节点形式存在**，身份、挂载、删除清理都要围着这条假注册行打转。

R6 定稿：**关系（relation）作为 EAV 的第二种独立值表类型，clone location 的整套实现**。

1. 属性定义 `data_type = relation`，新增 `relation_type`（图边类型），属性名自由
2. 值存 `relation_attribute_values`，一条记录 = 一条**有向**边（`entity_id → to_node_id`）
3. 一个关系属性在一个实体上**只有一个值**；需要多条关系时定义多个关系属性
4. 边**不注册** `nodes`；不设 `is_directed`（Nebula 天然有向）；不设 `rank`（恒 0）
5. `edge_instances` 及其服务/控制器/前端整体下线
6. 图内投影：边类型 = `relation_type`，边属性 = `value_id / attribute_id / name`

## What Changes

### 1. 数据模型

`attribute_catalog` 增加一列：

```sql
relation_type varchar(100) null    -- 图边类型；仅 data_type='relation' 时非空
```

- **不唯一**（多个属性可复用同一图边类型，表示同义关系）
- 创建后**不可修改**（与 `entity_type / attribute_name / data_type` 同为不可变字段）
- 非 `relation` 属性的该列必须为 `null`

新表 `relation_attribute_values`：

```sql
create table relation_attribute_values (
  value_id      text primary key default gen_random_uuid()::text,  -- 边实例 id
  entity_id     varchar(36)  not null,                             -- 起点（宿主实体）
  entity_type   varchar(100) not null,
  attribute_id  text         not null references attribute_catalog(attribute_id)
                                 on delete restrict,
  relation_type varchar(100) not null,                             -- 冗余：图边类型
  to_node_id    varchar(36)  not null references nodes(id) on delete restrict,
  name          varchar(200) null,                                 -- 边实例展示名
  created_at    timestamptz  not null default now(),
  updated_at    timestamptz  not null default now(),
  constraint uq_rav_entity_attr     unique (entity_id, entity_type, attribute_id),
  constraint uq_rav_type_endpoints  unique (relation_type, entity_id, to_node_id)
);
create index ix_rav_entity  on relation_attribute_values (entity_id);
create index ix_rav_to_node on relation_attribute_values (to_node_id);
```

两个唯一键各管一件事：

| 唯一键 | 语义 |
|---|---|
| `uq_rav_entity_attr` | 一个关系属性在一个实体上只有一个值（单值语义，与 `uq_lav_entity_attr` 同构） |
| `uq_rav_type_endpoints` | 同一图边类型下，起点不能重复指向同一终点（防 Nebula `(src,dst,type,rank=0)` 覆盖） |

`entity_id` 不设 `nodes` 外键（与 location 一致，宿主存在性由 `EntityOwnerGuardRegistry` 保证）；`to_node_id` 必须外键，目标节点必须是 `nodes` 中的真实节点。

`EavDataTypes` 增加 `Relation = "relation"` 并加入 `All`。

### 2. `relation_type` 的取值规则

创建关系属性时按优先级解析，**入库前必须得到合法值**：

1. 显式提供 → 采用
2. 留空 + `AttributeName` 是用户指定的英文名（非 `attr_` 自动生成）→ 驼峰拆词转 snake_case（`UserDept` → `user_dept`）
3. 留空 + `AttributeName` 是自动生成的 `attr_xxxxxxxxxxxx` → **400**

校验规则（前后端同一套）：

- 存前统一转小写
- `^[a-z][a-z0-9_]*$`（`_` 不能开头，防止与 Nebula 内部名混叠）
- 长度 ≤ 100
- 拒绝内置图边类型：`parent_of`、`line_contains`、`connects_to`、`line_segment`
- 不依赖后端调用 MafWfApi：中文显示名 → snake_case 由前端复用现有 AI 命名建议生成后回填

### 3. 值读写（复用 EAV 统一链路）

- 请求体：`{ "toNodeId": "…", "name": "…" }`
- 读取体：`{ "valueId": "…", "toNodeId": "…", "name": "…" }`
- `valueId` 由服务端生成；`toNodeId` 必填；`name` 可空（≤200）
- 写入前校验 `toNodeId` 存在于 `nodes`，不存在 → 400
- 命中 `uq_rav_type_endpoints` → 409「同一图边类型下，该起点已指向该终点」
- 同一属性已有值 → **覆盖**（改目标/改名），不新增行
- `PUT` 未提供该属性 / 显式 `null` → 删除该关系值
- 允许自环（`entity_id == to_node_id`），不加额外限制
- 审计：写 `AttributeAuditLog`（Insert/Update/Delete），JSON 序列化 `{ valueId, toNodeId, name }`
- 乐观锁：`UpdatedAt` 上界合并 `attribute_values` + `location_attribute_values` + `relation_attribute_values` 三表
- 关系值不进入 `attribute_values`；`is_searchable / is_sortable` 对 relation 无效（元数据层忽略）

### 4. 删除守卫

- **删除属性定义**：`relation_attribute_values` 仍有该 `attribute_id` 的行 → 400，文案与 location 一致
- **删除 node**：`attribute_values` / `location_attribute_values` 有值 → 409；`relation_attribute_values` 作为**起点**有值 → 409；作为**终点被引用**（`to_node_id`）→ 409
- 删除流程保持「同一事务内先检测后删除」，前端删除按钮**常显**，冲突文案用 Snackbar 展示

### 5. 图投影

- `GraphSchemaInitializer`：把原来「以 `edge_instances.type_name` 为准动态建边」改为「以 `attribute_catalog.relation_type`（`data_type='relation' and is_deleted=false`）为准」，建边列 `value_id / attribute_id / name`
- 增量同步：新增 `SyncRelationAsync(RelationChangedEvent evt)`，upsert / delete 均以 `(relation_type, from, to)` 定位图边
- 全量重建：`AddNodeGraphAsync` / bulk 载荷追加关系边
- 事件：新增 `RelationChangedEvent { ValueId, AttributeId, RelationType, FromNodeId, ToNodeId, Name?, Kind }`，由 `EavWriteService` 在 `SaveChanges` 成功后逐条发布；删除 `EdgeChangedEvent`
- 消费者：`RelationGraphSyncConsumer`，去重键 `graph-sync-relation:{ValueId}`

### 6. 下线清单

| 对象 | 处理 |
|---|---|
| `EdgeSky/Entities/EdgeInstance.cs`、`ConfigureEdges`、`DbSet<EdgeInstance>` | 删除 |
| `EdgeSky/Services/EdgeInstanceService.cs`、`EdgeSky/Controllers/EdgeController.cs` | 删除 |
| `TreeGraph.Shared/EdgeSky/**`（DTO、词表） | 删除 |
| `EdgeChangedEvent` / `EdgeChangeKind` | 删除，改 `RelationChangedEvent` |
| `GraphSynchronizer` 的 EdgeInstance 分支、`EdgeProps`、`SyncEdgeAsync` | 删除 |
| `NodesRegistryGuard` 中的 EdgeInstance 分支与注释 | 删除 |
| `TreeGraph.Blazor.Shared/EdgeSky/**`（EdgeDialog、EdgeInstances、EdgeRelationPanel、EdgeApiClient） | 删除 |
| 相关测试（`EdgeTests`、`EdgeHostTests`、图同步边用例） | 删除或改写为 relation 用例 |

**BREAKING**：

- `POST/PUT/DELETE /api/eav/edges` 移除，改走 EAV 属性值端点
- `nodes` 不再出现 `host_type='EdgeInstance'` 行
- 旧边数据不迁移（开发期 drop 库重建，见迁移策略）

### 7. 迁移

延续开发期策略：drop 库 → 删除历史迁移 → 重新生成单一 `InitialCreate` → 补回 `CreateAuditPartitionTable` / `DropAuditPartitionTable` 调用。不写增量迁移。

## Impact

- Affected specs：NodeSky §8.6（Edge 简化）、R4 剩余欠账第 2 项（EdgeInstance 顶点/身份）。
- Affected code：
  - `TreeGraph.Shared/NodeEavSky/EavDataTypes.cs`
  - `TreeGraph.Shared/NodeEavSky/Dtos/MetadataDtos.cs`（请求/详情新增 `RelationType`）
  - `TreeGraph.Shared/NodeEavSky/Dtos/RelationDtos.cs`（新增）
  - `TreeGraph.Api/NodeEavSky/Entities/AttributeDefinition.cs`
  - `TreeGraph.Api/NodeEavSky/Entities/RelationAttributeValue.cs`（新增）
  - `TreeGraph.Api/Data/TreeGraphDbContext.cs`（新增 `ConfigureRelationAttributeValues`，删除 `ConfigureEdges`）
  - `TreeGraph.Api/NodeEavSky/Services/EavWriteService.cs` / `EavReadService.cs` / `EavValidationService.cs` / `EavRequestBodyConverter.cs` / `SubtreeService.cs`
  - `TreeGraph.Api/NodeEavSky/Controllers/EavMetadataController.cs` / `EavController.cs`
  - `TreeGraph.Api/NodeSky/Controllers/NodeController.cs`
  - `TreeGraph.Api/GraphSyncSky/GraphSchemaInitializer.cs` / `GraphSynchronizer.cs` / `GraphSyncConsumers.cs`
  - `TreeGraph.Shared/Messaging/RelationChangedEvent.cs`（新增）
  - `TreeGraph.Blazor.Shared/NodeEavSky/Components/FieldRenderers/RelationField.razor`（新增）、`DynamicForm.razor`
  - `TreeGraph.Blazor.Shared/NodeEavSky/**`（属性定义对话框）
  - `TreeGraph.Api/Data/Migrations/20261024043501_InitialCreate.cs`（重建）
  - `TreeGraph.Api.Tests/**`

## ADDED Requirements

### Requirement: 关系属性定义

`data_type = relation` 的属性 SHALL 拥有一个合法且不可变的 `relation_type`，非 relation 属性 SHALL NOT 有 `relation_type`。

#### Scenario: 显式提供
- **WHEN** 创建属性 `dataType=relation`、`relationType=belongs_to`
- **THEN** 保存成功，`relation_type='belongs_to'`

#### Scenario: 留空回退英文属性名
- **WHEN** 创建属性 `dataType=relation`、`attributeName=UserDept`、`relationType` 为空
- **THEN** `relation_type='user_dept'`

#### Scenario: 自动属性名无法推导
- **WHEN** 创建属性 `dataType=relation`、`attributeName` 为空（自动生成 `attr_…`）、`relationType` 为空
- **THEN** 返回 400，提示必须填写 `relation_type` 或指定英文属性名

#### Scenario: 非法取值
- **WHEN** `relationType` 为 `Connects_To`（大小写混合）、`_x`、含中文、或超长
- **THEN** 分别按「转小写后合法」「`_` 开头拒绝」「非法字符拒绝」「超长拒绝」处理

#### Scenario: 内置边类型
- **WHEN** `relationType` 为 `line_contains`
- **THEN** 返回 400，提示为系统保留

#### Scenario: 修改
- **WHEN** 更新属性定义时试图修改 `relation_type`
- **THEN** 该字段被忽略/拒绝，原值不变

#### Scenario: 类型不匹配
- **WHEN** `dataType=string` 却填写 `relationType`
- **THEN** 返回 400

### Requirement: 关系值单值语义

一个关系属性在一个实体上 SHALL 最多只有一个关系值。

#### Scenario: 首次写入
- **WHEN** 对实体 A 的属性 `belongs_to` 写入 `{ toNodeId: B, name: "主供" }`
- **THEN** `relation_attribute_values` 新增一行，`value_id` 自动生成

#### Scenario: 覆盖
- **WHEN** 同一属性再次写入 `{ toNodeId: C }`
- **THEN** 原行被更新为目标 C，不新增行

#### Scenario: 删除
- **WHEN** `PUT` 省略该属性或传 `null`
- **THEN** 该行被删除，审计记录 Delete

### Requirement: 目标节点校验

`toNodeId` SHALL 必填且必须是 `nodes` 中存在的节点。

#### Scenario: 目标不存在
- **WHEN** `toNodeId` 指向不存在的 id
- **THEN** 返回 400，不落库

#### Scenario: 目标为边
- **WHEN** 因边不再注册 `nodes`，`toNodeId` 传入旧边 id
- **THEN** 返回 400

### Requirement: 图边不覆盖

同一 `relation_type` 下，同一 `(entity_id, to_node_id)` SHALL 最多一条关系值。

#### Scenario: 端点冲突
- **WHEN** 属性 P1（`relation_type=belongs_to`）已写入 A→B，属性 P2（同为 `belongs_to`）也写 A→B
- **THEN** 返回 409「同一图边类型下，该起点已指向该终点」

#### Scenario: 同类型不同端点
- **WHEN** P1 写 A→B，P2（同类型）写 A→C
- **THEN** 两者均成功，图内为两条独立边

### Requirement: 删除守卫

仍有 EAV 数据的实体 SHALL NOT 被删除。

#### Scenario: 属性有值
- **WHEN** 删除属性定义，但其关系值仍存在
- **THEN** 返回 400「该属性已产生实例数据，无法删除」

#### Scenario: 节点有属性值
- **WHEN** 删除 node，其 `attribute_values` 或 `location_attribute_values` 有值
- **THEN** 返回 409，提示先清空属性值

#### Scenario: 节点被关系引用
- **WHEN** 删除 node，其仍作为某关系值的 `to_node_id`
- **THEN** 返回 409，提示先删除相关关系

### Requirement: 图投影

关系值 SHALL 投影为 NebulaGraph 的有向边。

#### Scenario: 动态建边类型
- **WHEN** 存在 `relation_type=belongs_to` 的活动属性
- **THEN** 图空间内存在边类型 `belongs_to`，列 `value_id/attribute_id/name`

#### Scenario: 增量写入
- **WHEN** 关系值写入成功
- **THEN** 发布 `RelationChangedEvent`，消费端 upsert `belongs_to` 边，属性含 `value_id/attribute_id/name`

#### Scenario: 删除
- **WHEN** 关系值被删除
- **THEN** 消费端按 `(relation_type, from, to)` 删除图边

#### Scenario: 全量重建
- **WHEN** 执行 `RebuildAllAsync`
- **THEN** bulk 载荷包含全部关系边，统计计入 `SemanticEdges`

### Requirement: EdgeInstance 下线

边 SHALL 不再拥有独立表、独立服务与独立端点。

#### Scenario: 端点移除
- **WHEN** 访问 `POST /api/eav/edges`
- **THEN** 返回 404

#### Scenario: 不再注册节点
- **WHEN** 创建关系值
- **THEN** `nodes` 行数不变

## Out of Scope

- 关系值自身的 EAV 属性（边属性）
- 无向边
- 同一属性指向多个目标（需要则定义多个关系属性）
- `rank > 0` 的平行同类型边
- Line / 树类型宿主作为关系起点或终点（当前仅支持 `nodes` 内的宿主）
- 关系值的历史版本与软删除
