# APromisedLand — TreeGraph EAV 系统

基于 .NET 10 + Aspire + PostgreSQL 的实体-属性-值（EAV）元数据系统，支持 iNode 聚合模型。

## 项目结构

```
TreeGraph.Api/              # EAV API 薄宿主（Program.cs + 编排）
  NodeEavSky/               # EAV 全部承载：Controllers/Services/Entities/Data/Infrastructure
  TreeSky/                  # 树组件后端（ITreeService/TreeControllerBase/EfTreeService）
TreeGraph.Blazor/           # Blazor Server 前端（MudBlazor 9.11）
TreeGraph.Shared/           # 共享 DTO
TreeGraph.TreeSky/          # 树组件库（TreeSky 精简移植，详见其 README）
TreeGraph.Api.Tests/        # API 集成测试（Testcontainers.PostgreSql）
TreeGraph.TreeSky.Tests/    # TreeSky 类库单元测试（xUnit）
TreeGraph.Blazor.Tests/     # Blazor 单元测试（bUnit）
TreeGraph.Blazor.E2E.Tests/ # Playwright E2E 测试
APromisedLand.AppHost/      # Aspire 编排
```

## 固定端口

| 服务 | 端口 |
|---|---|
| TreeGraph.Api (EAV) | 5773 |
| TreeGraph.Blazor | 5783 |
| PostgreSQL | 8433 |

## 核心模型

### EAV 元数据

- **EntityType**：实体类型（`et_` + 12 位 hex 自动生成）
- **AttributeCatalog**：属性定义（`attr_` + 12 位 hex）
- **OptionSet / OptionItem**：选项集
- **Unit / UnitCategory**：单位

### iNode 聚合层（外挂表，零破坏 EAV）

- `inode_entitytype`：iNode 声明的实体类型
- `inode_entity`：iNode 下的实体实例
- `GET /api/inode/{id}/json`：iNode 全景 JSON（支持 `includeNull` / `displayName` / `units=default|base|original`）

## 测试

```
API 集成:   100（含 TreeSky 后端测试 13）
TreeSky 单元: 63（纯逻辑 28 / Moq 服务 30 / bUnit 组件 5）
Blazor 单元: 81
E2E:        21
─────────────
总计:       265
```

## TreeSky 树组件移植决策

将 `APromisedLand.Razor/DiberyTree` 的 TreeSky 树组件移植到本仓库时，否决了「197 文件平铺复制 + 全局命名空间替换」方案（数据太乱、无法编译），改为：

| 决策 | 内容 |
|---|---|
| **独立类库承载** | 新建 `TreeGraph.TreeSky`（Razor 类库），宿主仅 ProjectReference + `AddTreeSky()` 接入，源项目零改动 |
| **精简闭包** | 只复制组件本身及真实依赖；外围 UI（Dialog/Loading/BoolField）新建薄包装承载 |
| **裁剪重耦合模块** | Attributes 属性子系统、附件页面、UnitTree/CategoryTree 具体实现均不复制，裁剪点以占位提示处理 |
| **内建 string 节点** | 库自带 `StringTreeNode`（string 为 sealed 且无 `new()`，不能直接作泛型参数） |
| **后端承载** | 泛型后端（`ITreeService<T>`/`TreeControllerBase<T>`/`EfTreeService<T>`）放 `TreeGraph.Api/TreeSky/`，类库保持纯前端 RCL；仅实现组件 9 端点调用面，CategoryTree/UnitTree 具体树不带 |
| **真实演示** | `/tree-sky-demo` 页读写真实 Postgres（`string_tree_nodes` 表，Aspire 服务发现直连 `treegrapheavapi`） |

移植过程修复的问题（库 DI `TryAddTransient` 失效、移动对话框无法确认等）及裁剪清单详见 [TreeGraph.TreeSky/README.md](TreeGraph.TreeSky/README.md)。

### E2E 关键决策

| 决策 | 原因 | 实现 |
|---|---|---|
| **串行执行** | E2E 用真实浏览器 + SignalR，并行导致连接竞争/服务负载/DB 污染 | `xunit.runner.json` + `parallelizeTestCollections: false`（需 `CopyToOutputDirectory`） |
| **数据自清理** | 测试累积导致 DB 膨胀，触发 N+1 性能问题 | 每个建数据的测试在 `finally` 调 `TryDeleteXxxByNameAsync` |
| **Drawer 定位** | MudBlazor Temporary Drawer 关闭后保留 DOM | `BlazorHelpers.FindDrawerByTitle` 按标题文本锚定 |

详见 [BlazorHelpers.README.md](TreeGraph.Blazor.E2E.Tests/Fixtures/BlazorHelpers.README.md)。

## 性能修复记录

### EntityTypes N+1（HTTP 146 → 1）

**问题**：`EntityTypes.razor.LoadAsync` 对每个类型串行调 `GetEntityTypeAsync`，145 个类型时 146 次 HTTP，耗时 12-20s，E2E 超时。

**修复**：
- 后端：`GET /api/eav/entity-types/details`（1 次 HTTP + GroupBy 聚合属性计数）
- 前端：`LoadAsync` 改单次调用

**效果**：页面加载 < 500ms。

## 开发约定

- `.razor` 文件用 LF 行尾
- EAV 实体 ID 为 GUID 字符串（`gen_random_uuid()::text` + `HasSentinel("")`）
- 软删除用 `IsDeleted` 字段，API 层过滤
- API 响应格式由 vLLM `response_format` 校验为主，`JsonSchemaValidator` 为后备
