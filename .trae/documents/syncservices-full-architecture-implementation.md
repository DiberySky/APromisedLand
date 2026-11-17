# TreeGraph.SyncServices 全量架构落地计划

## Context

当前 `TreeGraph.SyncServices` 是复制式草稿：直接引用整个 `TreeGraph.Api` Web 宿主，构建被静态资产阻断，并自动引入核心 API 的 MVC ApplicationPart。核心仍注册旧消费者和后台任务，`EntityChangedEvent` 没有跨进程路由，现有 `Solo` 消息模式也不能保证业务提交与事件发布的原子性。

目标是落地为“模块化单体核心 API + 独立投影服务”：核心独占 PostgreSQL 业务写入、迁移和种子；SyncServices 只读业务数据，独占 NebulaGraph/Qdrant 投影、搜索、手动同步与对账；两个宿主只共享明确的小程序集，不互相引用。

## 目标依赖

- `TreeGraph.Contracts`：四类业务变更事件、反向清理命令及消息契约。
- `TreeGraph.Persistence → TreeGraph.Shared`：`TreeGraphDbContext`、EF 实体和 29 个模块化配置；Shared 中已被客户端复用的领域实体暂留，避免向 Blazor 传递 EF 依赖。
- `TreeGraph.GraphSync → TreeGraph.Persistence + TreeGraph.Shared`：核心管理功能与投影服务共用的 Nebula 客户端、命名、选项、DDL 和纯投影类型。
- `TreeGraph.WebShared → TreeGraph.Shared`：两个 Web 宿主共用的响应信封和 JSON 转换器。
- `TreeGraph.Api`、`TreeGraph.SyncServices` 分别引用上述项目，二者互不引用；移除 `InternalsVisibleTo=TreeGraph.SyncServices`。
- 继续使用同一个 `TreeGraphDb`；核心拥有业务表和迁移，Wolverine 消息存储使用独立 schema，不新增第二套业务数据库。

## 实施步骤

### 1. 建立项目骨架并统一基线

- 新增 `TreeGraph.Contracts`、`TreeGraph.Persistence`、`TreeGraph.GraphSync`、`TreeGraph.WebShared`、`TreeGraph.SyncServices.Tests`，更新 [APromisedLand.sln](file:///d:/APromisedLand/APromisedLand.sln) 和各项目引用。
- Wolverine 全部统一到 `6.64.1`，EF Core/Npgsql 保持现有兼容的 `11.0.0-rc.3` 基线。
- 每完成一个移动阶段即构建相关项目，避免最后集中处理跨程序集错误。

### 2. 抽离持久化与消息契约

- 将 [TreeGraphDbContext.cs](file:///d:/APromisedLand/TreeGraph.Api/Data/TreeGraphDbContext.cs)、[Configurations](file:///d:/APromisedLand/TreeGraph.Api/Data/Configurations/) 和 API 侧 EF 实体移动到 `TreeGraph.Persistence`，同步对齐命名空间与 using；`ApplyConfigurationsFromAssembly` 改为显式使用 Persistence 程序集。
- 迁移和种子继续留在核心 API。核心在 [Program.cs](file:///d:/APromisedLand/TreeGraph.Api/Program.cs) 和设计期工厂中显式指定 `MigrationsAssembly("TreeGraph.Api")`，SyncServices 不执行 Migrate/Seed。
- 将 Shared 的 Messaging 事件移入 Contracts，更新核心、SyncServices 和测试引用；保留既有 DTO 的公开形状。
- 定义幂等 `TagProjectionCleanupRequested` 命令，包含空间、源属性/Tag 标识和版本，供投影服务确认墓碑删除后由核心清理业务账本。

### 3. 拆出共享图基础设施和 Web 契约

- 将 `GraphNaming`、`SeedSpaceIds`、`SyncCategory`、`NebulaGraphClient`、`GraphSyncOptions`、`EdgeNgqlDdlBuilder`、`SpaceNodeTagProjector` 移到 `TreeGraph.GraphSync`。
- 核心的 Space/Edge 同步管理继续通过该共享项目调用 Nebula；`GraphSchemaInitializer.NgSpaceName` 等静态依赖改用共享命名工具。消费者、全量同步器、初始化/对账 hosted services 不属于核心，留在 SyncServices。
- 将 [ApiEnvelopeFilter.cs](file:///d:/APromisedLand/TreeGraph.Api/NodeEavSky/Infrastructure/ApiEnvelopeFilter.cs) 和相关 JSON converter 移到 WebShared，保持搜索/图同步响应信封兼容。
- SyncServices 只注册自己的 `SearchController`、`GraphSyncController` 和健康端点；不引用 API ApplicationPart，不暴露核心控制器。

### 4. 移动投影模块并收回业务写权

- 将核心 `GraphSyncSky` 与 `NodeEavSky/SemanticSearch` 的消费者、同步器、搜索服务、控制器和 hosted services 真正移动到 SyncServices，删除核心旧实现，不保留双份代码。
- 核心 Program 移除 `AddSemanticSearch`、旧 `AddGraphSync` 及其初始化/对账任务，只保留核心写业务、Nebula 同步管理 gateway 和消息发布。
- 将 GraphSchemaInitializer 中的 TagProjection 懒物化移到核心：空间创建路径与核心启动 backfill 负责补齐账本；SyncServices 启动前等待核心完成迁移/backfill。
- 将 `SpaceNodeTagSyncConsumers`、`SpaceNodeTagReconcileService` 中直接删除墓碑的写法改为发布清理命令；核心 Wolverine handler 条件删除对应账本。SyncServices 业务代码不得再出现业务 `Add/Remove/SaveChanges`。
- SyncServices 查询统一 `AsNoTracking`，保留现有幂等 Nebula/Qdrant upsert/delete 与对账能力。

### 5. 落地持久化 Outbox/Inbox 和完整消息拓扑

- 引入并核对 Wolverine `6.64.1` 的 EF Core/PostgreSQL 集成 API，启用 PostgreSQL 消息持久化、EF 事务集成、持久化 incoming/outgoing 和既有 RabbitMQ 重试/DLQ；移除 `DurabilityMode.Solo`。
- 核心增加统一写请求 Unit of Work：使用 Npgsql ExecutionStrategy 包住整个写 action，让多个 `SaveChangesAsync` 与 Outbox 记录共享 connection/transaction，成功后提交，由 Wolverine 在提交后派发。
- 收拢 `NodeController`、`SpaceNodeService`、`LineNodeController`、`LineStructureService`、`EavWriteService` 等现有显式事务，避免请求级事务与局部事务嵌套；非 HTTP 写操作复用同一 UoW。测试环境的 Noop/Recording publisher 保持轻量。
- `IEventPublisher` 不再直接即时发送 RabbitMQ，而是参与当前 Outbox 事务。不得只添加配置就声称具备事务性，必须用故障测试证明。
- 核心发布四个业务队列：`inode-changed`、`edge-changed`、`entity-changed`、`space-node-tag-projection-changed`；新增此前缺失的 `entity-changed` 跨进程路由。核心只监听反向清理命令队列，不监听四个投影队列。
- SyncServices 监听四个业务队列并发布清理命令。`InodeChangedEvent` 的 Graph 与 Vector handler 在同一消费进程中保持现有编排和故障隔离，不能因同队列竞争而只执行其中一个。
- 使用稳定的 Wolverine envelope 身份、持久化 Inbox、确定性 upsert/delete 和必要的版本/乱序防护；语义为 at-least-once + 幂等，不承诺端到端 exactly-once。

### 6. 接入 AppHost 并切换调用方

- 在 [AppHostResourceContext.cs](file:///d:/APromisedLand/APromisedLand.AppHost/AppHostResourceContext.cs) 增加 SyncServices 资源，新建编排扩展并接入 [AppHost.cs](file:///d:/APromisedLand/APromisedLand.AppHost/AppHost.cs)，使用现有 `5774` 端口。
- 核心保留 TreeGraphDb、RabbitMQ、NebulaFastApi、FileStorageApi；Qdrant 和 Embedding 接线移到 SyncServices。SyncServices 注入 TreeGraphDb、RabbitMQ、NebulaFastApi、Qdrant、Embedding，并等待核心迁移/健康检查完成。
- Blazor/MAUI 增加 SyncServices 服务发现引用。将 [EavApiClient.cs](file:///d:/APromisedLand/TreeGraph.Blazor.Shared/NodeEavSky/Services/EavApiClient.cs) 中搜索、向量对账、图同步方法切到独立 SyncServices HttpClient，更新对应页面和 MAUI 注册；核心 EavApiClient 继续负责业务数据。
- MafWfApi 的 `SearchApiClient` 改用 `TreeGraphSyncServices:BaseAddress`，`SubtreeLoader` 等核心查询仍使用 TreeGraphApi。AppHost 注入两个不同地址，本地回退分别为 `5773` 和 `5774`。

### 7. 迁移和补充测试

- 新建 SyncServices.Tests，迁移 `SourceTextBuilder`、`EntityVector`、`SearchServicePath`、`VectorLineScope` 及相关同步测试；核心测试只保留业务/API/核心 gateway 范围。
- 更新 [EavApiFactory.cs](file:///d:/APromisedLand/TreeGraph.Api.Tests/Fixtures/EavApiFactory.cs)、Noop/Recording publisher、Blazor.Shared.Tests 和 AppHost.Tests 的接线断言。
- 增加架构测试：禁止 `SyncServices → Api`、禁止核心投影消费者/旧路由、禁止 SyncServices 业务写库、禁止核心 ApplicationPart 泄漏。
- 增加消息集成测试：四队列路由、Graph+Vector 均执行、清理命令回到核心、重复/乱序消息、Rabbit 暂停时 Outbox 留存及恢复后派发。

## 验证

1. `dotnet restore APromisedLand.sln`、`dotnet build APromisedLand.sln --no-restore`：0 error；记录既有警告。
2. EF `has-pending-model-changes`：程序集移动不得产生隐式模型变化；只有明确设计的消息/账本变更才允许新迁移。
3. 运行核心、SyncServices、AppHost、Blazor.Shared 等相关测试及解决方案测试；本改造相关测试必须全绿，既有 bUnit/E2E 基线问题单独列出。
4. 启动 Aspire，确认 `5773/health`、`5774/health`、`5783` 正常；核心无旧投影 listener/搜索/图同步路由，SyncServices 仅有所需控制器。
5. 分别触发 inode、edge、独立 entity、TagProjection 变更，验证 Nebula/Qdrant 最终一致；验证 Blazor/MAUI/MafWf 搜索、手动图同步和向量对账均访问 `5774`。
6. RabbitMQ 不可用时提交业务变更，确认 Outbox 已持久化；恢复后消息仍送达。验证重启、重复消息、Vector 故障不丢 Graph 投影，以及核心对墓碑清理命令的幂等处理。
7. 最终检查无核心旧模块残留、无 `SyncServices → TreeGraph.Api`、无面向 SyncServices 的 IVT、无业务库写入或 Migrate/Seed。

不默认提交或推送；完成后列出改动、测试结果和仍需用户决定的部署切换事项。
