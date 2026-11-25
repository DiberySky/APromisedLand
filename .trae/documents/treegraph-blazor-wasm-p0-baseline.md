# TreeGraph WASM 迁移 P0：测试基线与页面清单

日期：2026-11-24  
范围：只执行测试基线和页面/路由/身份管理注入盘点；未开始 WASM 产品代码改造。

## 1. 测试基线

### 1.1 执行口径

- 使用 .NET SDK 11.0.100，逐项目执行 `dotnet test --nologo --verbosity minimal`。
- Aspire 正在运行，首轮部分项目因应用 DLL 被运行进程锁定而出现 MSB3021/MSB3027；这些属于构建失败，不计为测试失败。
- 未停止 Aspire。受影响项目改用独立 `OutDir` 重新编译并执行。
- SyncServices 的架构测试会从 `AppContext.BaseDirectory` 向上查找 `APromisedLand.sln`，因此其独立输出放在项目 `bin/P0` 下；在仓库外输出时出现的 2 个 `DirectoryNotFoundException` 是测试定位仓库失败，已由仓库内独立输出的 38/38 通过结果取代。
- `TreeGraph.Blazor.E2E.Tests` 未执行；完整 Aspire/浏览器 E2E 按约定由用户启动验证。

### 1.2 实际结果

| 测试项目 | 通过 | 失败 | 跳过 | 总计 | 结论 |
|---|---:|---:|---:|---:|---|
| TreeGraph.Blazor.Tests | 75 | 0 | 0 | 75 | 通过 |
| TreeGraph.Blazor.Shared.Tests | 266 | 5 | 1 | 272 | 保留为既有失败基线 |
| TreeGraph.IdentityAdmin.Tests | 42 | 0 | 0 | 42 | 通过 |
| TreeGraph.Api.Tests | 316 | 0 | 0 | 316 | 通过 |
| TreeGraph.MafWfApi.Tests | 67 | 0 | 0 | 67 | 通过 |
| TreeGraph.FileStorageApi.Tests | 153 | 0 | 0 | 153 | 通过 |
| TreeGraph.SyncServices.Tests | 38 | 0 | 0 | 38 | 通过 |
| APromisedLand.AppHost.Tests | 23 | 0 | 0 | 23 | 通过 |
| TreeGraph.IdentityAdmin.IntegrationTests | 1 | 6 | 0 | 7 | 保留为既有集成失败基线 |
| **合计（本次执行）** | **981** | **11** | **1** | **993** | 不把失败隐瞒为通过 |

### 1.3 既有失败明细

Shared 的 5 个失败：

1. `LineNodeActionsDialogSmokeTests.ClickEdit_ClosesWithEditAction`：bUnit 渲染期间 `NullReferenceException`。
2. `LineNodeActionsDialogSmokeTests.Renders_BasicInfo_AndActionButtons`：同类 `NullReferenceException`。
3. `LineNodeActionsDialogSmokeTests.Hides_LineManage_WhenDisabled`：同类 `NullReferenceException`。
4. `LineNodeActionsDialogSmokeTests.ClickLineManage_ClosesWithLineManageAction`：同类 `NullReferenceException`。
5. `NodeEavDialogSmokeTests.TreeNodeActionsDialog_TreeHost_Renders`：未注册 `ITreeClientService<UnitTree>`。

Shared 的 1 个跳过：`FilePreviewDialogTests.Dispose_WhenBlobUrlCreated_RevokesIt`。

IdentityAdmin 集成测试使用 Testcontainers 启动 `quay.io/keycloak/keycloak:26.8.2`，容器成功启动；6 个管理 API 用例均在刷新令牌后仍收到 HTTP 401：

- `Realm_IsReadableWithServiceAccountToken`
- `RealmKeys_ContainActiveSigningKey`
- `ClientLogoutAttributes_RoundTrip`
- `ClientScopes_IncludeAdminApiScopeOnSupportedVersions`
- `User_Crud_RoundTrip`
- `Organizations_EndpointAvailable`

该结果只记录为当前基线，不在 P0 擅自修改认证实现；后续管理 API 工作不得把它误算成 WASM 迁移回归，也不能忽略其对新管理链路的风险提示。

## 2. 清单统计口径

- `TreeGraph.Blazor/Components/Pages`：46 个 Razor 文件。
  - 35 个带 `@page` 的宿主路由文件：12 个普通/系统页 + 23 个管理页。
  - 11 个无独立路由的管理对话框。
- `TreeGraph.Blazor.Shared`：另有 24 个 `@page` 路由，经 `Routes.razor` 的 `AdditionalAssemblies` 装载。
- 因而当前路由清单总计 **59 条**，不能只按宿主 35 条估算。
- 身份管理相关注入按以下明确口径统计：`I*AdminService`、`IAdminAuditQueryService`、`IAdminActorAccessor`、`StepUpGuard`、`IStepUpService`。
- 实际共 **79 处**：Admin 目录 77 处（管理页 56 + 对话框 21），StepUpPage 2 处。该数字取代旧计划中的 71 处。

迁移分类：

- **复用/适配**：保留页面和 Shared 组件，移除 Server render mode，接运行时 URL、Bearer Handler 或 HTTP 客户端。
- **HTTP 适配**：将进程内 IdentityAdmin 服务替换为受保护管理 API 客户端；服务端保留鉴权、审计和 Step-Up。
- **重写**：认证/宿主生命周期相关，不能直接搬到 WASM。
- **候选退役**：模板或开发演示页；正式删除前需确认，不在 P0 删除。

## 3. 宿主普通与系统路由（12）

| 文件 | 路由 | 分类 | 阶段/说明 |
|---|---|---|---|
| Home.razor | `/` | 复用/适配 | P3；移除 InteractiveServer，复用 Shared 对话框 |
| SpaceTreePage.razor | `/space-tree` | 复用/适配 | P1；首个只读垂直切片，接 Bearer 与运行时 URL |
| EavTreePage.razor | `/eav-tree` | 复用/适配 | P3；TreeGraph.Api |
| CategoryTreePage.razor | `/category-tree` | 复用/适配 | P3；TreeGraph.Api |
| KnowledgeTreePage.razor | `/knowledge-tree` | 复用/适配 | P3；TreeGraph.Api |
| UnitTreePage.razor | `/unit-tree` | 复用/适配 | P3；TreeGraph.Api |
| SplitTestPage.razor | `/split-test` | 候选退役/适配 | P3；先确认是否属于开发试验页 |
| Counter.razor | `/counter` | 候选退役 | P3；模板演示页，删除前确认 |
| Weather.razor | `/weather` | 候选退役 | P3；模板演示页，删除前确认 |
| Error.razor | `/Error` | 重写 | P1；WASM 宿主错误边界 |
| NotFound.razor | `/not-found` | 重写/适配 | P1；接入新 Router 的 NotFoundPage |
| StepUpPage.razor | `/step-up` | 重写 | P2；2 处注入，改 OIDC max-age 与管理 API step-up 流程 |

## 4. 宿主管理路由（23）

除 Users/Roles 当前仅 `[Authorize]` 外，其余管理路由现状均使用 `AdminOnlyPolicy`；P0 只记录，不修改权限。所有管理 API 均必须服务端授权，不能依赖 UI 隐藏。

| 文件 | 路由 | 注入数 | 分类 | 阶段 |
|---|---|---:|---|---|
| AdminDashboardPage.razor | `/admin` | 1 | HTTP 适配 | P4 |
| AdminUsersPage.razor | `/admin/users` | 2 | HTTP 适配 | P2；用户搜索 |
| AdminUserDetailPage.razor | `/admin/users/{Id}` | 6 | HTTP 适配 + Step-Up | P2 先覆盖详情/禁用/启用，其余 P4 |
| AdminRolesPage.razor | `/admin/roles` | 2 | HTTP 适配 | P4 |
| AdminGroupsPage.razor | `/admin/groups` | 3 | HTTP 适配 + Step-Up | P4 |
| AdminSpacesPage.razor | `/admin/spaces` | 2 | HTTP 适配 | P4；当前复用 GroupsAdminService |
| AdminClientsPage.razor | `/admin/clients` | 2 | HTTP 适配 | P4 |
| AdminClientDetailPage.razor | `/admin/clients/{ClientId}` | 6 | HTTP 适配 + Step-Up | P4 |
| AdminClientScopesPage.razor | `/admin/client-scopes` | 2 | HTTP 适配 | P4 |
| AdminClientScopeDetailPage.razor | `/admin/client-scopes/{ScopeId}` | 2 | HTTP 适配 | P4 |
| AdminIdentityProvidersPage.razor | `/admin/identity-providers` | 2 | HTTP 适配 | P4 |
| AdminIdentityProviderDetailPage.razor | `/admin/identity-providers/{Alias}` | 2 | HTTP 适配 | P4 |
| AdminUserFederationPage.razor | `/admin/user-federation` | 2 | HTTP 适配 | P4 |
| AdminFederationDetailPage.razor | `/admin/user-federation/{ComponentId}` | 3 | HTTP 适配 + Step-Up | P4 |
| AdminAuthFlowsPage.razor | `/admin/auth-flows` | 2 | HTTP 适配 | P4 |
| AdminAuthFlowDetailPage.razor | `/admin/auth-flows/{Alias}` | 3 | HTTP 适配 + Step-Up | P4 |
| AdminOrganizationsPage.razor | `/admin/organizations` | 2 | HTTP 适配 | P4 |
| AdminOrganizationDetailPage.razor | `/admin/organizations/{OrganizationId}` | 2 | HTTP 适配 | P4 |
| AdminRealmPage.razor | `/admin/realm` | 3 | HTTP 适配 + Step-Up | P4 |
| AdminRealmKeysPage.razor | `/admin/realm-keys` | 3 | HTTP 适配 + Step-Up | P4 |
| AdminEventsPage.razor | `/admin/events` | 1 | HTTP 适配 | P4 |
| AdminAuditPage.razor | `/admin/audit` | 1 | HTTP 适配 | P4 |
| AdminExportPage.razor | `/admin/export` | 2 | HTTP 适配；文件下载专项 | P4 |

**管理路由注入小计：56。**

## 5. 管理对话框（11，无独立路由）

| 文件 | 注入数 | 分类 | 阶段 |
|---|---:|---|---|
| CreateUserDialog.razor | 2 | HTTP 适配 | P4 |
| ResetPasswordDialog.razor | 2 | HTTP 适配 + 高危写验证 | P4 |
| CreateRoleDialog.razor | 2 | HTTP 适配 | P4 |
| CreateGroupDialog.razor | 2 | HTTP 适配 | P4 |
| GroupMembersDialog.razor | 1 | HTTP 适配 | P4 |
| CreateClientDialog.razor | 2 | HTTP 适配 | P4 |
| CreateIdentityProviderDialog.razor | 2 | HTTP 适配 | P4 |
| CreateLdapFederationDialog.razor | 2 | HTTP 适配 | P4 |
| CopyAuthFlowDialog.razor | 2 | HTTP 适配 | P4 |
| CreateOrganizationDialog.razor | 2 | HTTP 适配 | P4 |
| CreateRealmKeyDialog.razor | 2 | HTTP 适配 + 高危写验证 | P4 |

**管理对话框注入小计：21。**

## 6. Shared/RCL 路由（24）

这些页面由 Shared 程序集提供，宿主通过 `AdditionalAssemblies` 装载；优先保留组件，按所属 API 替换运行时地址、Bearer、CORS 和测试 mock。

| 文件 | 路由 | 分类 | 阶段/专项 |
|---|---|---|---|
| SpaceListPage.razor | `/spaces` | 复用 + HTTP 适配 | P3 |
| SpaceNodesPage.razor | `/space-nodes` | 复用 + HTTP 适配 | P3 |
| EntitySelector.razor | `/entities` | 复用 + HTTP 适配 | P3 |
| EntityList.razor | `/entities/{EntityType}` | 复用 + HTTP 适配 | P3 |
| EntityEdit.razor | `/entities/{EntityType}/{EntityId}` | 复用 + HTTP 适配 | P3 |
| EntityHistory.razor | `/entities/{EntityType}/{EntityId}/history` | 复用 + HTTP 适配 | P3 |
| InodeList.razor | `/inodes` | 复用 + HTTP 适配 | P3 |
| InodeDetail.razor | `/inodes/{InodeId}` | 复用 + HTTP 适配 | P3 |
| InodeEntityEdit.razor | `/inodes/{InodeId}/bindings/{BindingId}` | 复用 + HTTP 适配 | P3 |
| InodeEntityHistory.razor | `/inodes/{InodeId}/bindings/{BindingId}/history` | 复用 + HTTP 适配 | P3 |
| AttributesList.razor | `/attributes` | 复用 + HTTP 适配 | P3 |
| DynamicQuery.razor | `/query` | 复用 + HTTP 适配 | P3 |
| SchemaViewer.razor | `/schema` | 复用 + HTTP 适配 | P3 |
| EntityTypes.razor | `/metadata/entity-types` | 复用 + HTTP 适配 | P3 |
| CompositeTypes.razor | `/metadata/composite-types` | 复用 + HTTP 适配 | P3 |
| CustomTables.razor | `/metadata/custom-tables` | 复用 + HTTP 适配 | P3 |
| OptionSets.razor | `/metadata/option-sets` | 复用 + HTTP 适配 | P3 |
| EdgeTypes.razor | `/metadata/edge-types` | 复用 + HTTP 适配 | P3；现有 RZ10012 警告需保留基线 |
| SemanticSearch.razor | `/search` | 复用 + HTTP 适配 | P3；SyncServices |
| NodeSemanticSearch.razor | `/node-semantic-search` | 复用 + HTTP 适配 | P3；SyncServices |
| SemanticSearchDirect.razor | `/semantic-search-direct` | 复用 + HTTP 适配 | P3；SyncServices |
| WriterCritic.razor | `/maf-wf/writer-critic` | 复用 + HTTP/SSE 适配 | P3；增量流、取消、401、禁止自动重放 |
| GraphAgentChat.razor | `/maf-wf/graph-agent` | 复用 + HTTP/SSE 适配 | P3；按实际传输实现验证 |
| NumberFieldDemo.razor | `/number-field-demo` | 候选退役 | P3；演示页，删除前确认 |

## 7. P0 本轮结论与门禁

- 已完成：9 个非 E2E 测试项目的实际执行和结果记录；59 条路由、46 个宿主页面文件、11 个管理对话框及 79 处身份管理相关注入的分类清单。
- 基线并非全绿：Shared 5 失败/1 跳过，IdentityAdmin 集成测试 6 失败；后续对比必须沿用该基线。
- 首个 P1 切片仍只做 `/space-tree` 只读链路，不把管理 API 或 Shared 全量页面塞入 P1。
- 未执行：Blazor E2E、性能基线、CSP/第三方依赖安全评审；它们仍是进入后续阶段的待办，不宣称 P0 全部工作已结束。
