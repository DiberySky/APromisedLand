# TreeGraph Blazor Server → WebAssembly：方案评估与实施计划

## 1. 目标与本文决策范围

目标是在保留现有系统可用的前提下，评估并实施 Blazor Server → Blazor WebAssembly 迁移，最终完整替代 `TreeGraph.Blazor`（含全部身份管理页），允许分阶段双跑和回退。

本文先比较三条路线，再给出推荐路线的可执行计划；批准本文不等于同时实施三种方案。

- 方案 A：保留 Blazor Server，缩小认证 Cookie，服务端持有 JWT 并转发 API。
- 方案 B：新建 WASM + BFF，浏览器只持有 HttpOnly 会话 Cookie，不接触 token。
- 方案 C：新建纯 WASM SPA，浏览器通过 OIDC Authorization Code + PKCE 获取并持有 JWT，无应用认证 Cookie。

基于此前已明确的“纯 SPA + JWT、完整替代、分阶段双跑”偏好，**推荐并以方案 C 为实施主线**。方案 B 是浏览器 token 安全评审或 WASM 流式验收不通过时的备选，不在本次实施中混合引入；方案 A 不满足无应用认证 Cookie 和去 Circuit 的目标。

## 2. 当前状态与已核实事实

1. `TreeGraph.Blazor/Program.cs` 当前使用 `treegraph.auth` Cookie + OIDC(code+PKCE) + `treegraph-web` confidential client，并通过 `AddInteractiveServerComponents` 建立 Blazor Server Circuit；access token 存在 Redis，JWT 不写入认证 Cookie。
2. `TreeGraph.Blazor.Shared` 已声明 browser 支持，组件、DTO、业务 HttpClient、SSE 和文件客户端具有较高复用价值；但服务器宿主、认证状态、服务端 ActorAccessor、Aspire 服务发现地址不能直接复用。
3. `TreeGraph.IdentityAdmin` 通过 `AddIdentityAdmin` 在 Blazor Server 进程内注册全部管理服务，没有管理 HTTP API。完整迁移管理页时，无论 BFF 还是纯 SPA，都需要新增受保护的 HTTP 边界；管理密钥仍只能留在服务端。
4. `MafWfSseReader` 使用 `HttpRequestMessage` + `ResponseHeadersRead` 自行解析 SSE，并非原生 `EventSource`，因此可由 Bearer Handler 附加 JWT。仍需实测浏览器是否增量交付响应流、取消是否断流以及代理是否缓冲。
5. 文件上传已经分块；下载虽然流式读取 API 响应，但 `DownloadProgressDialog` 将完整内容写入 `MemoryStream` 后再交给 JS，大文件内存占用会随文件大小增长，必须专项改造。
6. TreeGraph 业务项目未发现业务 SignalR Hub；工作流实时能力走 HTTP SSE。迁移去除的是 Blazor Server Circuit 的 SignalR，不应据此宣称取消所有业务实时通信。
7. `TreeGraph.Api` 的 `Identity:RequireAuthenticatedApi` 默认关闭，现有 Server 的业务 HttpClient 仍有匿名兼容路径。双跑期不能提前全局强制鉴权，否则会切断旧系统。
8. Aspire `https+http://...` 服务发现名称只在服务端可用；浏览器需要运行时公共绝对 URL 和精确 CORS。
9. 测试需区分：纯 UI bUnit 可大量复用；涉及 JS interop、认证和 HttpClient 的测试需要新的 mock；`WebApplicationFactory` 若测试 API 端点，应继续用于对应 API，不能一概视为不可复用。

## 3. 方案对比与评估

| 维度 | A：Server + 最小 Cookie + API JWT | B：WASM + BFF | C：纯 WASM SPA + 浏览器 JWT |
|---|---|---|---|
| 应用认证 Cookie | 保留，尽量只存会话标识 | 保留 HttpOnly 会话 Cookie | 不创建；Keycloak 自身 SSO Cookie 仍可能存在 |
| 浏览器是否持有 token | 否 | 否 | 是，仅内存，禁止持久化 |
| Blazor Server Circuit | 保留 | 移除 | 移除 |
| 静态部署 WASM 资产 | 不适用 | 可以，但仍需部署 BFF | 可以，另需无密钥的配置宿主 |
| Shared 组件复用 | 最高 | 高 | 高 |
| IdentityAdmin 改造 | 可继续进程内调用 | 需 BFF/管理 HTTP 边界 | 必须新增独立管理 API |
| CORS | 基本无新增浏览器跨域 | 可经同源 BFF 减少 | 必须为各 API 配置精确 origin |
| SSE | 当前服务端链路 | BFF 必须禁用缓冲并转发流 | 当前 HttpClient SSE 可带 Bearer，需 WASM 实测 |
| 文件传输 | 当前服务端链路 | 可由 BFF 代理，仍需控制缓冲 | 上传可复用分块；下载需消除 MemoryStream 全量缓冲 |
| token 被 XSS 窃取风险 | 较低 | 较低 | 较高，依赖 CSP、依赖审计和内存令牌 |
| 多标签/刷新 | 服务端会话成熟，但仍需撤销策略 | BFF 统一会话和刷新 | 必须显式处理单航班刷新、并发 401、多标签登出 |
| 部署与安全评审成本 | 低 | 中 | 高 |
| 总体改造量 | 最小 | 大 | 最大，尤其管理 API 与浏览器安全边界 |
| 是否满足既定动机 | 不满足去 Cookie/Circuit | 不满足无应用 Cookie | 满足 |

### 条件化结论

- 若真实需求只是解决 Cookie 体积、DataProtection 或服务端令牌转发复杂度，A 的代价最低，但不构成 WASM 迁移。
- 若首要需求是 WASM、静态 UI 和最小化浏览器 token 风险，B 更稳妥；代价是保留应用会话 Cookie 和 BFF 运维边界。
- 若“无应用认证 Cookie、浏览器直接 Bearer、最终完全替代 Server”是硬需求，选 C。必须接受更高的 XSS 防护、CORS、刷新竞态、管理和传输验收成本，不能用 UI 隐藏替代服务端授权。

## 4. 方案 C 的目标结构与约束

```text
TreeGraph.Blazor.Wasm           纯浏览器客户端：UI、OIDC、内存令牌、Bearer Handler
TreeGraph.Blazor.WebHost        静态宿主：WASM 文件、SPA fallback、/config.json、安全响应头
TreeGraph.Blazor.Shared         保留复用：组件、DTO、业务客户端、SSE/文件客户端
TreeGraph.Shared                保留复用：共享契约；必要时承接纯管理契约
TreeGraph.IdentityAdmin.Api     新增服务端管理 API
TreeGraph.IdentityAdmin         保留复用：Keycloak 管理、审计、备份、Step-Up 等领域服务
TreeGraph.Blazor                双跑期保留，最终验收后退役
```

实施约束：

- 单一 `apromisedland` realm；新增 `treegraph-wasm` public client，不改变 `treegraph-web` confidential client 的现有用途；`treegraph-admin` 密钥和管理 token 只能留在服务端。
- 不改变现有 API 路由、DTO/JSON 结构和既有调用行为。新管理 API 使用纯 DTO，浏览器不得引用 EF、Redis、Keycloak 管理实现。
- WebHost 不注册 Cookie/OIDC、Redis、IdentityAdmin 或 YARP，不持有密钥。
- 双跑期间不提前打开现有业务 API 的全局 `RequireAuthenticatedApi`；新管理 API 从第一版起所有端点强制 JWT、管理员授权和服务端 Step-Up。
- Keycloak Bootstrap 与 IdentityAdminDb 迁移必须始终只有一个所有者。
- 不伪造运行或测试结论；完整 Aspire E2E 由用户启动，未实测项明确标为未验证。
- 不主动创建额外说明文档；不提交或推送，除非用户明确说“完美”。

## 5. 横切设计

### 5.1 浏览器认证与令牌

- `treegraph-wasm` 使用 public client、Authorization Code + PKCE S256、state/nonce、精确 redirect/post-logout URI；禁止 implicit 和 client secret。
- 建立 OIDC 适配层、`AuthenticationStateProvider`、`IAccessTokenProvider` 与 Bearer `DelegatingHandler`，继续复用 `[Authorize]` / `AuthorizeView`。
- access/refresh token 仅保存在内存，不写 localStorage、sessionStorage、IndexedDB 或应用 Cookie；发布产物和运行时不打印 token。
- 刷新采用单航班并提前执行；并发 401 最多共同刷新一次，原请求仅按幂等策略重试，非幂等请求与 SSE POST 不自动重放。
- 页面重载导致内存状态丢失时，通过 Keycloak SSO 重新走授权码流程；IdP 会话失效则回登录页。
- 多标签不共享 token，用 BroadcastChannel（storage event 降级）广播登出；登出同时执行 Keycloak end-session、清理内存状态并取消上传/SSE。
- 对 Keycloak public client refresh token 策略、轮换/重用失败、SSO idle/max、front/back-channel logout 做显式配置和测试，不能假定默认行为满足要求。

### 5.2 安全边界

- WebHost 输出严格 CSP：`default-src 'self'`、`object-src 'none'`、`frame-ancestors 'none'`、`base-uri 'self'`；`connect-src` 仅允许 Keycloak 和配置的 API；脚本策略仅保留 Blazor WASM 必需项，不开放宽泛 inline。
- 第三方脚本和 JS 依赖纳入审查；发布产物扫描 token、client secret、管理密钥和敏感配置。
- 高危管理写由管理 API 校验已验证 JWT 的 `auth_time`/max-age；陈旧时返回标准化 `step_up_required`，WASM 再以 `max_age=0` 重新认证。UI 票据不能替代服务端新鲜度校验。
- 管理 API 从 JWT 构造 `AdminActor`，不信任浏览器传入的操作者；复用 `AdminOperationRunner` 双阶段审计、审计失败关闭、写前备份和最后管理员保护。

### 5.3 运行时配置、CORS 与缓存

- WebHost 提供无缓存 `/config.json`，只含 Keycloak authority/public client id 和各 API 公共基址，实现一份构建多环境。
- 浏览器 HttpClient 使用运行时绝对 URL 并统一经 Bearer Handler；不使用 Aspire 服务发现伪协议。
- 各业务 API 和管理 API 配置精确 WASM origin，不启用 credentials；按需允许 `Authorization`、`Range`、`X-Chunk-Sha256` 等请求头并暴露下载响应头，CORS 预检不要求 JWT。
- `index.html`、`/config.json` 禁止缓存；指纹静态资源使用长缓存，保证版本切换和回退。

## 6. 分阶段实施

### P0：基线、范围与安全门禁

1. 运行现有 Blazor、Shared、IdentityAdmin 和各 API 测试，记录通过数及既有失败；已知 Shared 基线失败不得误算为迁移回归。
2. 建立页面/路由与 79 处 IdentityAdmin/ActorAccessor 注入清单，逐项标记“直接复用 / HTTP 适配 / 必须重写 / 退役”。
3. 记录现有 API 契约、旧新 audience、首屏与关键树/EAV 页面性能基线；明确浏览器/终端支持范围。
4. 对 CSP、第三方依赖、内存令牌、Keycloak 会话策略做安全评审。如果“浏览器持有 token”不可接受，停止 C 并重新评估 B，不在实施中悄然改为混合认证。

**退出门槛：** 迁移清单完整，基线可复现，方案 C 的安全前提获得确认。

### P1：最小垂直切片——登录 + 一个只读业务页

**不包含管理 API，不迁移文件和工作流页面。**

1. 新增 `TreeGraph.Blazor.Wasm` 与 `TreeGraph.Blazor.WebHost`，加入解决方案和 Aspire AppHost；WASM 使用独立 HTTPS 入口，旧 Server 保留原入口。
2. 新增 WASM App/Routes/MainLayout/NavMenu、OIDC 内存令牌实现、Bearer Handler 和 `/config.json` 加载。
3. 新增浏览器端 DI 注册入口，复用 Shared 组件/DTO 和 `SpaceTreeClientService`，只迁移 `/space-tree` 只读展示。
4. 在 `APromisedLand.AppHost/KeycloakRealmpart.json` 增加 `treegraph-wasm`，并扩展 `KeycloakBootstrapService` 幂等创建/更新该客户端，已有数据卷无需重建。
5. 调整 `EnsureTokenMappersAsync` 支持显式 audience 参数，旧 Web/Maui 行为不变；WASM 使用自己的 audience。
6. `TreeGraph.Api` 将原 Audience 与新增 AdditionalAudiences 合并验证，同时接受旧新 token；只新增 WASM origin CORS，不打开全局授权开关。

**退出门槛：** PKCE 登录、刷新后 SSO 恢复、角色显示、只读空间树数据、业务请求 Bearer、无应用认证 Cookie、无持久化 token；旧 Server 和旧 audience 回归通过。记录首屏压缩传输量、冷启动和内存实测，未达到双方确认的性能预算则不扩大页面迁移。

### P2：第二垂直切片——管理 API 读写闭环

1. 新增 `TreeGraph.IdentityAdmin.Api`，复用 `TreeGraph.IdentityAdmin`；首批只实现用户搜索/详情、禁用/启用及必要会话撤销。
2. 新增基于 HttpContext JWT 的 `IAdminActorAccessor`、管理员授权策略、服务端 Step-Up 校验和统一错误映射；所有管理端点默认拒绝匿名。
3. 服务端注册 DataProtection 备份保护、Redis 撤销/票据和 IdentityAdminDb；密钥只注入该 API。
4. 新 API 接管 Keycloak Bootstrap 时，旧 Blazor 必须关闭对应 HostedService；数据库迁移同样保持单所有者。
5. WASM 增加用户管理 HTTP 客户端，优先以适配器实现现有 UI 服务契约，完成用户列表 → 详情 → 禁用 → 启用闭环。

**退出门槛：** 匿名 401、非管理员 403、陈旧 `auth_time` 返回 step-up、重新认证后成功；禁用后撤销生效；审计前后记录、审计失败关闭、备份恢复均有测试证据；浏览器产物无管理密钥。

### P3：业务模块、文件与工作流

1. 按路由清单迁移树、空间、EAV、线路、节点和搜索页面；Shared 客户端统一接浏览器基址和 Bearer Handler。
2. 为 SyncServices、FileStorageApi、MafWfApi 增加双 audience 和精确 CORS，保持现有契约及旧 Server 兼容。
3. 处理 `DevSourceFileLocator` 等 DEBUG 文件系统能力：浏览器构建不得扫描服务器文件，提供开发期替代或禁用入口。
4. 复用 `MafWfSseReader` 的 HttpClient 流式实现，实测增量事件、取消/关标签断流、代理无缓冲、过期 401，且不自动重放 SSE POST。
5. 保留分块上传、哈希、取消/续传；改造 `DownloadProgressDialog` 的 MemoryStream 全量缓冲，优先浏览器流式落盘，不支持时使用有明确大小上限的分块 Blob 降级。保留 Range/206、进度和错误处理。

**退出门槛：** 全业务路由对照通过；大文件记录内存曲线，不再默认整文件进入 .NET 内存；SSE 保存逐事件时间戳 trace；关键页面性能达到预算，否则先裁剪/懒加载，不进入全量管理迁移。

### P4：完整身份管理迁移

1. 在管理 API 逐模块补齐角色、组、客户端、客户端作用域、身份提供商、用户联邦、认证流、Realm 设置/密钥、组织、事件、导入导出和审计端点。
2. 复用 AdminService、审计、备份、最后管理员保护和 Step-Up；纯 DTO 如仍在服务端程序集则移至 Shared，保持命名空间与 JSON 结构。
3. 迁移全部管理 Razor 页面和对话框，优先以 HTTP 适配现有服务接口，移除 UI 对服务端 ActorAccessor、DataProtection 和 IDistributedCache 的直接依赖。
4. 为每个高风险写操作补齐匿名、非管理员、陈旧认证、审计失败和最后管理员等负向测试。

**退出门槛：** 46 个宿主页面文件、59 条宿主/RCL 路由和 79 处注入均有迁移或明确退役结论；管理功能等价，权限不只靠 UI 隐藏；共享 bUnit、管理 API 集成测试和浏览器 E2E 全部通过或明确记录既有失败。

### P5：安全切换、灰度与旧 Server 退役

1. 双跑期使用独立入口，按已验收路由逐步开放 WASM；新旧系统共用业务数据和 Keycloak，不复制用户体系。
2. 确认旧 Server 不再承载业务流量后，再打开业务 API 的 `RequireAuthenticatedApi`，并按端点启用 `[RequireSpaceAccess]`；该步骤与旧 Server 退役绑定。
3. 最终入口切至 WASM；回退通过恢复旧入口和 API 兼容开关完成，audience/CORS 增量配置无需回滚。明确灰度入口、回退责任人和旧认证链退役条件。
4. 全部验收后删除旧 Server 的 Cookie/OIDC、Circuit 认证状态和 Redis AccessTokenStore；Redis 仍保留给 API 撤销和 Step-Up。
5. `treegraph-web` client/audience 不因旧 Server 退役立即删除，先确认 MAUI 等其他消费者；Keycloak Bootstrap 和 IdentityAdminDb 迁移保持单点执行。

## 7. 验证与证据

### 自动化

```powershell
dotnet test "TreeGraph.Blazor.Tests\TreeGraph.Blazor.Tests.csproj"
dotnet test "TreeGraph.Blazor.Shared.Tests\TreeGraph.Blazor.Shared.Tests.csproj"
dotnet test "TreeGraph.IdentityAdmin.Tests\TreeGraph.IdentityAdmin.Tests.csproj"
dotnet test "TreeGraph.Api.Tests\TreeGraph.Api.Tests.csproj"
dotnet test "TreeGraph.MafWfApi.Tests\TreeGraph.MafWfApi.Tests.csproj"
dotnet test "TreeGraph.FileStorageApi.Tests\TreeGraph.FileStorageApi.Tests.csproj"
dotnet test "TreeGraph.IdentityAdmin.Api.Tests\TreeGraph.IdentityAdmin.Api.Tests.csproj"
dotnet test "TreeGraph.Blazor.E2E.Tests\TreeGraph.Blazor.E2E.Tests.csproj"
dotnet publish "TreeGraph.Blazor.WebHost\TreeGraph.Blazor.WebHost.csproj" -c Release
```

新增管理 API 测试项目；浏览器认证单元测试放在 WASM 对应测试项目，E2E 优先扩展现有 `TreeGraph.Blazor.E2E.Tests`，不重复建设测试基建。

### 端到端、安全与性能验收

- 用户启动 Aspire 后，验证登录、刷新、会话过期、多标签登出、IdP 全局登出、非管理员 403、Step-Up 和禁用账号撤销。
- 保存 Playwright trace、脱敏网络请求及测试输出；证明无应用认证 Cookie、无 URL token、无持久化 token、无管理密钥。
- 验证旧 `treegraph-web` 与新 `treegraph-wasm` token 的兼容；业务 API 在最终切换后拒绝匿名。
- 记录 Release 首屏传输量、冷启动、关键路由性能、树/EAV 渲染内存，以及大文件内存曲线和 SSE 逐事件时间戳。
- 未实测项标记“未验证”，不写“已通过”。

## 8. 风险矩阵与最终完成定义

| 风险 | 可能性 × 影响 | 控制 |
|---|---|---|
| XSS 窃取浏览器 token | 中 × 高 | 仅内存、严格 CSP、依赖审计、敏感日志扫描 |
| 新管理 API 越权或绕过 Step-Up | 中 × 高 | 全端点服务端鉴权、负向集成测试、审计失败关闭 |
| audience/CORS 改动影响旧 Server 或 MAUI | 中 × 高 | 双 audience、旧 token 回归、入口可回退 |
| WASM 大文件全量缓冲或 SSE 不增量 | 中 × 高 | 下载流式改造、内存曲线与事件时间戳专项验收 |
| 多标签刷新/登出竞态 | 中 × 中 | 刷新单航班、单次重试、BroadcastChannel、IdP 会话测试 |
| 页面/管理注入遗漏导致无法退役 | 中 × 高 | 路由与注入清单作为切换门禁 |
| `/config.json` 或发布产物泄露密钥 | 低 × 高 | 公共字段白名单、产物扫描、密钥仅注入管理 API |
| 首屏与大数据渲染性能回退 | 中 × 中 | P1 实测预算、裁剪、懒加载和大数据专项测试 |

最终完成定义：

- 全部业务与管理页面在 WASM 等价可用，旧 Server 不再承载流量。
- 无应用认证 Cookie、无 Blazor Circuit 依赖、无浏览器管理密钥；业务与管理 API 均执行服务端 JWT 授权。
- 审计、备份、最后管理员、Step-Up、撤销和空间授权均有可追踪测试证据。
- 文件、SSE、性能、多标签和登出专项验收完成；灰度入口及回退路径验证有效。
- 只有用户明确说“完美”后，才提交与推送。
