# R7 · LineNode 收敛进 nodes、线路结构收敛为 relation 边（定稿 v4）

> 状态：**已定稿**——决策点 ①~⑨ 全部经用户拍板（含「三点都行」与列命名 parent_id ⇒ **LineId**）。
> 破坏性重构：drop 库 → 删历史迁移 → 重建单一 `InitialCreate`（同 R6 策略）。
> 流程：spec 定稿 → 出 tasks.md → 用户批准 → 实施。

## Why

R2/R3 之后 `nodes` 是身份唯一真源；R6 之后「有向关系」有统一的 EAV 值表与图投影管道。LineSky 仍留两套冗余：

- `line_nodes` 表有效载荷只剩 `line_id` + `description`（nodes 已有同列）；
- `line_structure_segments` 自存 from/to 的有向段——与 relation 值表「一条记录 = 一条有向边」同构，却另起存储/事件/图投影；
- `TopologyMode`（Linear/Radial/Network）是校验期概念，用户定稿：**无必要区分，结构由边连接定义**；
- Line 不注册 `nodes`，身份链路残缺。

R7 定稿方向（用户拍板的概念切分）：

1. **LineNode => node**：删 `line_nodes` 表；线路节点 = `nodes` 中 `host_type='LineNode'` 注册行；
2. **Line 是 LineNode 的 parent（身份锚点，不是边）**：`nodes` 增加 `line_id`；图内复用 `parent_of` 边（Line → LineNode）；
3. **`line_segment` 纯化**：只表达 LineNode ↔ LineNode 的结构连接，存储 = `relation_attribute_values`（一条记录 = 一条边）；
4. 创建节点：首节点只设 parent（无边）；后续节点 = parent=Line + 一条 `line_segment`（前驱 → 自身）；
5. 删 `line_structures` / `line_structure_segments` / `TopologyMode`；创建后可再定义新 relation 属性表达分支/跨接。

## 目标形态

```
nodes  host_type='Line'      → 线路身份（补注册；线路 = 名称定义 + parent 锚点）
nodes  host_type='LineNode'  → 线路节点身份（名称/描述/时间戳 + line_id → Line）
lines                         → 线路主表（不变：Name/Description）
relation_attribute_values     → line_segment 结构边（仅 LineNode ↔ LineNode）
line_terminals                → 保留（端子角色/投运态/外部连接；node_id FK 改指 nodes）
```

## What Changes

### 1. 数据模型

**`nodes` 增加一列**：

```sql
line_id varchar(36) null references nodes(id) on delete restrict
```

- **LineNode 必填且恒 = 所属 Line 的 id**（`nodes.line_id` 即原 LineNode.LineId 的归宿）；目标行必须 `host_type='Line'`，服务层校验；
- **列命名定稿**：DB 列 `line_id` / C# 属性 `Node.LineId`——用户拍板「parent_id ⇒ LineId」，parent 锚点落地命名为 LineId，不用通用 `parent_id`（避免与树节点的 ParentId 混淆）；
- Node 实体注释更新：line_id 是**身份锚点**（线路节点挂到线路）；通用 Node/树节点恒 null（树的 ParentId 仍在各自宿主表）；
- 「线路的全部节点」= `nodes where line_id = lineId`（一条 SQL，无需遍历）。

**删除**：`line_nodes`、`line_structures`、`line_structure_segments` 三表及实体、`TopologyMode` 枚举、`ak_line_nodes_line_id_id` 备用键。

**`lines`**：补注册 nodes——Create 同事务写 `nodes` 行（HostType="Line"），Delete 守卫通过后双删。

**`line_terminals`**：`node_id` FK 改指 `nodes(id)` Restrict；其余不变。

### 2. 种子：线路结构实体类型 + 链边属性

seed（`entity_type_catalog` + `attribute_catalog`，固定 id，内置保护拒改拒删）：

| 种子 | 字段 | 值 |
|---|---|---|
| 实体类型 | entity_type / display_name | `LineStructure` / 「线路结构」（已定） |
| 链边属性 | attribute_name / display_name | `next` / 「下一段」（已定） |
| | data_type / relation_type | `relation` / `line_segment` |

- `line_segment` 保留在 `RelationNaming.ReservedTypes`；`line_contains` 从保留名移除（概念废弃）；
- 单值语义按 `(entity, attribute)` 计：一个节点对 `next` 属性最多一条出边（一个后继）；分支/跨接 = 用户再定义新 relation 属性（多出边天然支持）；
- `uq_rav_type_endpoints` 防同类型同端点重复。

### 3. 创建链路（核心交互语义）

```
创建 Line L            → nodes(host_type='Line') + lines 行
创建 N1（在 L 下）      → nodes(host_type='LineNode', line_id=L)
                          + 声明 LineStructure 实体类型（inode_entitytype）
                          （首节点无 line_segment 边）
创建 N2（前驱=N1）      → nodes(line_id=L) + 声明
                          + relation 值：entity_id=N1, to=N2（line_segment）
创建 N3（前驱=N2）      → …依此类推
```

- parent 与段边都由标准链路落库；段边经 EAV 写入链（校验/审计/`RelationChangedEvent` 复用）；
- 「属于线路 L」= `line_id = L`；段边两端必须同 parent（服务层校验，替代原复合外键）；
- 换前驱/改连接 = 改 relation 值；换线路 = 改 line_id（连带校验其段边两端仍同线，否则拒绝）。

### 4. API 形态

**`api/LineNode`（Facade，保 DTO 形状）**：

| 端点 | 行为 |
|---|---|
| GET `?lineId=&search=` | `nodes where host_type='LineNode' and line_id=lineId`；search 过滤 name |
| POST | 事务：nodes 行（line_id=lineId）→ 实体类型声明 → 有前驱则写 line_segment 值 → 事件 |
| PUT `{id}` | 改 name/description；改连接 = 改段边值 |
| DELETE `{id}` | 守卫链：段边引用（from 或 to → 409）→ 端子引用（409）→ 关系终点被指向（409，R6 已有）→ 自身 EAV 值（409）→ 挂载实体（409）→ 被作为 parent 引用（409，FK 兜底+预检）→ 清声明 → 删 nodes 行 |

**`api/LineStructure`**：重写为「按线路读边集 + 加/删边」薄层——节点集 = `line_id=lineId`；边集 = 两端均属本线的 relation 值；顺序推导 = 沿 `line_segment` 链走（无入边者为起点；分叉/环按发现序）；拓扑模式校验整体删除，只保留：禁自环、同线校验、诊断（连通/成环/度数，只报告不阻断）。

**`NodeController.Delete`** 补守卫：作为段边端点 / 端子 / 被作为 parent 引用 → 409。

### 5. 事件与图投影

- `LineStructureChangedEvent` / `LineStructureGraphSyncConsumer` / `SyncLineStructureAsync` / 全量重建段投影：**整体删除**——段边即 relation 值，走 R6 管道；
- `parent_of`：Line→LineNode 由既有 `SyncParentEdgeAsync` 自动对齐（`case "LineNode"` 填 `ParentId = nodes.line_id`）——零新增；
- `line_contains` 边类型下线（schema 不再创建；`SyncLineNodeLinksAsync` 的对齐段、`SyncLineContainsAsync` 删除）；
- `line_segment` 边类型由 seed 属性 `relation_type` 经 R6 动态建边自动创建（列 `value_id/attribute_id/name`）；
- `case "Line"`：nodes 行真实存在后顶点链路修复；
- `connects_to` 不变；全量重建统计中 line_segment 计入 SemanticEdges。

### 6. 向量与语义搜索

- 三件套（`VectorSyncService` / `SourceTextBuilder` / `SearchService`）的 `_db.LineNodes` 特判改 `nodes where host_type='LineNode'`；
- SourceTextBuilder 祖先链 = parent（「所属线路」标签保留），与树节点同一套 parent 上溯形态。

### 7. 前端

- `LineNodeDto` / ApiClient / 节点 4 个 Dialog：形状不变（Facade 承接）；创建 Dialog 增加「前驱」选择（首节点 = 无）；
- `LineStructureDialog`：段列表 = 段边列表；「加段」= 选 from/to（同线节点）；拓扑模式选择器删除。

### 8. 迁移策略（同 R6）

drop 库 → 删历史迁移 → 重建 `InitialCreate` → 补回审计分区 DDL → `migrations script` 核对：`nodes.line_id` 在、无三表、seed 含实体类型与链边属性、`line_terminals.node_id` FK→nodes。

## 决策点（确认状态）

| # | 问题 | 结论 |
|---|---|---|
| ① | 归属存储 | **已定：nodes.line_id（Line 是 parent，非边）** |
| ② | 拓扑模式 | **已定：下线，结构由边连接定义** |
| ③ | 结构边存储 | **已定：relation 值表；line_segment 仅 LineNode↔LineNode** |
| ④ | 首节点 | **已定：只设 parent，无边；次节点起 from=前驱** |
| ⑤ | 分支/环 | **已定：单值按属性计，新边类型 = 新 relation 属性** |
| ⑥ | 种子命名 | **已定：`LineStructure`「线路结构」+ `next`「下一段」** |
| ⑦ | 段 Weight | **已定：砍掉（DisplayName→relation.name）** |
| ⑧ | 端子校验 | **已定：只留「投运 FeedIn ≤ 1」** |
| ⑨ | 列命名 | **已定：parent_id ⇒ `line_id` / `Node.LineId`** |

## 影响面清单（实施时逐项核对）

- 删：`LineNode.cs`、`LineStructure.cs`、`LineStructureSegment.cs`、`LineStructureTypes.cs`、`LineStructureChangedEvent`、line_contains 相关、`LineStructureGraphSyncConsumer`
- 改：`Node.cs`（line_id）、`TreeGraphDbContext`、`LinesController`、`LineNodeController`（Facade 重写）、`LineStructureController/Service`（边集薄层）、`NodeController.Delete`（补守卫）、种子、`RelationNaming`、GraphSync 三件套、向量/搜索三件套
- 测试：LineNode/LineStructure*/VectorLineScope/DeletePrinciple 改写；新增：parent 设定与守卫、创建即建边、链序推导、多边类型分支、内置种子保护
- 验证：`dotnet build` 0 错误；五套测试全绿（Api ≥330）

## 明确不做

- 不做数据迁移（drop 重建）；
- 不动 `lines` 表结构与 Line 的 Inode 身份（只补 nodes 注册）；
- 不恢复拓扑模式；诊断只报告不阻断；
- 不动 `connects_to` 与端子外部连接语义；
- 通用 Node 与树节点不使用 `line_id`（本期仅 LineNode）。
