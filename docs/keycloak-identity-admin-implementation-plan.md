# Keycloak 身份与用户管理平台 — 实施方案（基于 Aspire 17 升级后基线）

> 目标：在 TreeGraph Blazor 管理台内完成全部 realm 级身份与访问管理操作；Keycloak 作为身份引擎，不要求管理员日常进入 Keycloak Admin Console。
>
> 已定决策：空间成员使用 Keycloak 组；用户仅禁用不物理删除；管理调用使用单 service account + 高危操作二次认证（step-up）；**本期固定 Keycloak 26.8.2，升级门禁并入 P0**。
>
> 适用范围：`TreeGraph.Blazor`、`TreeGraph.Api`、`TreeGraph.MafWfApi`、`APromisedLand.AppHost`、`TreeGraph.Shared`。
>
> 文档状态：已按 2026-11-22 的 Aspire 17 / EF Core 11 升级结果重写。本文只描述实施方式，不含已落地代码。

---

## 0. 升级后基线（2026-11-22 实测）

本节是本次重写的依据。所有条目均来自仓库与本地包缓存的直接核对，不含推断。

### 0.1 运行时与依赖

| 项 | 实际版本 |
|---|---|
| 目标框架 | `net11.0` |
| EF Core / Npgsql EF | 11.0.0 |
| Aspire 稳定包 | 17.0.0 |
| Aspire 预览包 | 17.0.0-preview.1.26567.5（Keycloak / Blazor / Foundry / Maui / Npgsql） |
| Aspire.Hosting.Elasticsearch | 13.3.0（无 17.x 发布，保持） |
| WolverineFx 系列 | 6.64.2 |
| Roslyn 组件 | 5.12.0（TreeGraph.Api、QuestionService 显式引用 Workspaces 子包以消除 NU1608） |
| 构建结果 | 0 错误 / 451 警告 |

### 0.2 Aspire 17 带来的三项变化

1. **Keycloak 默认镜像已变为 `26.8`**。
   实测：`Aspire.Hosting.Keycloak 17.0.0-preview.1.26567.5` 程序集内默认 tag 为 `26.8`（旧版为 26.6）。因此本方案不再需要"从 26.6 升级"的迁移动作，改为"在编排中显式固定 26.8.2"。

2. **`AddKeycloak` 已支持参数化管理员凭据**。
   实测签名：`AddKeycloak(string name, int? port, IResourceBuilder<ParameterResource>? adminUserName, IResourceBuilder<ParameterResource>? adminPassword)`；另有 `WithRealmImport(string)`、`WithDataVolume(string)`、`WithOtlpExporter()`。这使 §4 的"密钥外置"可以直接在编排层完成。

3. **AppHost SDK 与 `AddProject` 已标记过时**。
   - `Aspire.AppHost.Sdk/17.0.0` 触发 `ASPIRE012`（×2）：官方建议改用 `Microsoft.NET.Sdk` + `AddDotnetProject`。
   - `AddProject<T>(name)` 触发 `CS0618`（25 处）：建议改用 `AddDotnetProject(name, path)`。
   - **本方案决策**：本期不迁移。理由：迁移会同时改动 20+ 编排扩展文件，与身份边界改造无耦合，风险叠加。新增的 Keycloak 相关编排沿用现有 `AddProject` 风格，SDK 迁移单列为后续技术债。

### 0.3 现有代码事实（与旧方案的差异）

| 位置 | 当前事实 |
|---|---|
| [AppHost.cs](file:///d:/APromisedLand/APromisedLand.AppHost/AppHost.cs) | `TreeGraph()` 拓扑**未声明 Keycloak**；`AddKeycloak` 仅存在于未启用的 `AddService()` 注释中 |
| [KeycloakExtension.cs](file:///d:/APromisedLand/APromisedLand.AppHost/Extensions/KeycloakExtension.cs) | 仅 3 行：`AddKeycloak("Keycloak", 8323).WithDataVolume("keycloak-data").WithOtlpExporter()`；无镜像固定、无 realm 导入、无凭据参数 |
| [PostgresExtension.cs](file:///d:/APromisedLand/APromisedLand.AppHost/Extensions/PostgresExtension.cs) | 已建 7 个库，**无 `IdentityAdminDb`** |
| [KeycloakRealmpart.json](file:///d:/APromisedLand/APromisedLand.AppHost/KeycloakRealmpart.json) | `registrationAllowed: true`；明文 `secret: "diberysky-secret"`；明文用户密码 `Pa$$w0rd`；**组映射器写入 `claim.name: "roles"`**（与角色 claim 冲突）；无 `/spaces` 组；无 `kc-admin-api` scope；仅 1 个客户端 |
| [docker/.env](file:///d:/APromisedLand/docker/.env) | 仅有 `KEYCLOAK_PASSWORD=...` 明文（对应 Aspire 参数名 `Keycloak-password`） |
| [TreeGraph.Blazor/Program.cs](file:///d:/APromisedLand/TreeGraph.Blazor/Program.cs) | 无任何认证/授权注册；无 Redis；`MapRazorComponents<App>()` 仅注册 `SpaceSky` 程序集 |
| [Routes.razor](file:///d:/APromisedLand/TreeGraph.Blazor/Components/Routes.razor) | 使用 `RouteView`，非 `AuthorizeRouteView` |
| [TreeGraph.Api/Program.cs](file:///d:/APromisedLand/TreeGraph.Api/Program.cs) | `UseAuthorization()` 被注释（L160-163）；无 JwtBearer；无 Redis 引用；`AddNpgsqlDbContext<TreeGraphDbContext>("TreeGraphDb")` |
| [TreeGraph.MafWfApi/Program.cs](file:///d:/APromisedLand/TreeGraph.MafWfApi/Program.cs) | 已有 `AddRedisDistributedCache("Redis")`；仅有 `McpApiKeyMiddleware`，无 JwtBearer |
| [TreeGraph.Blazor.Maui.csproj](file:///d:/APromisedLand/TreeGraph.Blazor.Maui/TreeGraph.Blazor.Maui.csproj) | **引用 `TreeGraph.Blazor.Shared`**，目标 `net11.0-android/ios/maccatalyst/windows` |

### 0.4 相对旧方案必须修正的三处

1. **不能把 `TreeGraph.IdentityAdmin` 挂到 `TreeGraph.Blazor.Shared` 上。**
   MAUI 工程引用了该 RCL。若 RCL 引用含 EF Core / Npgsql / Redis 的类库，依赖会传递进 Android/iOS 目标。修正：RCL 只依赖 `TreeGraph.Shared`。

2. **realm JSON 当前的组映射器写错了 claim 名。**
   现状 `oidc-group-membership-mapper` 的 `claim.name` 为 `roles`，会与 realm role 映射互相覆盖。修正：组 → `groups`，角色 → `roles`，两者分离。

3. **`kc-admin-api` 不写进 realm JSON。**
   该 scope 由 Keycloak 26.8.2 自动创建，realm JSON 导入顺序无法保证其已存在。修正：由 bootstrap 步骤经 Admin API 幂等分配并校验。

---

## 1. 目标与非目标

### 1.1 目标

1. Blazor 管理台覆盖：用户、角色、组（含空间组）、客户端、client scope、protocol mapper、身份提供方、LDAP/AD 用户联邦、认证流、realm 设置、会话、事件、审计、realm 导出。
2. 所有管理请求经服务端 BFF 调 Keycloak Admin API，管理令牌不进入浏览器。
3. 建立 `sub → 会话/附件` 归属、`groups → SpaceIds` 空间隔离、`roles → 管理权限` 三段授权链。
4. 禁用用户 = 禁用 + 吊销会话 + 撤销标记，UI 不提供物理删除。
5. 所有管理写操作可审计，失败尝试也留痕。
6. 现有明文凭据、未导入 realm、claim 名冲突在 P0 一并清理。

### 1.2 非目标

- 不提供 realm 删除、内置客户端删除、已硬删除用户恢复。
- 不通过 UI 上传/部署登录主题与邮件模板（Keycloak 无对应 Admin API），只允许选择已部署主题。
- 不通过 UI 修改 Keycloak 服务端启动配置（hostname、TLS、数据库、feature flags）。
- 不在 MAUI 端提供管理功能；MAUI 仅保留登录与业务访问。
- 本期不做 AppHost SDK 迁移（`ASPIRE012`）。

---

## 2. 总体架构

```text
浏览器 (Blazor Server 5783)
  ├─ Cookie 登录态（OIDC Authorization Code + PKCE）
  ├─ /admin/* 页面（需要 app-admin）
  ├─ /step-up 二次认证页
  └─ 服务端服务层
        ├─ IAdminActorAccessor       从 ClaimsPrincipal 构造 AdminActor
        ├─ IUsersAdminService 等     所有写操作强制携带 AdminActor
        ├─ IAdminTokenProvider       client_credentials 令牌缓存 + 单飞
        ├─ IAdminAuditStore          审计写入（独立库 IdentityAdminDb）
        └─ IStepUpService            auth_time 新鲜度 + 一次性操作票据
              │
              ▼
        Keycloak Admin REST API (26.8.2)
              ▲
              │ 用户令牌
TreeGraph.Api (5773) / MafWfApi (5323)
  ├─ JwtBearer（roles / groups claim）
  ├─ ICurrentUser（UserId / Roles / SpaceIds）
  ├─ 空间授权过滤器（403）
  └─ 撤销标记校验（Redis）
```

### 2.1 程序集分层（关键修正）

```text
TreeGraph.Shared
  └─ Identity/                       ← 契约层，零第三方依赖
       ├─ ICurrentUser.cs
       ├─ IdentityClaimTypes.cs
       └─ Admin/                     ← 管理 DTO + 服务接口
            ├─ IUsersAdminService.cs
            ├─ IRolesAdminService.cs
            └─ ...

TreeGraph.IdentityAdmin              ← 实现层，仅被 TreeGraph.Blazor 引用
  ├─ Options / Auth / Token / Audit / Keycloak / Modules
  └─ Extensions/IdentityAdminServiceCollectionExtensions.cs

TreeGraph.Blazor.Shared
  └─ IdentitySky/Pages/              ← 仅依赖 TreeGraph.Shared，注入 Admin/* 接口
```

这条分层是硬约束：RCL 被 MAUI 引用，因此接口与 DTO 必须落在 `TreeGraph.Shared`，EF Core / Npgsql / Redis 实现只能落在 `TreeGraph.IdentityAdmin`。

### 2.2 其它约束

- 管理令牌只存在于服务端内存缓存。
- 浏览器的 `Keycloak:Authority` 必须是外部可达地址（开发环境 `http://localhost:8323`），不能使用容器内服务发现地址；管理 API 调用可以使用内部地址。
- Blazor 的 Cookie 与 Keycloak 会话是两套生命周期，必须通过撤销标记与短 Cookie 保持一致。

---

## 3. Keycloak 设计

### 3.1 Realm

| 配置 | 值 |
|---|---|
| Realm | `apromisedland` |
| 自助注册 | 关闭（`registrationAllowed=false`） |
| 暴力破解保护 | 开启 |
| 密码策略 | 长度 ≥ 12，含大小写/数字/符号 |
| Access Token 寿命 | 10 分钟 |
| SSO Session Idle | 30 分钟 |
| SSO Session Max | 8 小时 |
| 默认组 | `/diberysky` |
| 主题 | 只允许选择已部署主题 |

### 3.2 客户端

| clientId | 类型 | 用途 | 关键配置 |
|---|---|---|---|
| `treegraph-web` | confidential | Blazor Server 登录 | 标准流 + PKCE；redirect `http://localhost:5783/signin-oidc`；post-logout `.../signout-callback-oidc`；secret 由 Aspire 参数注入 |
| `treegraph-maui` | public | MAUI 登录 | 标准流 + PKCE；自定义 URL scheme |
| `treegraph-admin` | confidential | 管理 API 调用 | service accounts enabled；关闭标准流；secret 参数化；**默认 client scope 含 `kc-admin-api`**（26.8.2 必需）；P2 起可切 `private_key_jwt` |
| `account` / `admin-cli` / `broker` / `realm-management` | 内置 | 保持 | UI 只读，不提供删除 |

`treegraph-admin` 的 service account 需要 realm-management 角色：

`view-realm`、`manage-realm`、`view-users`、`query-users`、`manage-users`、`view-clients`、`manage-clients`、`view-identity-providers`、`manage-identity-providers`、`view-events`、`manage-events`、`query-groups`。

### 3.3 角色与组模型

```text
realm roles
  ├─ app-admin     管理台入口 + 高危操作
  ├─ app-editor    业务写权限
  └─ app-viewer    业务只读

groups
  ├─ /Admin
  ├─ /diberysky             默认组
  └─ /spaces
       ├─ space-{spaceId}
       └─ ...
```

- 角色只使用 realm roles；不引入 client roles 作为业务权限。
- 空间成员 = `/spaces/space-{id}` 组成员。
- 新建空间时创建对应组；空间删除时由业务侧发起解绑，不从通用组管理页直接删。
- 新用户默认只加入 `/diberysky`，不自动加入任何空间。

### 3.4 Protocol Mapper（含现状缺陷修正）

现状 [KeycloakRealmpart.json](file:///d:/APromisedLand/APromisedLand.AppHost/KeycloakRealmpart.json#L26-L41) 只有一个 `oidc-group-membership-mapper`，却把 `claim.name` 设为 `roles`，导致组与角色共用一个 claim。必须拆成两条：

| Mapper | 类型 | 关键配置 |
|---|---|---|
| 角色映射 | `oidc-usermodel-realm-role-mapper` | `claim.name=roles`；multivalued；access token 与 userinfo 输出 |
| 组映射 | `oidc-group-membership-mapper` | `claim.name=groups`；`full.path=false`；access token 输出 |
| 可选 audience | 保持现有 mapper，或改为 `treegraph-web` |  |

后端规则：

```text
Roles    = token["roles"]
SpaceIds = token["groups"].Where(g => g.StartsWith("space-"))
```

标准 group mapper 不支持按前缀过滤；不在 mapper 层裁剪。空间数量增长后的处理见 §6.6。

### 3.5 Realm 配置应用策略

`--import-realm` 只在 realm 不存在时导入，**不会更新已有 realm**。实施规则：

- 开发环境：realm 结构变更时删除 `keycloak-data` 卷后重建；
- 首次导入：`WithRealmImport` 挂载版本化 realm JSON；
- 后续变更：由 bootstrap 步骤通过 Admin API 幂等应用（客户端、角色、组、mapper、realm 设置），不依赖 import；
- 生产环境：使用 keycloak-config-cli 或等价工具，版本化配置入仓；
- 禁止在 realm JSON 中写明文用户密码与固定 client secret；密钥由 Aspire 参数注入，导入后通过 Admin API 设置。

### 3.6 Keycloak 26.8.2 固定与门禁

**事实更新**：Aspire 17 的 `Aspire.Hosting.Keycloak 17.0.0-preview.1.26567.5` 默认镜像已是 `26.8`，不再存在"从 26.6 被动继承"的问题。本期改为在编排中显式固定 `26.8.2`，杜绝后续漂移。

编排写法（Keycloak 扩展）：

```csharp
var adminUser = builder.AddParameter("Keycloak-username", "admin");
var adminPassword = builder.AddParameter("Keycloak-password", secret: true);

resourceContext.Keycloak = builder.AddKeycloak("Keycloak", 8323, adminUser, adminPassword)
    .WithImageTag("26.8.2")
    .WithDataVolume("keycloak-data")
    .WithRealmImport(realmJsonPath)
    .WithOtlpExporter();
```

升级前置：

1. 开发环境直接删除 `keycloak-data` 卷重建（当前 realm 未正式导入）。
2. 显式固定镜像 `26.8.2`，启动后通过版本端点确认实际版本。
3. 升级窗口内暂停管理操作，禁止混版本运行。

P0 门禁：

1. **Admin API audience**：26.8.2 内置 `kc-admin-api` scope 自动创建。由 bootstrap 步骤将该 scope 分配为 `treegraph-admin` 的 default client scope，并验证：
   - token 的 scope 含 `kc-admin-api`；
   - token 的 `aud` 含 `{frontendUrl}/admin/realms/apromisedland`；
   - 调用 `GET /admin/realms/{realm}/users?max=1` 返回 200。
2. **Protocol mapper 权限**：26.8.2 起 mapper CRUD 默认需要 `manage-realm`；`treegraph-admin` 已具备，不受影响。若未来拆分"仅 manage-clients 的委托管理员"，必须在 Realm settings 配置 **Allowed protocol mapper types for client administrators**（默认空列表）。
3. **回归**：用户、客户端、组、mapper、认证流 CRUD，realm 导出，登录/登出全部通过。
4. **滚动升级**：旧节点签发的管理令牌缺少新 audience，路由到新节点会 401；升级窗口内暂停管理操作。

---

## 4. 配置与密钥

[TreeGraph.Blazor/appsettings.json](file:///d:/APromisedLand/TreeGraph.Blazor/appsettings.json) 增加：

```json
{
  "Keycloak": {
    "Authority": "http://localhost:8323/realms/apromisedland",
    "AdminBaseUrl": "http://localhost:8323",
    "Realm": "apromisedland",
    "WebClientId": "treegraph-web",
    "AdminClientId": "treegraph-admin",
    "RequireHttpsMetadata": false,
    "StepUpMaxAgeSeconds": 300,
    "SpaceGroupPrefix": "space-"
  }
}
```

密钥处理：

- `WebClientSecret`、`AdminClientSecret` 由 Aspire 参数注入为 `Keycloak__WebClientSecret`、`Keycloak__AdminClientSecret`，不写 appsettings；
- [docker/.env](file:///d:/APromisedLand/docker/.env) 的明文 `KEYCLOAK_PASSWORD` 改为本地用户机密或未跟踪的 env 文件；
- 服务发现键名：`AddKeycloak` 暴露的是 `http` 端点，配置统一使用 `http`，不再出现 `https:0`；
- Blazor 通过 `WithReference(Keycloak)` 获取服务发现，但 `Authority` 仍写外部地址。

后端（Api / MafWfApi）新增：

```json
{
  "Keycloak": {
    "Authority": "http://localhost:8323/realms/apromisedland",
    "Audience": "treegraph-web",
    "RequireHttpsMetadata": false
  }
}
```

---

## 5. 数据模型

使用独立数据库 `IdentityAdminDb`（同一 Postgres 实例，独立迁移历史），避免把管理审计耦合进 `TreeGraphDbContext`。

### 5.1 admin_audit_log

| 列 | 类型 | 说明 |
|---|---|---|
| id | uuid | 主键 |
| occurred_at | timestamptz | 发生时间 |
| actor_sub | text | 真实管理员 sub |
| actor_name | text | preferred_username |
| service_client_id | text | 固定 `treegraph-admin` |
| action | text | 如 `user.disable`、`client.create` |
| target_type / target_id | text | 操作对象 |
| request_json | jsonb | 脱敏后的入参 |
| result | text | pending / success / partial / failed |
| keycloak_status | int | HTTP 状态码 |
| correlation_id | text | 请求关联 ID |
| error | text | 错误摘要（不含凭据） |

### 5.2 撤销标记

Redis：`identity:revoked:{sub}`，值为撤销时间，TTL = 24 小时（覆盖 Cookie 与令牌最大寿命）。

- 禁用时写入；
- 重新启用时删除；
- Blazor 与 API 在读路径校验。

### 5.3 迁移工具前置

本机 `dotnet-ef` 为 10.0.12，低于 EF Core 11.0.0。生成迁移前须先 `dotnet tool update --global dotnet-ef` 到 11.x，否则工具与运行时版本不匹配。

---

## 6. 服务端实现

### 6.1 新增项目 `TreeGraph.IdentityAdmin`

类库（`Microsoft.NET.Sdk`，`net11.0`），被 `TreeGraph.Blazor` 引用，不被 `TreeGraph.Api` / `TreeGraph.Blazor.Shared` 引用。

```text
TreeGraph.IdentityAdmin/
  Options/KeycloakAdminOptions.cs
  Auth/AdminActor.cs
  Auth/IAdminActorAccessor.cs
  Auth/AdminActorAccessor.cs
  Token/IAdminTokenProvider.cs
  Token/AdminTokenProvider.cs
  Token/KeycloakUnauthorizedException.cs
  Audit/AdminAuditLog.cs
  Audit/IdentityAdminDbContext.cs
  Audit/IAdminAuditStore.cs
  Audit/AdminAuditStore.cs
  Auditing/AdminOperationRunner.cs
  Keycloak/KeycloakAdminClient.cs
  Revocation/IIdentityRevocationStore.cs
  Revocation/RedisIdentityRevocationStore.cs
  StepUp/IStepUpService.cs
  StepUp/StepUpService.cs
  Bootstrap/KeycloakBootstrapService.cs
  Modules/UsersAdminService.cs
  Modules/RolesAdminService.cs
  Modules/GroupsAdminService.cs
  Modules/ClientsAdminService.cs
  Modules/ClientScopesAdminService.cs
  Modules/IdentityProvidersAdminService.cs
  Modules/FederationAdminService.cs
  Modules/AuthFlowsAdminService.cs
  Modules/RealmSettingsAdminService.cs
  Modules/EventsAdminService.cs
  Modules/SessionsAdminService.cs
  Modules/ExportAdminService.cs
  Modules/DashboardAdminService.cs
  Extensions/IdentityAdminServiceCollectionExtensions.cs
```

依赖包：`Microsoft.EntityFrameworkCore` 11.0.0、`Npgsql.EntityFrameworkCore.PostgreSQL` 11.0.0、`Microsoft.Extensions.Http` 11.0.0、`Microsoft.Extensions.Options.ConfigurationExtensions` 11.0.0。项目引用：`TreeGraph.Shared`。

### 6.2 令牌提供器

- `client_credentials` 获取令牌；无 refresh token，"刷新"即重新请求；
- 缓存键固定，`SemaphoreSlim` 单飞覆盖**获取与刷新**两条路径；缓存有效期 = `expires_in - 60s`；
- Admin API 返回 401 时强制刷新一次并重试原请求一次，仍失败则抛出 `KeycloakUnauthorizedException`；
- 不记录令牌内容；指标记录获取次数、失败次数、令牌年龄。

### 6.3 AdminActor 与审计强制

```csharp
public sealed record AdminActor(string Sub, string Username, IReadOnlyList<string> Roles);

public interface IAdminActorAccessor
{
    AdminActor GetRequiredActor(); // 仅从 ClaimsPrincipal 构造
}
```

- 所有写方法第一个参数为 `AdminActor actor`；
- 不提供任何接收 `actorSub` 字符串的 HTTP 端点；
- 所有写方法经 `AdminOperationRunner.RunAsync(actor, action, target, request, () => ...)`：
  1. 生成 correlationId；
  2. 调用 Keycloak 前先写 `pending` 意图记录；审计库不可达时**拒绝执行**（fail-closed）；
  3. 执行 Keycloak 调用；
  4. 成功/失败/部分成功都更新审计；
  5. 后置更新失败时写 OTel + 本地文件兜底并告警。

### 6.4 Blazor 认证

修改 [TreeGraph.Blazor/Program.cs](file:///d:/APromisedLand/TreeGraph.Blazor/Program.cs)：

- `AddAuthentication`：默认 Cookie + OIDC challenge；
- `AddOpenIdConnect`：`ResponseType=code`、`UsePkce=true`、`GetClaimsFromUserInfoEndpoint=true`、`SaveTokens=false`、`RoleClaimType="roles"`；
- `OnTokenValidated`：读取 `auth_time` 写入自定义 claim（默认映射不保证保留原键名）；
- `AddAuthorization`：策略 `AdminOnly` = `RequireRole("app-admin")`；
- `AddCascadingAuthenticationState()`；
- `AddRedisDistributedCache("Redis")` 供撤销标记与 step-up 票据；
- `IdentityAdminServiceCollectionExtensions.AddIdentityAdmin(...)` 注册全部模块。

新增包引用：`Microsoft.AspNetCore.Authentication.OpenIdConnect` 11.0.0、`Aspire.StackExchange.Redis.DistributedCaching` 17.0.0。

修改 [Routes.razor](file:///d:/APromisedLand/TreeGraph.Blazor/Components/Routes.razor)：

- `AuthorizeRouteView` + `CascadingAuthenticationState` 替代 `RouteView`；
- `AdditionalAssemblies` 保持不变（`IdentitySky` 与 `SpaceSky` 同属 `TreeGraph.Blazor.Shared`，已注册）。

修改 [App.razor](file:///d:/APromisedLand/TreeGraph.Blazor/Components/App.razor)：`Routes` 保持 `InteractiveServer`，认证状态经 `AddCascadingAuthenticationState()` 级联。

管线顺序：`UseAuthentication()` 必须位于 `UseAuthorization()` 与 `UseAntiforgery()` 之前。

### 6.5 Step-up（二次认证）

- `IStepUpService.IsFresh(AdminActor)`：比较 `auth_time` 与当前时间，默认 5 分钟；
- 不满足时，页面跳转 `/step-up`，把待执行操作写入 Redis 一次性票据（actorSub、操作名、参数、returnUrl、5 分钟过期、单次消费）；
- `/step-up` 触发 `Challenge(prompt=login)` 重新登录；返回后按票据执行原操作；
- 高危操作清单：删除/禁用管理员、删除组/客户端、改认证流绑定、改 SMTP/联邦、密钥轮换、realm 导出配置修改；
- 不对所有写操作统一要求 step-up，按操作分级。

### 6.6 空间授权

`TreeGraph.Shared/Identity` 增加：

```csharp
public interface ICurrentUser
{
    string UserId { get; }
    IReadOnlyList<string> Roles { get; }
    IReadOnlyList<string> SpaceIds { get; }
}
```

- P0 实现：从 `groups` claim 过滤 `space-` 前缀；
- `groups` mapper 输出到 access token 与 userinfo，**不输出到 ID token**；Blazor 必须启用 `GetClaimsFromUserInfoEndpoint=true` 且 mapper 的 `userinfo.token.claim=true`，否则 `ClaimsPrincipal` 中不会出现 `groups`，`SpaceIds` 恒为空；
- 预留实现：按 `sub` 查询（Admin API 或本地缓存），接口不变；
- 指标：token 大小、组数量；超过阈值（初始 30）时告警，P1 评估切换；
- `TreeGraph.Api` 增加空间授权过滤器：
  - 路由含 `{spaceId}` 的端点直接比较；
  - 节点级端点（subtree/eav/edge by node）通过 `ISpaceResolver` 解析节点所属空间后比较；
  - 无空间维度的列表端点限制为 `app-admin` 或按允许空间过滤。

### 6.7 禁用用户（幂等 saga）

```text
DisableUserAsync(userId, actor):
  1. PUT /users/{id} { enabled: false }        // 必须成功
  2. POST /users/{id}/logout                   // 尽力而为，失败可重试
  3. 清理 offline sessions（如存在）            // 尽力而为
  4. 写 Redis 撤销标记                          // 失败也返回部分成功并告警
  5. 审计：result = success | partial
```

- 返回结构化结果：`{ Disabled, SessionsRevoked, RevocationMarked, Retryable }`；
- 重复调用安全：已禁用继续尝试吊销；
- UI 不提供单独的"仅禁用"按钮；部分成功时展示"会话吊销失败，重试"按钮；
- 重新启用时删除撤销标记，并提示用户需重新登录。

### 6.8 撤销传播

- Blazor：Cookie 有效期 20 分钟、滑动续期；`RevalidatingAuthenticationStateProvider` 每 60 秒检查撤销标记，命中则登出并刷新 circuit 认证状态；
- API：JwtBearer `OnTokenValidated` 检查 Redis 撤销标记（本地缓存 30 秒），命中则拒绝；
- 后续增强：Keycloak back-channel logout 端点。

### 6.9 后端 JwtBearer

`TreeGraph.Api`（新增 Redis 引用）与 `TreeGraph.MafWfApi`（已有 Redis）统一：

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.Authority = configuration["Keycloak:Authority"];
        o.Audience = configuration["Keycloak:Audience"];
        o.RequireHttpsMetadata = false;
        o.TokenValidationParameters.RoleClaimType = "roles";
    });
```

`TreeGraph.Api` 需同时移除 L160-163 的注释并启用 `UseAuthorization()`；`TreeGraph.MafWfApi` 的 `/mcp` 仍保留 `McpApiKeyMiddleware`，不改动。

---

## 7. UI 设计

页面放在 `TreeGraph.Blazor.Shared/IdentitySky/Pages/`，沿用 MudBlazor，注入 `TreeGraph.Shared/Identity/Admin` 的服务接口。

| 路由 | 页面 | 关键能力 |
|---|---|---|
| `/admin` | Dashboard | 用户数、活跃会话、最近审计、Keycloak 健康 |
| `/admin/users` | 用户列表 | 搜索、状态筛选、分页、创建 |
| `/admin/users/{id}` | 用户详情 | 资料、组、角色、会话、重置密码、禁用/启用 |
| `/admin/roles` | 角色 | realm roles CRUD、成员、组合角色 |
| `/admin/groups` | 组 | 组树、成员、组角色、空间组标识 |
| `/admin/spaces` | 空间成员 | 空间组列表、成员增删、禁止删除空间组 |
| `/admin/clients` | 客户端 | 列表、创建向导、设置、凭证轮换 |
| `/admin/clients/{id}` | 客户端详情 | redirect URI、scopes、mappers、service account roles |
| `/admin/identity-providers` | 身份提供方 | OIDC/SAML/社交 CRUD、mapper、同步 |
| `/admin/user-federation` | 用户联邦 | LDAP/AD 配置、测试连接、同步 |
| `/admin/auth-flows` | 认证流 | 列表、复制、执行器、绑定（危险区） |
| `/admin/realm` | Realm 设置 | 令牌、密码策略、SMTP、注册、默认组、主题选择 |
| `/admin/events` | 事件 | 登录/管理事件查询 |
| `/admin/audit` | 审计 | 业务管理审计、筛选、导出 |
| `/admin/export` | 导出 | realm partial export 下载 |
| `/step-up` | 二次认证 | 重新登录并继续待执行操作 |

交互规范：

- 所有列表服务端分页；
- 敏感字段写入后只显示"已设置"，不回显；
- 破坏性操作需输入资源名确认，并展示影响面；
- 写操作完成后重新拉取，不做乐观更新；
- 任何失败展示 Keycloak 状态码映射（400/403/404/409/502）。

导航：在 [NavMenu.razor](file:///d:/APromisedLand/TreeGraph.Blazor/Components/Layout/NavMenu.razor) 增加"身份管理"分组，整体包在 `<AuthorizeView Roles="app-admin">` 内。

---

## 8. 阶段计划与验收

### P0 身份边界

范围：Keycloak 26.8.2 固定与 audience 门禁、realm 应用策略、三个客户端、角色/组/mapper（含 claim 名修正）、密钥外置、Blazor 登录、`AdminOnly` 策略、令牌提供器、`AdminActor`、审计骨架、禁用 saga、撤销标记、后端 JWT 与 `ICurrentUser`。

验收：

- Keycloak 实际版本 26.8.2，镜像 tag 显式固定；
- `treegraph-admin` 的 default client scope 含 `kc-admin-api`；
- 管理令牌 `aud` 含 Admin API 地址，`/users?max=1` 返回 200；
- mapper CRUD 通过；
- realm 重复应用配置不报错；
- 管理员可登录；非 `app-admin` 访问 `/admin` 返回 403；
- 禁用用户后无法再登录，会话被吊销，撤销标记写入；
- 管理令牌不出现在浏览器；
- 仓库中无明文 Keycloak 凭据；
- 审计记录包含 actorSub 与 serviceClientId；审计库不可用时操作被拒绝。

### P1 用户与会话

范围：用户列表/详情/创建/启停/重置密码/会话下线；审计页。
验收：分页搜索可用；创建用户可登录；临时密码首次登录强制修改；所有写操作有审计。

### P2 角色、组、空间

范围：角色 CRUD/分配；组树/成员；空间组管理；后端 space 403；`ICurrentUser.SpaceIds` 生效。
验收：跨 space 访问被拒绝；token 带 `roles`/`groups`；组变更后下次取 token 生效。

### P3 客户端与 scope

范围：客户端列表/创建向导/设置/凭证；client scopes；protocol mappers；service account 角色。
验收：新建标准 OIDC 客户端可完成登录；mapper 修改在 token 中可见。

### P4 身份提供方与联邦

范围：OIDC/SAML/社交 IdP；LDAP/AD 配置、测试连接、同步；SMTP。
验收：LDAP 测试连接成功；重置密码邮件可达；IdP 登录可回到 Blazor。

### P5 Realm 与认证流

范围：认证流、realm 设置、事件、导出。
验收：每次认证流写操作前自动导出并加密存储；导出失败则拒绝写入。

### P6 高级

范围：密钥轮换、Organizations、主题选择、back-channel logout。

---

## 9. 关键流程

### 9.1 登录

```text
未认证访问 /admin → Challenge OIDC → Keycloak 登录 → /signin-oidc
→ 建立 Cookie（roles/groups/auth_time）→ 回到 /admin
→ AdminOnly 策略校验 → 进入页面
```

### 9.2 高危操作 step-up

```text
点击"删除客户端" → IStepUpService.IsFresh=false
→ 写一次性票据到 Redis → 跳 /step-up
→ prompt=login 重新认证 → 回跳 → 校验票据与 actor
→ 执行操作 → 删除票据 → 审计
```

### 9.3 新建空间

```text
业务创建空间 → POST /admin/groups（/spaces/space-{id}）
→ 失败则回滚空间创建（或标记待补偿）
→ 成功则写审计
```

### 9.4 修改认证流

```text
检查最近自动备份时间 → 执行 partial export → 加密上传 SeaweedFS
→ 备份成功 → 执行认证流修改 → 审计
→ 备份失败 → 拒绝操作并提示
```

备份语义：

- partial export 是**结构备份**，不含用户；密码与 client secret 会被掩码为 `*`，不能作为含密钥的完整恢复源；
- client secret 由 Aspire 参数/用户机密独立保管，恢复后需重新注入；
- 导出不含事件、会话、撤销标记，多节点一致性不保证；
- UI"最近备份时间"旁必须提示"结构备份，密钥需单独恢复"。

---

## 10. 安全设计与护栏

1. 管理入口仅 `app-admin`；管理员强制 MFA。
2. 不可变约束：
   - 不能禁用/删除最后一个 `app-admin`；
   - 不能移除自己的 `app-admin`；
   - 不能删除内置客户端；
   - 不提供删除 realm。
3. 服务账号 secret 独立保管；轮换与 step-up 解耦；P2 起切 `private_key_jwt`。
4. 审计不可绕过：写方法只接受 `AdminActor`；统一落审计；失败留痕。
5. 敏感字段脱敏：client secret、SMTP 密码、LDAP bind 凭据、realm 导出文件。
6. 限流与熔断：管理 API 写操作限流；Keycloak 不可用返回 502，不重试非幂等写。
7. 生产环境保护：环境徽标、只读开关、操作窗口。
8. 备份：认证流修改前自动结构备份；备份加密且设保留期；secret 独立管理。

---

## 11. 测试策略

| 层级 | 内容 |
|---|---|
| 单元 | 令牌单飞、401 强制刷新、AdminActor 构造、审计装饰器、禁用 saga 部分成功、auth_time 新鲜度 |
| 集成 | Keycloak 26.8.2 容器（Testcontainers 或 Aspire 资源）：realm 导入幂等、用户/角色/组/客户端 CRUD、mapper 生效、管理令牌 `aud` 与 `kc-admin-api` scope |
| 权限 | 非管理员访问 `/admin` 403；跨 space 403；最后管理员保护；step-up 必需 |
| E2E | Playwright：登录、建用户、分配空间、禁用并验证会话吊销、修改客户端 |
| 安全 | 审计失败兜底、敏感字段不回显、撤销标记生效、导出文件加密 |

---

## 12. 可观测性与运维

指标：

- `keycloak_admin_requests_total{module,status}`
- `keycloak_admin_token_refresh_total{result}`
- `keycloak_token_age_seconds`
- `blazor_auth_step_up_total{action}`
- `admin_audit_write_failures_total`
- `identity_token_groups_count`、`identity_token_size_bytes`

日志：所有管理操作带 `correlationId`；不记录令牌与凭据。

健康检查：`/health/keycloak`（能获取管理令牌 + 读取 realm + 校验实际版本为 26.8.2）。

Runbook：

- break-glass：使用 Keycloak Admin Console 与 Bootstrap Admin 账号；
- realm 恢复：从加密导出恢复，说明 partial import 的局限；
- secret 轮换：更新 Aspire 参数 → 重启实例 → 验证令牌获取；
- 撤销异常：检查 Redis 标记与 Cookie 过期时间。

---

## 13. 文件级改动清单（按升级后实际路径）

| 位置 | 改动 |
|---|---|
| [KeycloakExtension.cs](file:///d:/APromisedLand/APromisedLand.AppHost/Extensions/KeycloakExtension.cs) | `AddKeycloak(name, 8323, adminUser, adminPassword)` + `WithImageTag("26.8.2")` + `WithRealmImport` + `WithOtlpExporter` |
| [AppHost.cs](file:///d:/APromisedLand/APromisedLand.AppHost/AppHost.cs) | `TreeGraph()` 内启用 `builder.AddKeycloak(context)`（须早于 Blazor） |
| [KeycloakRealmpart.json](file:///d:/APromisedLand/APromisedLand.AppHost/KeycloakRealmpart.json) | 去明文；`registrationAllowed=false`；新增 3 客户端、3 角色、`/spaces` 组、roles/groups 双 mapper |
| [PostgresExtension.cs](file:///d:/APromisedLand/APromisedLand.AppHost/Extensions/PostgresExtension.cs) | 增加 `IdentityAdminDb`；`AppHostResourceContext` 增加对应属性 |
| [TreeGraphBlazorExtension.cs](file:///d:/APromisedLand/APromisedLand.AppHost/Extensions/TreeGraphBlazorExtension.cs) | `WithReference(Keycloak)`、`WithReference(Redis)`、注入 `Keycloak__*` 环境变量 |
| `TreeGraphApiExtension.cs`（TreeGraphEavApi） | `WireIfPresent(Redis)`，注入 `Keycloak__Authority/Audience` |
| [TreeGraphMafWfExtension.cs](file:///d:/APromisedLand/APromisedLand.AppHost/Extensions/TreeGraphMafWfExtension.cs) | 注入 `Keycloak__Authority/Audience`（Redis 已接入） |
| [TreeGraph.Blazor/Program.cs](file:///d:/APromisedLand/TreeGraph.Blazor/Program.cs) | OIDC/Cookie/Authorization/step-up/Redis/服务注册；`UseAuthentication/UseAuthorization` |
| [TreeGraph.Blazor/appsettings.json](file:///d:/APromisedLand/TreeGraph.Blazor/appsettings.json) | 新增 `Keycloak` 配置节 |
| [Routes.razor](file:///d:/APromisedLand/TreeGraph.Blazor/Components/Routes.razor) | `AuthorizeRouteView` |
| [NavMenu.razor](file:///d:/APromisedLand/TreeGraph.Blazor/Components/Layout/NavMenu.razor) | 新增"身份管理"分组（`AuthorizeView Roles="app-admin"`） |
| 新增 `TreeGraph.IdentityAdmin` | Options/Auth/Token/Audit/Keycloak/Modules/Bootstrap/StepUp/Revocation |
| `TreeGraph.Shared/Identity` | `ICurrentUser`、`IdentityClaimTypes`、`Admin/*` 接口与 DTO |
| 新增 `TreeGraph.Blazor.Shared/IdentitySky` | 管理页面与组件（仅依赖 `TreeGraph.Shared`） |
| [TreeGraph.Api/Program.cs](file:///d:/APromisedLand/TreeGraph.Api/Program.cs) | JwtBearer、`ICurrentUser`、授权过滤器、撤销校验、启用 `UseAuthorization()` |
| [TreeGraph.MafWfApi/Program.cs](file:///d:/APromisedLand/TreeGraph.MafWfApi/Program.cs) | JwtBearer、`ICurrentUser`、space 403 |
| `TreeGraph.Api.csproj` | 新增 `Aspire.StackExchange.Redis.DistributedCaching` 17.0.0、`Microsoft.AspNetCore.Authentication.JwtBearer` 11.0.0 |
| `TreeGraph.Blazor.csproj` | 新增 `Microsoft.AspNetCore.Authentication.OpenIdConnect` 11.0.0、`Aspire.StackExchange.Redis.DistributedCaching` 17.0.0、`TreeGraph.IdentityAdmin` 项目引用 |
| [docker/.env](file:///d:/APromisedLand/docker/.env) | 移除明文密码 |
| `APromisedLand.sln` | 加入 `TreeGraph.IdentityAdmin`（新增 GUID） |
| 测试项目 | 单元/集成/E2E/安全测试 |

---

## 14. 风险登记

| 风险 | 影响 | 缓解 |
|---|---|---|
| realm 导入不更新已有配置 | 配置看似生效实则未变 | 版本化 bootstrap / 删卷重建；验收加复查 |
| Blazor Cookie 未随 Keycloak 会话失效 | 禁用用户仍可操作 | 短 Cookie + 撤销标记 + 定期重校验；后续 back-channel logout |
| 单 service account 权限过大 | secret 泄露即 realm 失守 | 参数化密钥、网络隔离、step-up、审计、P2 切 private_key_jwt |
| groups claim 膨胀 | token 变大、登录变慢 | 指标 + 阈值告警；P1 切后端查询 |
| 认证流/密钥误操作 | 全站无法登录 | 自动备份 + 二次认证 + 恢复手册 |
| Admin API 权限模型变化 | 某些模块 403 | 26.8.2 门禁已在 P0 验证 |
| 审计写失败 | 追溯缺失 | 两阶段审计 + fail-closed + 兜底告警 |
| 主题/服务端配置无法 UI | 期望落差 | UI 明确"只可选择已部署主题" |
| 26.8.2 管理 API 401 | 全部管理功能失效 | P0 门禁：分配 `kc-admin-api` scope 并验证 `aud`（§3.6） |
| 26.8.2 mapper 权限收紧 | 委托管理员 mapper 操作 403 | 本方案含 `manage-realm` |
| partial export 不含 secret | 恢复后客户端无法登录 | 结构备份 + secret 独立保管与重新注入（§9.4） |
| ID token 组膨胀 | 登出重定向超限 | groups 只进 access token + userinfo |
| 审计库不可用 | 管理操作无留痕 | 两阶段审计 + fail-closed；break-glass 通道 |
| **RCL 被 MAUI 引用** | EF Core/Npgsql 传递进移动端 | 契约与实现分层（§2.1） |
| **Aspire 17 `AddProject` 过时** | 未来 SDK 迁移成本 | 本期不迁移，单列技术债（§0.2） |

---

## 15. 未决事项

1. 管理员 MFA 是否在 P0 强制，还是 P2 再启用。
2. `app-editor` / `app-viewer` 的具体接口权限矩阵。
3. 空间组命名是否需要支持改名（当前约定 `space-{空间ID}` 不可变）。
4. 审计保留期与导出格式（CSV/JSON）。
5. 生产环境 Keycloak 的 TLS 终止位置（Ingress 还是 Keycloak 自身）。
6. AppHost SDK 迁移到 `Microsoft.NET.Sdk` + `AddDotnetProject` 的排期。
