# R7 Tasks

> 依据 [spec.md](spec.md)（定稿 v4）。破坏性重构：drop 库 → 删历史迁移 → 重建单一 `InitialCreate`（同 R6）。

- [x] Task 1: 数据模型与迁移
  - [x] 1.1 `TreeGraph.Shared/NodeSky/Entities/Node.cs`：新增 `LineId`（`string?`）+ 注释「身份锚点：LineNode 挂到所属 Line；通用 Node/树节点恒 null」
  - [x] 1.2 `TreeGraphDbContext`：`ConfigureNodes` 映射 `line_id varchar(36) null`，FK → `nodes(id)` Restrict；目标行须 `host_type='Line'`（服务层校验，DB 不约束）
  - [x] 1.3 删除实体：`LineSky/Entities/LineNode.cs`、`LineStructure.cs`、`LineStructureSegment.cs`；`TreeGraphDbContext` 移除对应 DbSet 与 Configure、`ak_line_nodes_line_id_id` 备用键
  - [x] 1.4 `LineSky/Entities/LineStructureTypes.cs`：删除 `TopologyMode`；`TerminalRole` / `TerminalOperatingState` 挪入 `LineTerminal.cs`（或独立 `LineTerminalTypes.cs`），同步 using
  - [x] 1.5 `line_terminals.node_id` FK 改指 `nodes(id)` Restrict（原指 line_nodes）
  - [x] 1.6 drop 库 → 删历史迁移 → 重建单一 `InitialCreate` → 补回 `CreateAuditPartitionTable` / `DropAuditPartitionTable`
  - [x] 1.7 `dotnet ef migrations script` 核对：`nodes.line_id` 在、无 line_nodes/line_structures/line_structure_segments 三表、`line_terminals.node_id` FK→nodes、分区表 DDL 在

- [x] Task 2: 种子与元数据
  - [x] 2.1 seed `entity_type_catalog`：`LineStructure` / 「线路结构」（固定 id，内置保护拒改拒删）
  - [x] 2.2 seed `attribute_catalog`：`next` / 「下一段」，`data_type='relation'`、`relation_type='line_segment'`（固定 id，内置保护拒改拒删）
  - [x] 2.3 `RelationNaming.ReservedTypes`：保留 `line_segment`，移除 `line_contains`（概念废弃）；确认无 `belongs_to_line`
  - [x] 2.4 内置种子的改/删保护在元数据 Controller 生效（拒改拒删，409 文案）

- [x] Task 3: Line 注册 nodes
  - [x] 3.1 `LinesController.Create`：同事务写 `lines` 行 + `nodes` 行（`host_type='Line'`）
  - [x] 3.2 `LinesController.Delete`：守卫（仍有 LineNode：`nodes.AnyAsync(n => n.LineId == id)` → 409）通过后双删
  - [x] 3.3 `GraphSynchronizer` `case "Line"`：nodes 行真实存在，顶点链路修复

- [x] Task 4: LineNode Facade（保 DTO 形状）
  - [x] 4.1 GET `api/LineNode?lineId=&search=`：`nodes where host_type='LineNode' and line_id=lineId`，search 过滤 name；DTO 形状不变
  - [x] 4.2 POST：事务——nodes 行（`host_type='LineNode'`, `line_id=lineId`，校验目标为 Line）→ 声明 LineStructure 实体类型 → 有前驱则写 `line_segment` 值（前驱→自身，走 EAV 写入链）→ 事件
  - [x] 4.3 PUT `{id}`：改 name/description；改连接 = 改段边值；换线路 = 改 `line_id`（连带校验段边两端仍同线，否则 400）
  - [x] 4.4 DELETE `{id}` 守卫链：段边引用（from 或 to → 409）→ 端子引用（409）→ 关系终点被指向（409，R6 已有）→ 自身 EAV 值（409）→ 挂载实体（409）→ 被作为 parent 引用（409）→ 清声明 → 删 nodes 行
  - [x] 4.5 段边两端必须同线校验（服务层，替代原复合外键）；禁自环复用 R6

- [x] Task 5: LineStructure 边集薄层
  - [x] 5.1 `LineStructureController/Service` 重写：节点集 = `line_id=lineId`；边集 = 两端均属本线的 relation 值；加/删边 = 写/删 relation 值
  - [x] 5.2 链序推导：沿 `line_segment` 链走（无入边者为起点；分叉/环按发现序）
  - [x] 5.3 拓扑模式校验整体删除；只保留：禁自环、同线校验、诊断（连通/成环/度数，只报告不阻断）
  - [x] 5.4 段 Weight 砍掉：段 DisplayName → relation.name

- [x] Task 6: 事件与图投影
  - [x] 6.1 删除 `LineStructureChangedEvent` / `LineStructureGraphSyncConsumer` / `SyncLineStructureAsync` / 全量重建段投影（段边走 R6 relation 管道）
  - [x] 6.2 `SyncParentEdgeAsync` `case "LineNode"`：`ParentId = nodes.line_id`（Line→LineNode 自动对齐）
  - [x] 6.3 `line_contains` 边类型下线：schema 不再创建；删 `SyncLineContainsAsync` 与 `SyncLineNodeLinksAsync` 的对齐段
  - [x] 6.4 确认 `line_segment` 边类型由 seed 属性 `relation_type` 经 R6 动态建边自动创建（列 `value_id/attribute_id/name`）；`connects_to` 不变；全量重建统计计入 SemanticEdges

- [x] Task 7: 向量与语义搜索
  - [x] 7.1 `VectorSyncService` / `SourceTextBuilder` / `SearchService` 的 `_db.LineNodes` 特判改 `nodes where host_type='LineNode'`
  - [x] 7.2 `SourceTextBuilder` 祖先链 = parent 上溯（「所属线路」标签保留）

- [x] Task 8: 前端
  - [x] 8.1 `LineNodeDto` / ApiClient / 节点 4 个 Dialog 形状不变；创建 Dialog 增加「前驱」选择（首节点 = 无）
  - [x] 8.2 `LineStructureDialog`：段列表 = 段边列表；「加段」= 选 from/to（同线节点）；拓扑模式选择器删除

- [x] Task 9: 测试
  - [x] 9.1 改写：LineNode CRUD/守卫、LineStructure 边集/链序/诊断、VectorLineScope、DeletePrinciple
  - [x] 9.2 新增：Line 创建双写/删除守卫、parent 设定与守卫（`line_id` 非 Line 拒绝）、创建即建边（首节点无边/次节点有边）、链序推导、多边类型分支（新 relation 属性）、内置种子拒改拒删、换线路连带校验
  - [x] 9.3 图投影：parent_of（Line→LineNode）对齐、line_segment 经 R6 管道建边、line_contains 不再出现
  - [x] 9.4 前端：创建 Dialog 前驱选择、LineStructureDialog 边列表
  - [x] 9.5 Api 测试数量不低于 330

- [x] Task 10: 验证与收尾
  - [x] 10.1 `dotnet build APromisedLand.sln` 0 错误
  - [x] 10.2 五套测试全绿（Api ≥330、Blazor.Shared、MafWfApi、FileStorageApi、Blazor）
  - [x] 10.3 全仓 grep 清零：`LineStructureChangedEvent`、`line_contains`、`TopologyMode`、`line_nodes`（迁移文件除外）
  - [x] 10.4 写 `review.md` 并勾选本文件

# Task Dependencies

- Task 2 依赖 Task 1（迁移含种子）
- Task 3、Task 4 依赖 Task 1、Task 2
- Task 5 依赖 Task 4（边集薄层复用 Facade 的同线校验）
- Task 6 依赖 Task 1、Task 3、Task 4（先接新链路再切旧事件）
- Task 7 依赖 Task 4
- Task 8 依赖 Task 4、Task 5
- Task 9 依赖 Task 3~8
- Task 10 依赖全部
