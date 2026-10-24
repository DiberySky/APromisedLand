# R7 LineNode 收敛进 nodes / 线路结构收敛为 relation 边 — 实施回顾

## 结论

LineNode 已从专表收敛进 `nodes` 注册表（`host_type='LineNode'`、`line_id` → Line），线路结构
（原 `LineStructure`/`LineStructureSegment` 专表 + `TopologyMode`）整体收敛为 EAV relation 值：
种子实体类型 `LineStructure` + 种子属性 `next`（`relation_type='line_segment'`），一条段边 =
一条 `relation_attribute_values` 记录，走 R6 relation 写入链与图投影管道。
`LineStructureChangedEvent` 专用事件与消费者、`line_contains` 边类型、段 `Weight` 全部下线。
构建 0 错误，五套测试全绿（Api 366 ≥ 330）。

## 主要变更

### 数据模型与迁移
- `Node` 新增 `LineId`（`varchar(36)`，可空，FK → `nodes(id)` Restrict；身份锚点：LineNode 挂到所属
  Line，通用 Node/树节点恒 null；目标行须 `host_type='Line'` 由服务层校验）。
- 删除实体 `LineNode` / `LineStructure` / `LineStructureSegment` 及其 DbSet/Configure/备用键；
  删除 `TopologyMode` 枚举，`TerminalRole` / `TerminalOperatingState` 挪入 `LineTerminal.cs`。
- `line_terminals.node_id` FK 改指 `nodes(id)` Restrict（原指 line_nodes）。
- drop 库 → 删历史迁移 → 重建单一 `20261024130457_InitialCreate`，补回
  `CreateAuditPartitionTable` / `DropAuditPartitionTable`；`dotnet ef migrations script` 核对通过。

### 种子与元数据
- seed `entity_type_catalog`：`LineStructure` / 「线路结构」（固定 id，内置保护拒改拒删，409 文案）。
- seed `attribute_catalog`：`next` / 「下一段」，`data_type='relation'`、`relation_type='line_segment'`。
- `RelationNaming.ReservedTypes`：保留 `line_segment`，移除 `line_contains`（概念废弃）；无 `belongs_to_line`。

### Line 注册 nodes
- `LinesController.Create` 同事务双写 `lines` + `nodes`（`host_type='Line'`）；
  `Delete` 守卫（仍有 LineNode → 409）通过后双删；`GraphSynchronizer` `case "Line"` 顶点链路修复。

### LineNode Facade（保 DTO 形状）
- GET `api/LineNode?lineId=&search=` 走 `nodes where host_type='LineNode' and line_id=…`，DTO 形状不变。
- POST 事务：nodes 行 → 声明 LineStructure 实体类型 → 有前驱则写 `line_segment` 值（前驱→自身，
  走 EAV 写入链）→ 发 `InodeChangedEvent` + `RelationChangedEvent`。
- PUT：改 name/description；改连接 = 改段边值；换线路 = 改 `line_id`（连带校验段边两端仍同线，否则 400）。
- DELETE 守卫链：段边引用 → 端子引用 → 关系终点被指向 → 自身 EAV 值 → 挂载实体 → 被作为 parent 引用
  （均 409）→ 清声明 → 删 nodes 行。
- 段边两端必须同线校验（服务层，替代原复合外键）；禁自环复用 R6。

### LineStructure 边集薄层
- 节点集 = `line_id=lineId`；边集 = 两端均属本线的 relation 值；加/删边 = 写/删 relation 值。
- 链序推导沿 `line_segment` 链走（无入边者为起点；分叉/环按发现序）。
- 拓扑模式校验整体删除；只保留禁自环、同线校验、诊断（连通/成环/度数，只报告不阻断）。
- 段 `Weight` 砍掉；段 DisplayName → `relation.name`；PUT 全量替换语义（Terminals 传 null = 清空）。

### 事件与图投影
- 删除 `LineStructureChangedEvent` / `LineStructureGraphSyncConsumer` / `SyncLineStructureAsync` /
  全量重建段投影；段边变更经 EAV 写入链发布 `RelationChangedEvent`（Insert/Update/Delete）。
- `SyncParentEdgeAsync` `case "LineNode"`：`ParentId = nodes.line_id`（Line→LineNode 自动对齐）。
- `line_contains` 边类型整体下线：schema 不再创建，删 `SyncLineContainsAsync` 与对齐段。
- `line_segment` 边类型由 seed 属性 `relation_type` 经 R6 动态建边自动创建
  （列 `value_id/attribute_id/name`），全量重建统计计入 `SemanticEdges`。

### 向量与语义搜索
- `VectorSyncService` / `SourceTextBuilder` / `SearchService` 的 `_db.LineNodes` 特判改
  `nodes where host_type='LineNode'`；祖先链 = parent 上溯（「所属线路」标签保留）。

### 前端
- `LineNodeDto` / ApiClient / 节点 4 个 Dialog 形状不变；创建 Dialog 增加「前驱」下拉
  （首节点 = 无，空串归一化为 null）。
- `LineStructureDialog`：段列表 = 段边列表（起点/终点/类型/显示名列）；「加段」= 选 from/to 同线节点；
  拓扑模式选择器删除；模板只预填端子默认值。

### 关键修复（实施期发现）
- `LineNodeController.Create` 与 `LineStructureService.ReplaceAsync` 的裸事务包进
  `CreateExecutionStrategy().ExecuteAsync`，解决 NpgsqlRetryingExecutionStrategy 不支持用户事务的 500
  （沿用 R6 `NodeController.Delete` / `EavWriteService` 既有模式）。

## 测试

| 套件 | 结果 |
| --- | --- |
| TreeGraph.Api.Tests | 366 通过（≥330） |
| TreeGraph.Blazor.Shared.Tests | 238 通过 + 1 跳过 |
| TreeGraph.MafWfApi.Tests | 65 通过 |
| TreeGraph.FileStorageApi.Tests | 153 通过 |
| TreeGraph.Blazor.Tests | 71 通过 |

新增/改写覆盖：
- `LineNodeCrudTests`：CRUD 往返、400/409 守卫、创建即建边（首节点无边/次节点有边）、
  换线路连带校验、前驱重接（next 单值语义：同 from 覆盖旧边）。
- `LineStructureTests` / `LineStructureEventTests`：边集薄层、链序推导、诊断、
  结构变更经 R6 管道发布 `RelationChangedEvent`（Insert/Delete）。
- `LineStructureSyncTests`：parent_of（Line→LineNode）对齐、`line_segment` 经 R6 管道建边、
  `line_contains` 负向断言（任何同步请求均不再出现）。
- `LineRegistrationTests`：Line 创建双写 / 删除守卫；`DeletePrincipleTests`：LineNode 删除守卫链。
- 元数据：内置种子拒改拒删；`line_contains` 移出保留名（删两处过时 InlineData）。
- 前端 `LineStructureDialogSmokeTests`（重写 8 个）+ `LineNodeCreateDialogSmokeTests`（新增 3 个）：
  诊断 chips、段表边列、模板预填端子默认、前驱选择与 null 归一化、R7 契约序列化形状。
- 删除 `TreeGraph.Blazor.Tests` 下重复且失效的旧版 LineStructureDialog 测试
  （MudDialog 脱离 Provider 直接 Render 产出空 markup；Shared 侧 provider 模式版本全覆盖）。

## 说明

- 全仓 grep 清零：`LineStructureChangedEvent` / `line_contains` / `TopologyMode` / `line_nodes`
  在生产代码与非测试代码中均为 0。例外（功能性负向断言，验证「不再出现」）：
  `LineStructureSyncTests.cs` 5 行 `line_contains` 负向断言及就近注释（9.3 要求）、
  `LineStructureDialogSmokeTests.cs` 1 行 `DoesNotContain("topologyMode")` 序列化负向断言。
- EAV 写入链要求 entityId 为 GUID：测试线路节点 id 一律 `$"{Guid.NewGuid()}"`；
  线路 id 保留 `L_` 前缀（lines 不经 EAV 路径）。
- 本地开发库（`localhost:8433`）未运行，未执行实际 drop；迁移按「单 InitialCreate」从零重建，
  开发环境下次启动 `database update` 即得到干净 schema。
- 未提交 git；如需入库请自行提交。
