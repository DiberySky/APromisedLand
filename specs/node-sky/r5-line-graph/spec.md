# R5 · 线路入图（LineSky → NebulaGraph 结构边同步）Spec

## Why

R4 把线路结构从边模块解耦：`line_structure_segments` 成为线路自己的拓扑单元，PG 内不再创建 `edge_instances`。代价是 NebulaGraph 里只剩 `line_contains`（线路含节点）与 `connects_to`（端子外接），**段与段之间的拓扑（谁连谁）没有进图**，图查询无法回答「沿线走几跳能到哪」。

R5 按 `specs/node-sky/spec.md` §8.7 定稿补齐：**PG 内继续解耦，图内统一拓扑**——由 LineSky 自发事件驱动，把线路结构段同步为图内的结构边。

## What Changes

- 新增共享事件 `LineStructureChangedEvent { LineId, OccurredAt }`，Wolverine 队列 `treegraph.line-structure-changed`（publish + listen）。
- `LineStructureService` 在结构变更成功后发布该事件（建结构 / 全量替换 / 清空 / 加段 / 删段；端子变更不触发）。
- `GraphNaming` 新增结构边类型 `line_segment` 及属性列：`edge_id`、`line_id`、`weight`(double)、`display_name`。
- `GraphSchemaInitializer` 幂等创建 `line_segment`，并对存量空间 `DESCRIBE EDGE` + `ALTER EDGE ADD` 补列。
- `GraphSynchronizer` 新增 `SyncLineStructureAsync(lineId)`：以 PG 现状为准，确保端点顶点存在 → upsert 当前段 → 删除该线路已不存在的结构边（幂等、可重放）。
- 全量重建 / `SyncCategory.Line` 的 `AddLineGraphAsync` 追加结构边。
- 新增消费者 `LineStructureGraphSyncConsumer`（带 5s 去重窗口，键含 LineId）。
- 测试：新增 `LineStructureSyncTests`（InMemory + 伪 Nebula REST，覆盖建/删/清空/重建载荷）与 `LineStructureEventTests`（真实 PG 容器 + 录制发布器，验证结构变更确实发事件）。

**BREAKING**：无。`line_segment` 是新增图边类型；PG 表结构不变。

## Impact

- Affected specs：NodeSky §8.7（线路入图）、R4 剩余欠账第 1 项。
- Affected code：
  - `TreeGraph.Shared/Messaging/LineStructureChangedEvent.cs`（新增）
  - `TreeGraph.Api/MessagingSky/MessagingExtensions.cs`
  - `TreeGraph.Api/LineSky/Services/LineStructureService.cs`
  - `TreeGraph.Api/GraphSyncSky/GraphSyncOptions.cs`（GraphNaming）
  - `TreeGraph.Api/GraphSyncSky/GraphSchemaInitializer.cs`
  - `TreeGraph.Api/GraphSyncSky/GraphSynchronizer.cs`
  - `TreeGraph.Api/GraphSyncSky/GraphSyncConsumers.cs`
  - `TreeGraph.Api.Tests/Tests/LineStructureSyncTests.cs`、`LineStructureEventTests.cs`（新增）
  - `TreeGraph.Api.Tests/Fixtures/RecordingEventPublisher.cs` / `RecordingApiFactory.cs`（新增）

## ADDED Requirements

### Requirement: 结构变更事件

LineSky SHALL 在每次线路结构（段集合）变更成功后发布 `LineStructureChangedEvent`，事件只带 `LineId`，消费端从 PG 现取状态。

#### Scenario: 建结构
- **WHEN** `PUT /api/eav/lines/{lineId}/structure` 成功
- **THEN** 发布一条 `LineStructureChangedEvent { LineId }`

#### Scenario: 加段 / 删段 / 清空
- **WHEN** 加段、删段或清空结构成功
- **THEN** 同样发布该事件（端子变更不发布）

### Requirement: 结构边进图

图同步 SHALL 把每条 `line_structure_segments` 投影为 NebulaGraph 的 `line_segment` 边（`FromNodeId → ToNodeId`），属性为 `edge_id/line_id/weight/display_name`。

#### Scenario: 首次同步
- **WHEN** 消费到某线路的结构事件且 PG 有 2 段
- **THEN** 图内该线路节点间出现 2 条 `line_segment` 边，属性与 PG 一致

#### Scenario: 段被删除
- **WHEN** PG 中某段已删、事件到达
- **THEN** 图内对应 `line_segment` 边被删除，其余段保留

#### Scenario: 结构清空
- **WHEN** 线路结构被删除/清空
- **THEN** 图内该线路的所有 `line_segment` 边被删除，`line_contains` 与 `connects_to` 不受影响

### Requirement: 全量重建包含结构边

`RebuildAllAsync` / `SyncCategoryAsync(Line)` SHALL 在批量写入中包含全部 `line_segment` 边。

#### Scenario: 重建
- **WHEN** 执行全量重建或按 Line 类别同步
- **THEN** bulk 载荷包含 `line_segment` 边，且 `LineEdges` 统计包含它们

### Requirement: 存量图空间补列

`GraphSchemaInitializer` SHALL 在已有 space 上为 `line_segment` 补齐缺失属性列（`DESCRIBE EDGE` + `ALTER EDGE ADD`）。

#### Scenario: 存量空间
- **WHEN** 旧 space 没有 `line_segment` 或缺少列
- **THEN** 初始化后该边类型与列均存在，再次初始化无副作用
