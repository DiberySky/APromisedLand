# R5 Tasks

- [x] Task 1: 新增共享事件与队列
  - [x] 1.1 `TreeGraph.Shared/Messaging/LineStructureChangedEvent.cs`：`record { required LineId, OccurredAt }`
  - [x] 1.2 `MessagingExtensions`：publish/listen `treegraph.line-structure-changed`

- [x] Task 2: LineSky 发布结构事件
  - [x] 2.1 `LineStructureService` 增加 `PublishStructureChangedAsync(lineId, ct)`
  - [x] 2.2 在 5 个结构变更成功点（建/替换/清空/加段/删段）与 `PublishLineChangedAsync` 并列调用

- [x] Task 3: 图命名与 schema
  - [x] 3.1 `GraphNaming.LineSegmentEdge = "line_segment"`
  - [x] 3.2 `GraphSchemaInitializer` 创建该边（`edge_id/line_id string`、`weight double`、`display_name string`）
  - [x] 3.3 `EnsureEdgeColumnsAsync`：DESCRIBE EDGE + ALTER EDGE ADD 补列

- [x] Task 4: 同步器
  - [x] 4.1 `SyncLineStructureAsync(lineId)`：取 line_nodes/segments，确保端点顶点，upsert 段边
  - [x] 4.2 查询既有 `line_segment` 边并删除不在期望集合内的边
  - [x] 4.3 `AddLineGraphAsync` 追加结构边（全量重建 / SyncCategory.Line 共用）

- [x] Task 5: 消费者
  - [x] 5.1 `LineStructureGraphSyncConsumer.Consume(LineStructureChangedEvent)`：EnsureReady → SyncLineStructureAsync
  - [x] 5.2 去重键 `graph-sync-line-structure:{LineId}`，窗口取 `DedupWindowSeconds`

- [x] Task 6: 测试
  - [x] 6.1 新增 `LineStructureSyncTests`（InMemory + 伪 Nebula REST）：建段 upsert、删段 delete、清空全删、SyncCategory.Line 载荷含结构边
  - [x] 6.2 新增 `RecordingEventPublisher` / `RecordingApiFactory` 与 `LineStructureEventTests`：建/删结构各发一条事件
  - [x] 6.3 跑 Api 测试，数量不低于 322

- [x] Task 7: 验证与收尾
  - [x] 7.1 `dotnet build APromisedLand.sln` 0 错误
  - [x] 7.2 五套测试全绿（Api ≥322、Blazor.Shared ≥235+1skip、MafWfApi 65、FileStorageApi 153、Blazor 71）
  - [x] 7.3 写 `review.md` 并勾选本文件

# Task Dependencies

- Task 2 依赖 Task 1
- Task 4 依赖 Task 3
- Task 5 依赖 Task 1、Task 4
- Task 6 依赖 Task 4、Task 5
- Task 7 依赖全部
