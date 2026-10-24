# R5 · 线路入图（review）

> 对照 `specs/node-sky/spec.md` §8.7 与 `r5-line-graph/spec.md` 逐项评审。
> 结论：**通过**。全解决方案构建 0 错误，五套测试全绿且不低于基线。未 commit / push。

## 1. 事件链路

| 环节 | 实现 | 状态 |
| --- | --- | --- |
| 事件契约 | `TreeGraph.Shared/Messaging/LineStructureChangedEvent.cs`：`required LineId` + `OccurredAt` | ✅ |
| 队列声明 | `MessagingExtensions`：publish + listen `treegraph.line-structure-changed`（durable，一事件一队列） | ✅ |
| 发布点 | `LineStructureService` 的 5 个结构变更成功点：`CreateAsync`、`ReplaceAsync`（空分支）、`ReplaceAsync`（正常分支）、`AddSegmentAsync`、`DeleteSegmentAsync` | ✅ |
| 不发布 | 端子（terminals）变更不触发结构事件 | ✅ |
| 消费者 | `LineStructureGraphSyncConsumer`：去重键 `graph-sync-line-structure:{LineId}`，窗口 `DedupWindowSeconds` → `EnsureReadyAsync` → `SyncLineStructureAsync` | ✅ |

事件只携带 `LineId`，消费端从 PG 现取状态，天然支持乱序与重放（收敛到最终态）。

## 2. 图内结构边

- `GraphNaming.LineSegmentEdge = "line_segment"`；属性 `edge_id/line_id`(string)、`weight`(double)、`display_name`(string)。
- `GraphSchemaInitializer`：`CREATE EDGE IF NOT EXISTS line_segment(...)`；并调用新增的 `EnsureEdgeColumnsAsync`（`DESCRIBE EDGE` → `ALTER EDGE ADD`）为存量 space 补列，重复初始化无副作用。
- `GraphSynchronizer.SyncLineStructureAsync(lineId)`：
  1. 取该线路 `line_nodes`，为空直接返回（不发任何图请求）；
  2. 对全部段端点执行 `SyncInodeAsync` 确保顶点存在；
  3. `LoadLineSegmentPairsAsync` 用 `GO FROM "<vid>" OVER line_segment` 逐顶点取既有边；
  4. 删除「既有 − 期望」的边，再 upsert 全部期望段（`weight/display_name` 为 null 时不下发）。
- `AddLineGraphAsync` 末尾追加线路结构段，全量重建与 `SyncCategoryAsync(Line)` 共用，边缘计入 `LineEdges` 统计。

## 3. 解耦边界

PG 内 `line_structure_segments` 仍与 `edge_instances` 完全无关（R4 结论保持不变）；R5 只新增「读 PG → 写 NebulaGraph」的单向投影，不产生反向依赖，也不新增 PG 表/列。

## 4. 测试

| 套件 | 基线 | 实测 | 结果 |
| --- | --- | --- | --- |
| TreeGraph.Api.Tests | 322 | 330 通过 | ✅ |
| TreeGraph.Blazor.Shared.Tests | 235 + 1 skip | 236 通过 + 1 skip | ✅ |
| TreeGraph.MafWfApi.Tests | 65 | 65 通过 | ✅ |
| TreeGraph.FileStorageApi.Tests | 153 | 153 通过 | ✅ |
| TreeGraph.Blazor.Tests | 71 | 71 通过 | ✅ |

- 全解决方案 `dotnet build APromisedLand.sln`：0 错误。
- 新增 `LineStructureSyncTests`（5 例，InMemory + 伪 Nebula REST）：建段 upsert（含 null 属性不下发）、删段删边、清空全删、无节点零写入、`SyncCategory.Line` bulk 载荷含结构边。
- 新增 `LineStructureEventTests`（3 例，真实 PG + `RecordingApiFactory`）：建结构、清空（PUT 空段）、加段各发一条事件且 `LineId` 匹配。
- 新增 `Fixtures/RecordingEventPublisher.cs`：`RecordingEventPublisher` + `RecordingApiFactory`（替换 `IEventPublisher`）+ 集合 fixture。

测试实现要点（踩坑记录）：
- InMemory 不支持 `JsonDocument` 列，`LineStructureSyncTests` 使用 `TestDbContext` 子类：`base.OnModelCreating` 后对 `ClrType == typeof(JsonDocument)` 的属性 `Ignore`。
- `NebulaGraphClient.QueryAsync` 把 nGQL 放在 JSON 的 `query` 字段（引号被转义），伪 REST 需先反序列化取 `query` 原文再做 `GO FROM "vid"` 匹配，否则既有边解析为空、删除用例假失败。

## 5. 未覆盖 / 后续

1. 段边的 `weight` 变更依赖「删除 + 重新 upsert」路径（同一 src/dst 直接覆盖），当前无独立用例专门验证 `weight` 更新，由 upsert 语义保证。
2. 图内 `line_contains` / `connects_to` 与 `line_segment` 的一致性未做联合校验；图查询侧若需「线路连通性」可基于 `line_segment` 直接遍历。
3. 结构边删除依赖 `GO FROM` 遍历该线路全部节点；线路节点规模极大时可改为分页/批量查询，当前按单线路规模评估可接受。
