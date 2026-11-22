# Keycloak 身份与用户管理平台 — 完整实施方案

> 目标：在 TreeGraph Blazor 管理台内完成全部 realm 级身份与访问管理操作；Keycloak 作为身份引擎，不要求管理员日常进入 Keycloak Admin Console。
>
> 已定决策：空间成员使用 Keycloak 组；用户仅禁用不物理删除；管理调用使用单 service account + 高危操作二次认证（step-up）；**本期升级到 Keycloak 26.8.2，升级门禁并入 P0**。
>
> 适用范围：`TreeGraph.Blazor`、`TreeGraph.Api`、`TreeGraph.MafWfApi`、`APromisedLand.AppHost`、`TreeGraph.Shared`。
>
> 本文只描述实施方式，不包含本期代码改动。

---

## 1. 目标与非目标

### 1.1 目标

1. Blazor 管理台覆盖：用户、角色、组（含空间组）、客户端、client scope、protocol mapper、身份提供方、LDAP/AD 用户联邦、认证流、realm 设置、会话、事件、审计、realm 导出。
2. 所有管理请求经服务端 BFF 调 Keycloak Admin API，管理令牌不进入浏览器。
3. 建立 `sub → 会话/附件` 归属、`groups → SpaceIds` 空间隔离、`roles → 管理权限` 三段授权链。
4. 禁用用户 = 禁用 + 吊销会话 + 撤销标记，UI 不提供物理删除。
5. 所有管理写操作可审计，失败尝试也留痕。
6. 现有明文凭据、未导入 realm、服务发现键名不一致等问题在 P0 一并清理。

### 1.2 非目标

- 不提供 realm 删除、内置客户端删除、已硬删除用户恢复。
- 不通过 UI 上传/部署登录主题与邮件模板（Keycloak 无对应 Admin API），只允许选择已部署主题。
- 不通过 UI 修改 Keycloak 服务端启动配置（hostname、TLS、数据库、feature flags）。
- 不在 MAUI 端提供管理功能；MAUI 仅保留登录与业务访问。

---

## 2. 总体架构

```text
浏览器 (Blazor Server 5783)
  ├─ Cookie 登录态（OIDC Authorization Code + PKCE）
  ├─ /admin/* 页面（需要 app-admin）
  ├─ /step-up 二次认证页
  └─ 服务端服务层
        ├─ IAdminActorAccessor       从 ClaimsPrincipal 构造 AdminActor
        ├─ IKeycloakAdminFacade      所有写操作强制携带 AdminActor
        ├─ IAdminTokenProvider       client_credentials 令牌缓存 + 单飞
        ├─ IAdminAuditStore          审计写入（独立库）
        └─ IStepUpService            auth_time 新鲜度 + 一次性操作票据
              │
              ▼
        Keycloak Admin REST API
              ▲
              │ 用户令牌
TreeGraph.Api (5773) / MafWfApi (5323)
  ├─ JwtBearer（roles / groups claim）
  ├─ ICurrentUser（UserId / Roles / SpaceIds）
  ├─ 空间授权过滤器（403）
  └─ 撤销标记校验（Redis）
```

关键约束：

- 管理令牌只存在于服务端内存缓存。
- 浏览器的 `Keycloak:Authority` 必须是外部可达地址（开发环境 `http://localhost:8323`），不能使用容器内服务发现地址；管理 API 调用可使用内部地址。
- Blazor 的 Cookie 与 Keycloak 会话是两套生命周期，必须通过撤销标记与短 Cookie 保持一致性。

---

## 3. Keycloak 设计

### 3.1 Realm

| 配置 | 值 |
|---|---|
| Realm | `apromisedland` |
| 自助注册 | 关闭（`registrationAllowed=false`） |
| 暴力破解保护 | 开启 |
| 密码策略 | 长度 ≥ 12，含大小写/数字/符号（按需调整） |
| Access Token 寿命 | 10 分钟 |
| SSO Session Idle | 30 分钟 |
| SSO Session Max | 8 小时 |
| 默认组 | `/diberysky` |
| 主题 | 只允许选择已部署主题 |

### 3.2 客户端

| clientId | 类型 | 用途 | 关键配置 |
|---|---|---|---|
| `treegraph-web` | confidential | Blazor Server 登录 | 标准流 + PKCE；redirect `https://localhost:5783/signin-oidc`；post-logout `.../signout-callback-oidc`；secret 由 Aspire 参数注入 |
| `treegraph-maui` | public | MAUI 登录 | 标准流 + PKCE；自定义 URL scheme |
| `treegraph-admin` | confidential | 管理 API 调用 | service accounts enabled；关闭标准流；secret 参数化；**默认 client scope 含 `kc-admin-api`**（26.8.2 必需）；P2 起可切 `private_key_jwt` |
| `account` / `admin-cli` / `broker` / `realm-management` | 内置 | 保持 | UI 只读，不提供删除 |

`treegraph-admin` 的 service account 需要 realm-management 角色：

`view-realm`、`manage-realm`、`view-users`、`query-users`、`manage-users`、`view-clients`、`manage-clients`、`view-identity-providers`、`manage-identity-providers`、`view-events`、`manage-events`、`query-groups`。

`kc-admin-api` 必须作为 `treegraph-admin` 的 default client scope。由于 realm JSON 导入顺序可能先于内置 scope 创建，实际分配由 P0 bootstrap 步骤通过 Admin API 完成并校验（见 §3.6）。

### 3.3 角色与组模型

```text
realm roles
  ├─ app-admin     管理台入口 + 高危操作
  ├─ app-editor    业务写权限
  └─ app-viewer    业务只读

groups
  ├─ /Admin                 现有
  ├─ /diberysky             默认组
  └─ /spaces
       ├─ space-{spaceId}   每个业务空间一个组
       └─ ...
```

- 角色只使用 **realm roles**；不引入 client roles 作为业务权限。
- 空间成员 = `/spaces/space-{id}` 组成员。
- 新建空间时创建对应组；空间删除时由业务侧发起解绑，不从通用组管理页直接删。
- 新用户默认只加入 `/diberysky`，不自动加入任何空间。

### 3.4 Protocol Mapper

| Mapper | 配置 |
|---|---|
| 角色映射 | 类型 `oidc-usermodel-realm-role-mapper`；`claim.name=roles`；multivalued；access token 与 userinfo 均输出 |
| 组映射 | 类型 `oidc-group-membership-mapper`；`claim.name=groups`；`full.path=false`；access token 输出 |
| 可选 audience | 保持现有 `diberysky` audience mapper，或改为 `treegraph-web` |

后端规则：

```text
Roles    = token["roles"]
SpaceIds = token["groups"].Where(g => g.StartsWith("space-"))
```

说明：标准 group mapper 不支持按前缀过滤；不在 mapper 层做裁剪。空间数量增长后的处理见 §6.6。

### 3.5 Realm 配置应用策略（必须明确）

`--import-realm` 只在 realm 不存在时导入，**不会更新已有 realm**。实施规则：

- 开发环境：realm 结构变更时删除 `keycloak-data` 卷后重建；
- 首次导入：`WithRealmImport` 挂载版本化 realm JSON；
- 后续变更：由 bootstrap 步骤通过 Admin API 幂等应用（客户端、角色、组、mapper、realm 设置），不依赖 import；
- 生产环境：使用 keycloak-config-cli 或等价工具，版本化配置入仓；
- 禁止在 realm JSON 中写明文用户密码与固定 client secret；密钥由 Aspire 参数注入，导入后通过 Admin API 设置。

### 3.6 本期升级到 Keycloak 26.8.2（并入 P0）

决策：**本期升级到 Keycloak 26.8.2**，升级门禁与 P0 同批完成。当前生成产物固定 26.6（[aspire-output/docker-compose.yaml](file:///d:/APromisedLand/APromisedLand.AppHost/aspire-output/docker-compose.yaml#L12-L13)），必须在编排中显式固定 26.8.2，禁止随 Aspire 包升级被动漂移。

升级前置：

1. 备份 Keycloak 数据库；开发环境可直接删除 `keycloak-data` 卷重建（当前 realm 尚未正式导入）。
2. 显式固定镜像 `26.8.2`，启动后通过版本端点确认实际版本。
3. 升级窗口内暂停管理操作，禁止 26.6 与 26.8.2 节点混跑。

P0 门禁：

1. **Admin API audience**：26.8.2 升级后，内置 `kc-admin-api` scope 会自动创建。由 bootstrap 步骤将该 scope 分配为 `treegraph-admin` 的 default client scope，并验证：
   - token 的 scope 含 `kc-admin-api`；
   - token 的 `aud` 含 `{frontendUrl}/admin/realms/apromisedland`；
   - 调用 `GET /admin/realms/{realm}/users?max=1` 返回 200。
2. **Protocol mapper 权限**：26.8.2 起 mapper CRUD 默认需要 `manage-realm`；`treegraph-admin` 已具备，不受影响。若未来拆分“仅 manage-clients 的委托管理员”，必须在 Realm settings 配置 **Allowed protocol mapper types for client administrators**（默认空列表）。
3. **回归**：用户、客户端、组、mapper、认证流 CRUD，realm 导出，登录/登出全部通过。
4. **滚动升级**：旧节点签发的管理令牌缺少新 audience，路由到新节点会 401；升级窗口内暂停管理操作。

---

## 4. 配置与密钥

`TreeGraph.Blazor/appsettings.json` 增加：

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

- `WebClientSecret`、`AdminClientSecret` 走 Aspire 参数/用户机密，不写 appsettings；
- 清理 [docker/.env](file:///d:/APromisedLand/docker/.env) 中的明文 Keycloak 管理密码；
- 修正 `services:Keycloak:https:0` 与实际 `http` 端点键名不一致；
- `AddKeycloak` 启用后，Blazor 通过 `WithReference(Keycloak)` 获取服务发现；但 `Authority` 仍使用外部地址。

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
| result | text | success / partial / failed |
| keycloak_status | int | HTTP 状态码 |
| correlation_id | text | 请求关联 ID |
| error | text | 错误摘要（不含凭据） |

### 5.2 撤销标记

Redis：`identity:revoked:{sub}`，值为撤销时间，TTL = 24 小时（覆盖 Cookie 与令牌最大寿命）。

- 禁用时写入；
- 重新启用时删除；
- Blazor 与 API 在读路径校验。

---

## 6. 服务端实现

### 6.1 新增项目 `TreeGraph.IdentityAdmin`

类库，被 `TreeGraph.Blazor` 引用，不被 `TreeGraph.Api` 引用。

```text
TreeGraph.IdentityAdmin/
  Options/KeycloakAdminOptions.cs
  Auth/AdminActor.cs
  Auth/IAdminActorAccessor.cs
  Auth/AdminActorAccessor.cs
  Token/IAdminTokenProvider.cs
  Token/AdminTokenProvider.cs
  Audit/AdminAuditLog.cs
  Audit/IdentityAdminDbContext.cs
  Audit/IAdminAuditStore.cs
  Audit/AdminAuditStore.cs
  Auditing/AdminOperationRunner.cs
  Keycloak/KeycloakAdminClient.cs
  Modules/UsersAdminService.cs
  Modules/RolesAdminService.cs
  Modules/GroupsAdminService.cs
  Modules/ClientsAdminService.cs
  Modules/IdentityProvidersAdminService.cs
  Modules/FederationAdminService.cs
  Modules/AuthFlowsAdminService.cs
  Modules/RealmSettingsAdminService.cs
  Modules/EventsAdminService.cs
  Extensions/IdentityAdminServiceCollectionExtensions.cs
```

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

- facade 所有写方法第一个参数为 `AdminActor actor`；
- 不提供任何接收 `actorSub` 字符串的 HTTP 端点；
- 所有写方法经 `AdminOperationRunner.RunAsync(actor, action, target, request, () => ...)`：
  1. 生成 correlationId；
  2. 执行 Keycloak 调用；
  3. 成功/失败/部分成功都写审计；
  4. 审计采用两阶段写入：调用 Keycloak 前先写 `pending` 意图记录；审计库不可达时**拒绝执行**（fail-closed）；调用完成后更新为 `success/partial/failed`。后置更新失败时写 OTel + 本地文件兜底并告警。

### 6.4 Blazor 认证

修改 `TreeGraph.Blazor/Program.cs`：

- `AddAuthentication`：默认 Cookie + OIDC challenge；
- `AddOpenIdConnect`：`ResponseType=code`、`UsePkce=true`、`GetClaimsFromUserInfoEndpoint=true`、`SaveTokens=false`、`RoleClaimType="roles"`；
- `OnTokenValidated`：读取 `auth_time` 写入自定义 claim（默认 claim 映射不保证保留原键名）；
- `AddAuthorization`：策略 `AdminOnly` = `RequireRole("app-admin")`；
- `AddCascadingAuthenticationState()`。

修改 `Components/Routes.razor`：

- `AuthorizeRouteView` 替代 `RouteView`；
- `AdditionalAssemblies` 增加 `IdentitySky` 所在程序集（RCL 页面必须同时注册到 `Routes` 与 `MapRazorComponents().AddAdditionalAssemblies`，否则 404）。

修改 `Components/App.razor`：确认 `Routes` 的 `InteractiveServer` 渲染模式下认证状态可级联。

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
    IReadOnlyList<string> SpaceIds { get; } // 可替换实现
}
```

- P0 实现：从 `groups` claim 过滤 `space-` 前缀；
- `groups` mapper 只输出到 access token 与 userinfo，**不输出到 ID token**（避免 ID token 膨胀影响登出重定向）；同时 Blazor 必须启用 `GetClaimsFromUserInfoEndpoint=true` 且 mapper 的 `userinfo.token.claim=true`，否则 ASP.NET Core OIDC 的 ClaimsPrincipal 中不会出现 `groups`，`SpaceIds` 会恒为空；
- 预留实现：按 `sub` 查询（Admin API 或本地缓存），接口不变；
- 指标：token 大小、组数量；超过阈值（初始 30）时发出告警，P1 评估切换；
- `TreeGraph.Api` 增加空间授权过滤器：
  - 路由含 `{spaceId}` 的端点直接比较；
  - 节点级端点（subtree/eav/edge by node）通过 `ISpaceResolver` 解析节点所属空间后比较；
  - 无空间维度的列表端点（如全量 Node 列表）限制为 `app-admin` 或按允许空间过滤。

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
- 重复调用是安全的：已禁用继续尝试吊销；
- UI 不提供单独的“仅禁用”按钮；部分成功时展示“会话吊销失败，重试”按钮；
- 重新启用时删除撤销标记，并提示用户需重新登录。

### 6.8 撤销传播

- Blazor：Cookie 有效期 20 分钟、滑动续期；`RevalidatingAuthenticationStateProvider` 每 60 秒检查撤销标记，命中则登出并刷新 circuit 认证状态；
- API：JwtBearer `OnTokenValidated` 检查 Redis 撤销标记（本地缓存 30 秒），命中则拒绝；
- 后续增强：Keycloak back-channel logout 端点，清 Cookie 并通知 circuit。

---

## 7. UI 设计

页面放在 `TreeGraph.Blazor.Shared/IdentitySky/Pages/`，沿用 MudBlazor。

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
- 敏感字段写入后只显示“已设置”，不回显；
- 破坏性操作需输入资源名确认，并展示影响面；
- 写操作完成后重新拉取，不做乐观更新；
- 任何失败展示 Keycloak 状态码映射（400/403/404/409/502）。

---

## 8. 阶段计划与验收

### P0 身份边界

范围：**Keycloak 26.8.2 升级与 audience 门禁**、realm 应用策略、三个客户端、角色/组/mapper、密钥外置、Blazor 登录、`AdminOnly` 策略、令牌提供器、`AdminActor`、审计骨架、禁用 saga、撤销标记、后端 JWT 与 `ICurrentUser`。

验收：

- Keycloak 实际版本为 26.8.2；镜像 tag 已显式固定；
- `treegraph-admin` 的 default client scope 含 `kc-admin-api`；
- 管理令牌 `aud` 含 Admin API 地址，`/users?max=1` 返回 200；
- mapper CRUD 在 26.8.2 上通过；
- realm 按新配置可用，重复应用配置不报错；
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

验收：新建标准 OIDC 客户端可完成登录；mapper 修改在 token 中可见；26.8.2 的 mapper 权限模型已在 P0 门禁中验证。

### P4 身份提供方与联邦

范围：OIDC/SAML/社交 IdP；LDAP/AD 配置、测试连接、同步；SMTP。

验收：LDAP 测试连接成功；重置密码邮件可达；IdP 登录可回到 Blazor。

### P5 Realm 与认证流

范围：认证流、realm 设置、事件、导出。

验收：每次认证流写操作前自动导出并加密存储；导出失败则拒绝写入；导出内容按敏感文件管理。

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
点击“删除客户端” → IStepUpService.IsFresh=false
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

备份语义必须明确：

- partial export 是**结构备份**，不包含用户；Admin Console/partial export 会把密码与 client secret 掩码为 `*`，不能作为含密钥的完整恢复源；
- client secret 由 Aspire 参数/用户机密独立保管，恢复后需重新注入；
- 导出不包含事件、会话、撤销标记，且多节点运行时一致性不保证；
- UI 的“最近备份时间”旁必须提示“结构备份，密钥需单独恢复”。

---

## 10. 安全设计与护栏

1. 管理入口仅 `app-admin`；管理员强制 MFA。
2. 不可变约束：
   - 不能禁用/删除最后一个 `app-admin`；
   - 不能移除自己的 `app-admin`；
   - 不能删除内置客户端；
   - 不提供删除 realm。
3. 服务账号 secret 独立保管；轮换与 step-up 解耦；P2 起切 `private_key_jwt` 实现零停机轮换。
4. 审计不可绕过：facade 写方法只接受 `AdminActor`；装饰器统一落审计；失败留痕。
5. 敏感字段脱敏：client secret、SMTP 密码、LDAP bind 凭据、realm 导出文件。
6. 限流与熔断：管理 API 写操作限流；Keycloak 不可用返回 502，不重试非幂等写。
7. 生产环境保护：环境徽标、只读开关、操作窗口。
8. 备份：认证流修改前自动执行结构备份；备份文件加密且设保留期；secret 由 Aspire 参数独立管理，恢复后重新注入；明确“备份不是事务回滚”，配套恢复手册。

---

## 11. 测试策略

| 层级 | 内容 |
|---|---|
| 单元 | 令牌单飞、401 强制刷新、AdminActor 构造、审计装饰器、禁用 saga 部分成功、auth_time 新鲜度 |
| 集成 | Keycloak 26.8.2 容器（Testcontainers 或 Aspire 资源）：realm 导入幂等、用户/角色/组/客户端 CRUD、mapper 生效、**管理令牌 `aud` 与 `kc-admin-api` scope** |
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

## 13. 文件级改动清单

| 位置 | 改动 |
|---|---|
| [KeycloakExtension.cs](file:///d:/APromisedLand/APromisedLand.AppHost/Extensions/KeycloakExtension.cs) | 固定镜像 26.8.2、`WithRealmImport`、参数化密钥、OTLP |
| [KeycloakRealmpart.json](file:///d:/APromisedLand/APromisedLand.AppHost/KeycloakRealmpart.json) | 去明文；新增客户端/角色/组/mapper/realm 设置 |
| [AppHost.cs](file:///d:/APromisedLand/APromisedLand.AppHost/AppHost.cs) | 当前拓扑启用 `AddKeycloak` |
| `PostgresExtension.cs` | 增加 `IdentityAdminDb` 数据库 |
| `TreeGraphBlazorExtension.cs` | Blazor `WithReference(Keycloak)` |
| [TreeGraph.Blazor/Program.cs](file:///d:/APromisedLand/TreeGraph.Blazor/Program.cs) | OIDC/Cookie/Authorization/step-up/服务注册 |
| [Routes.razor](file:///d:/APromisedLand/TreeGraph.Blazor/Components/Routes.razor) | `AuthorizeRouteView` + IdentitySky 程序集 |
| [App.razor](file:///d:/APromisedLand/TreeGraph.Blazor/Components/App.razor) | 认证状态级联 |
| 新增 `TreeGraph.IdentityAdmin` | facade、令牌、审计、模块服务 |
| 新增 `TreeGraph.Blazor.Shared/IdentitySky` | 管理页面与组件 |
| `TreeGraph.Shared/Identity` | `ICurrentUser`、claim 常量、空间前缀 |
| [TreeGraph.Api/Program.cs](file:///d:/APromisedLand/TreeGraph.Api/Program.cs) | JwtBearer、授权过滤器、撤销校验 |
| `TreeGraph.MafWfApi/Program.cs` | JwtBearer、`ICurrentUser`、space 403 |
| `docker/.env`、`infra/.env` | 移除明文密码 |
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
| Admin API 权限模型变化 | 某些模块 403 | 26.8.2 门禁已在 P0 验证；委托管理员需配置 Allowed protocol mapper types |
| 审计写失败 | 追溯缺失 | 两阶段审计 + fail-closed + 兜底告警 |
| 主题/服务端配置无法 UI | 期望落差 | UI 明确“只可选择已部署主题” |
| 本期升级到 26.8.2 后管理 API 401 | 全部管理功能失效 | P0 门禁：分配 `kc-admin-api` scope 并验证 `aud`（§3.6） |
| 26.8.2 mapper 权限收紧 | 委托管理员 mapper 操作 403 | 本方案含 `manage-realm`；若拆分委托管理员，配置 Allowed protocol mapper types |
| partial export 不含 secret | 恢复后客户端无法登录 | 结构备份 + secret 独立保管与重新注入（§9.4） |
| ID token 组膨胀 | 登出重定向超限 | groups 只进 access token + userinfo；Blazor 启用 UserInfo 拉取 |
| 审计库不可用 | 管理操作无留痕 | 两阶段审计 + fail-closed；提供 break-glass 通道 |

---

## 15. 未决事项

1. 管理员 MFA 是否在 P0 强制，还是 P2 再启用。
2. `app-editor` / `app-viewer` 的具体接口权限矩阵（当前只定义角色）。
3. 空间组命名是否需要支持改名（当前约定 `space-{空间ID}` 不可变）。
4. 审计保留期与导出格式（CSV/JSON）。
5. 生产环境 Keycloak 的 TLS 终止位置（Ingress 还是 Keycloak 自身）。
6. 升级窗口安排与 Keycloak 数据库备份责任人（本期确定升级到 26.8.2）。
