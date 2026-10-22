# 交接报告（APromisedLand / TreeGraph）

> 面向接手人：本文件是本机工作的权威入口。仓库内其他文档（`Notes/交接.md` 等）是历史会话日志，仅供参考，不要当作规范。
> 编写时间：2026-10，当前 HEAD `fc321c6`，工作区干净。本轮变更（P0-1 + P1 全量闭环）见第 6 节。

---

## 0. 30 秒速览

| 项 | 内容 |
|---|---|
| 主仓库 | `d:\APromisedLand`，解决方案 `APromisedLand.sln` |
| **当前活跃子系统** | **TreeGraph**（`TreeGraph.*` 系列项目）；其余同名目录多为旁支/实验项目，勿混改 |
| 技术栈 | .NET 10 + Aspire 编排 + Blazor Server(MudBlazor 9.11) + PostgreSQL + NebulaGraph + Qdrant + RabbitMQ + Python FastAPI 网关 |
| 数据真源 | PostgreSQL（EF Core）。NebulaGraph 只是**投影**，可从 PG 全量重建 |
| **线路结构默认拓扑模式** | **Network**（本项目主用于铁路牵引供电：多电源、上下行并联、分区所联络会成环）。Linear/Radial 需显式选择 |
| 前端端口 | API 5773、Blazor 5783、PG 8433、NebulaFastApi 9393、FileStorageApi 5326、MafWfApi 5323、Nebula Studio 17001、LiteGraph 8701/3001 |
| 测试基线（2026-10 实测） | API 314 通过 / Blazor.Shared 227 通过 + 1 跳过 / 0 失败 |
| 分支 | `main`，最近提交 `bba71f4`（交接报告本身）；功能基线 `e8fe376` |

**第一次上手建议顺序**：本文第 3 节把环境跑起来 → 第 4 节跑两个测试套件确认基线 → 第 6 节了解当前在做的线路结构功能 → 第 7 节挑未尽事项。

---

## 1. 仓库结构（先分清哪些不是你的活）

根目录下有 50+ 个 .csproj，绝大多数是历史/实验项目。**只改 `TreeGraph.*`**。

活跃：

```
TreeGraph.Api/                 EAV + 线路 + 图同步 + 空间等后端（薄宿主，编排在 Program.cs）
  NodeEavSky/                  EAV 主体：Controllers/Services/Entities/SemanticSearch
  LineSky/                     线路与线路结构（本次工作重点）
  EdgeSky/                     边（EdgeInstance）领域
  TreeEavSky/                  泛型树后端（ITreeService<T>/TreeControllerBase<T>）
  GraphSyncSky/                PG → NebulaGraph 同步（全量重建 + 分类同步）
  SpaceSky/                    空间（Space/分类）
TreeGraph.Shared/              跨层 DTO 与枚举（NodeEavSky/LineSky/EdgeSky/...）
TreeGraph.Blazor/              Blazor Server 管理台宿主
TreeGraph.Blazor.Shared/       组件库（RCL），各 *Sky 目录与后端一一对应
TreeGraph.Api.Tests/           后端集成测试（Testcontainers.PostgreSql）
TreeGraph.Blazor.Shared.Tests/ 前端组件测试（bUnit）
TreeGraph.Blazor.Tests/        Blazor 单元测试
TreeGraph.Blazor.E2E.Tests/    Playwright E2E
APromisedLand.AppHost/         Aspire 编排（入口是 AppHost.cs，注意不是 Program.cs）
docker/                        dashboard + keycloak 的 compose 与 .env
```

旁支（**不要顺手改**）：`APromisedLand.*`、`Maf*`、`Dibery*`、`ConsoleFoundry*`、`MafRagApi`、`QuestionService`、`WeatherApi`、`_archive/` 等。`APromisedLand.AppHost/AppHost.cs` 里被注释掉的 `MafRagApi()/MafWorkFlowService()` 就是这些旁支的入口，默认不启动。

**模块约定（必须遵守）**：每个业务域 = 一个 `XxxSky` 目录，跨三个项目同名出现：

```
TreeGraph.Api/XxxSky/{Controllers,Services,Entities}
TreeGraph.Shared/XxxSky/{Dtos,Entities}
TreeGraph.Blazor.Shared/XxxSky/{Dialogs,Components,Services}
```

---

## 2. 运行环境

### 2.1 依赖（全部由 Aspire 拉起，需 Docker Desktop 运行中）

`APromisedLand.AppHost/AppHost.cs` 的 `TreeGraph()` 声明顺序（顺序有依赖，勿随意调整）：

1. PostgreSQL、Redis
2. NebulaGraph 容器组（graphd/metad/storaged/console/studio:17001）
3. TreeGraph.NebulaFastApi（Python → Nebula REST 网关，9393）—— 必须在 EavApi 之前声明
4. SeaweedFS（对象存储，FileStorageApi 依赖）
5. RabbitMQ、Qdrant（语义搜索依赖）
6. TreeGraph.Api（5773）、TreeGraph.FileStorageApi（5326）
7. vLLM（chat/embedding 容器）、LiteGraph（8701）
8. TreeGraph.MafWfApi（5323）—— **必须在 Blazor 之前**，否则服务发现解析不到
9. TreeGraph.Blazor（5783）、TreeGraph.Blazor.Maui

### 2.2 启动

```powershell
# 推荐入口（会先检查 Docker → 构建 → 启动）
powershell -File .\start-aspire.ps1
```

### 2.3 已知环境故障与解法（都已踩过）

| 现象 | 原因 | 解法 |
|---|---|---|
| AppHost 启动卡住/超时，报 `Aspire.Hosting.Dcp.DcpDependencyCheck` | 本机 DCP 依赖检查超时，与代码无关 | 绕过 Aspire，直接 `dotnet run --project TreeGraph.Api`（PG 需已在运行），前端另起 `dotnet run --project TreeGraph.Blazor` |
| Qdrant 连接失败 | 连接串用了 `grpc://` scheme | 必须用 `http://127.0.0.1:6334`（gRPC 端口，非 6333 REST） |
| RabbitMQ 认证失败 | 容器默认密码不是 `guest` | 从容器环境取：`docker inspect <rabbitmq容器> --format '{{range .Config.Env}}{{println .}}{{end}}'` 找 `RABBITMQ_DEFAULT_PASS` |

---

## 3. 构建与测试

```powershell
# 构建
dotnet build APromisedLand.sln --nologo -v q

# 后端集成测试（Testcontainers 自带 PG，无需先起 Aspire）
dotnet test TreeGraph.Api.Tests/TreeGraph.Api.Tests.csproj --nologo -v q

# 前端组件测试（bUnit）
dotnet test TreeGraph.Blazor.Shared.Tests/TreeGraph.Blazor.Shared.Tests.csproj --nologo -v q

# 单跑某组用例
dotnet test TreeGraph.Api.Tests/TreeGraph.Api.Tests.csproj --filter "FullyQualifiedName~LineStructureTests"
```

**基线（2026-10 实测）**：
- `TreeGraph.Api.Tests`：314 通过 / 0 失败
- `TreeGraph.Blazor.Shared.Tests`：227 通过 / 1 跳过 / 0 失败

**注意**：`README.md` 里的测试数字（292 等）已过期，以本文件为准。

**注意事项**：
- `TreeGraph.Blazor.Shared` 编译有 44 条警告（MudBlazor `Dense` 属性分析器），属既有噪声，不是本次引入，不必清理。
- E2E 测试必须串行（`parallelizeTestCollections: false`），且每个建数据的用例要在 `finally` 自清理，否则库膨胀导致超时。

---

## 4. 数据与图同步（核心认知）

- **PG 是唯一真源**。所有业务写入只落 PostgreSQL。
- NebulaGraph 是**读侧投影**，通过 `GraphSyncSky` 同步：
  - `POST /api/graph-sync/rebuild` —— 全量重建（DROP SPACE 后重建，幂等）
  - `POST /api/graph-sync/category/{category}` —— 按类别增量同步（space/tree/line/edge）
- 每条业务写入会发事件触发同步；图 schema 由 `GraphSchemaInitializer` 在启动时 `DESCRIBE` + `ALTER TAG ADD` 自动补列（新增属性列只需登记到 `GraphNaming.TagColumns`，存量图无需重建）。
- **全量重建的重试**：`GraphSynchronizer` 的 bulk 写入对「schema 传播未完成」和「storage 选主瞬时错误（Not the leader / Storage Error / retry later）」做整体重试，因为 bulk 幂等。不要删这段重试，否则 DROP SPACE 之后紧跟的写入会静默写一半。
- PG 用**软删除/归档**语义（`is_archived`），删线路/删除结构有跨表守卫，不要绕过服务层直接改库。

**测试用手工验证套路**（本次工作用过，可复用）：先调 API 造数据 → `POST /api/graph-sync/rebuild` → 查 Nebula 顶点/边计数与属性 → 清理数据再重建，确认图与 PG 一致。临时脚本放 `.trae/` 下，用完删除，不要进版本库。

---

## 5. 当前工作重点：线路结构（LineSky）

### 5.1 概念

- **Line**（线路）是一等公民（实现 iNode），下挂 **LineNode**（线路节点）。
- **LineStructure**（线路结构）= 线路的有向拓扑：头表（`line_structures`，一条线路至多一个）+ 段（`line_structure_segments`，LineNode 之间的边）+ 端子（`line_terminals`，LineNode 与外部 iNode 的馈入/馈出/联络关系）。
- 段用 `EdgeInstance` 承载（边类型须 `IsLine=true`，且该类型不能用于普通边 —— 有守卫）。

### 5.2 三种拓扑模式（默认 **Network**，因本项目主用于铁路牵引供电）

| | Linear | Radial | Network（默认） |
|---|---|---|---|
| 形状 | 一条链，每点度 ≤ 2，恰 2 个端点 | 树状，边数 = 节点数 − 1 | 仅要求连通 |
| 成环 | 禁止 | 禁止 | 允许 |
| 端子 | 不要求、不限个数 | **恰 1 个 FeedIn** | 不要求（0 个也行） |
| 铁路牵引场景 | 单条供电臂（一所→接触网→末端） | 同一所多路馈线扇出 | 牵引网主体：上下行并联、分区所/AT 所联络、多所供电、越区供电 |

**为什么默认 Network**：牵引网的合法拓扑天然含环（上下行 + 横向联络）与多电源接入，Linear 会拒「成环/分叉」，Radial 会拒「FeedIn ≠ 1」，都会把合法结构挡在门外。代价是 Network 只校验连通，不校验形状；「同一时刻只投一路电源」不在这层，见 P0-1。

判定在 `TreeGraph.Api/LineSky/Services/LineStructureService.cs` 的 `ValidateCore`，顺序推导在同文件 `DeriveOrder`。

### 5.3 入参与接口

- `POST/PUT /api/eav/lines/{lineId}/structure`（创建/全量替换；**空请求 = 清空结构**，不做模式校验）
- `POST /segments`、`DELETE /segments/{edgeId}`、`POST /terminals`、`DELETE /terminals/{nodeId}`
- 建结构两种入参**二选一**：`Segments`（显式给段）或 `OrderedNodeIds`（给节点顺序，服务端按相邻自动成段，Linear 快捷方式）。

### 5.4 前端

`TreeGraph.Blazor.Shared/LineSky/Dialogs/LineStructureDialog.razor`：线路 Tab 里打开。包含模式选择（带业务白话提示）、线性快速成链面板、段/端子编辑、诊断条。切模式走全量替换，校验失败会回滚并提示。

### 5.5 端子角色

`FeedIn`（电源接入点）/ `FeedOut`（向外供电口）/ `Tie`（联络点，与外部互备）。数据库与图属性里存英文枚举名，UI 显示中文白话。

---

## 6. 已完成（2026-10-22 全量闭环）

### P0 —— 端子运行状态字段 ✅

- 范围：`LineTerminal` 实体加 `OperatingState` 列（字符串映射，默认 `Maintenance`/检修）+ DbContext 显式配置 + `CreateLineStructureRequest`/`LineStructureNodeDto`/`AddLineTerminalRequest`/`LineStructureTerminalInput` 4 个 DTO 加字段 + `ValidateCore` 加 FeedIn 投运（`Energized`）≤ 1 校验 + `DeriveOrder` 顺序推导 `FeedIn → Tie` 起点优先级 + 图同步 3 处（`SyncLineNodeTerminalsAsync`/`GetNodeInfoAsync`/全量重建）加 `operating_state` 属性 + `LineStructureDialog` 前端加状态下拉/端子表格状态列/诊断区投运数显示 + 重新生成 Initial 迁移。
- 业务决策（已按推荐值落地）：状态=投运/热备用/冷备用/检修；校验口径=FeedIn 投运最多 1 个；迁移策略=重新生成 Initial（清库）。
- 存量端子默认 `Maintenance`（检修）态，避免新校验误拒历史数据。
- NebulaGraph 侧 `LineNodeTag` 新增 `terminal_operating_state` 属性，`ConnectsToEdge` 新增 `operating_state` 属性。

### P1 ✅

1. **`Tie` 角色参与顺序推导**：`DeriveOrder` 中无 `FeedIn` 时 `Tie` 也可作为结构起点（联络点也是电源入口场景）。
2. **游离 LineNode 生成 Warning**：`GetAsync` 里比对线路全部 LineNode vs 结构段端点 ∪ 端子节点，游离节点写入 `Diagnostics.Warnings`（非阻断，仅提示）。
3. **`LineStructureDiagnosticsDto.Warnings` 重新加回** `List<string>`，承载所有非阻断提示。
4. **`TieCount` + `FeedInEnergizedCount` 新增** 诊断字段，前端展示。
5. **`LineStructureDialog` bUnit 冒烟测试 ✅**（5 个用例）：[LineStructureDialogSmokeTests.cs](TreeGraph.Blazor.Shared.Tests/LineSky/LineStructureDialogSmokeTests.cs)，覆盖组件渲染/诊断区计数/端子状态列/拓扑模式默认/运行状态下拉控件。

### P2 ✅

6. `README.md` 更新：项目结构补全全部 Sky 模块 + 端口表补全 + 测试数字对齐 2026-10 基线。
7. `Notes/交接.md` 已归档到 `Notes/_archive/交接-2026-10-原始会话日志.md`。
8. AppHost DCP 超时：环境问题，**无解**（代码层面），已在 README/HANDOVER 标注。
9. `.trae/` 无残留临时脚本。

### 测试基线（2026-10-22 全量闭环后回归验证）

- TreeGraph.Api.Tests：**322 通过 / 0 失败 / 0 跳过**（314 旧 + 8 新）
- TreeGraph.Blazor.Shared.Tests：**232 通过 / 0 失败 / 1 跳过**（227 旧 + 5 新）

---

## 7. 工程约定（照做，别自创）

- **提交**：只在你确认后才 commit/push；提交信息用 `feat(xxx):` / `fix(xxx):` / `chore:` 前缀，正文写「为什么」。
- **迁移**：不做增量迁移，改模型后重新生成 `TreeGraph.Api/Data/Migrations` 下的 Initial（代价是清库，见 P0-1c）。
- **不要绕过服务层直接写库**：删除线路/段/端子都有引用守卫（`InodeOwnerGuardRegistry`、端子孤儿检查等）。
- **不要把技术术语写进用户可见文案**：UI 只说场景与操作，判据写进代码注释（线路结构对话框已按此改造，可作为范例）。
- **测试**：新增校验规则必须同时补通过路径与拒绝路径用例；后端用 Testcontainers，别连开发库。

---

## 8. 文档索引

| 文档 | 用途 |
|---|---|
| 本文件 `HANDOVER.md` | **权威入口**：结构、环境、基线、未尽事项 |
| `README.md` | 项目定位、总体结构、历史决策（部分数字已过期） |
| `TreeGraph.Blazor.Shared/README.md` | 树组件库移植决策与裁剪清单 |
| `docs/` | `E2E_VERIFICATION.md`、`smoke-checklist.md`、`dev-seed.sql`、审计与技能说明 |
| `TreeGraph.Blazor.E2E.Tests/Fixtures/BlazorHelpers.README.md` | E2E 帮助类与 Drawer 定位技巧 |
| `Notes/` | 历史会话日志（非权威，见 P2-8） |

---

## 9. 接手后第一小时建议动作

1. `git log --oneline -10` 看最近五笔（线路结构建模 → 图同步重试修复 → 三模式口径对齐 → 默认改 Network → 本报告），快速理解上下文。
2. 跑第 3 节两条测试命令，确认 314 / 227 基线。
3. 打开 `TreeGraph.Blazor.Shared/LineSky/Dialogs/LineStructureDialog.razor` 与 `TreeGraph.Api/LineSky/Services/LineStructureService.cs`，对照 5.2 的表格读一遍，就掌握了当前最大的一块业务。
4. 就 P0-1 的三件事（状态取值 / 校验口径 / 迁移策略）找业务确认，这是下一块该做的事。
5. 工作区已干净，无需先做清理；第 10 节记录了最近几笔提交，读一遍再动手。

---

## 10. 变更记录（本机最后一轮工作）

### 10.1 已提交（`main`，按时间从新到旧）

| 提交 | 内容 |
|---|---|
| `bba71f4` | 新增本交接报告 + README 首部指向它的提示 |
| `e8fe376` | **拓扑模式默认改为 Network**（理由见 5.2），并同步前端默认值与「（默认）」标记；用例改名 `Create_WithoutTopologyMode_DefaultsToNetwork` |
| `8ce3de5` | 三模式规则口径对齐（Radial 恰 1 个 FeedIn、Network 放开端子强制、删除恒空 Warnings）、前端接入 `OrderedNodeIds` 快速成链、模式提示改业务白话、新增 5 个校验用例 |
| `b1a4f8d` | 全量重建重试覆盖 Nebula storage 选主瞬时错误（`Not the leader` / `Storage Error` / `retry later`），修复 rebuild 返回 200 但只写一半的问题 |
| `3948580` | 线路结构（拓扑线）独立建模：`LineStructure`/`LineStructureSegment`/`LineTerminal` + 三模式校验 + 图同步（line / line_node / line_contains / connects_to）+ 前端 `LineStructureDialog` |

### 10.2 默认模式改动明细（已随 `e8fe376` 提交）

| 文件 | 改动 |
|---|---|
| `TreeGraph.Shared/LineSky/Entities/LineStructureTypes.cs` | 注释改为「默认 Network」并写明铁路牵引供电原因 |
| `TreeGraph.Shared/LineSky/Dtos/LineStructureDtos.cs` | `LineStructureDto` / `LineStructureRequest` 的 `TopologyMode` 默认值 → `Network` |
| `TreeGraph.Api/LineSky/Entities/LineStructure.cs` | 头表实体默认值 → `Network`，注释同步 |
| `TreeGraph.Api/LineSky/Services/LineStructureService.cs` | 无结构时返回 DTO 的默认模式 → `Network` |
| `TreeGraph.Blazor.Shared/LineSky/Dialogs/LineStructureDialog.razor` | 「（默认）」标记移到 Network；`_topologyMode` 初值 → `Network`；Network 适用场合补「牵引供电的接触网（上下行并联、分区所联络、多所供电）」 |
| `TreeGraph.Api.Tests/Tests/LineStructureTests.cs` | 用例改名 `Create_WithoutTopologyMode_DefaultsToNetwork`，断言 `Network` |

- 提交前已跑通：`TreeGraph.Api.Tests` 314 通过、`TreeGraph.Blazor.Shared.Tests` 227 通过 + 1 跳过、0 失败，构建无新增警告。
- 该改动**不影响数据库**：`topology_mode` 列没有 DB 默认值，默认值只存在于 C# 初始化器，无需迁移。

