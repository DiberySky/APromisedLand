# R3 · 锚点迁移到 nodes 注册表 · 评审报告

> 依据 `specs/node-sky/r3-registry-anchor/spec.md` 与 `tasks.md`。
> 评审日期：2026-10-24。范围：§8.5 + 边注册进 nodes（用户确认），不含 §8.6 边表重建。

## 1. 验收标准核对

| AC | 结论 | 证据 |
|---|---|---|
| **AC1** 宿主去 Inode | ✅ | 全仓 grep `: Inode` 仅剩 [Node.cs](file:///d:/APromisedLand/TreeGraph.Shared/NodeSky/Entities/Node.cs#L18) 与 [Line.cs](file:///d:/APromisedLand/TreeGraph.Shared/LineSky/Entities/Line.cs#L10) |
| **AC2** 守卫注册 8 → 2 | ✅ | [Program.cs](file:///d:/APromisedLand/TreeGraph.Api/Program.cs#L118-L119) 仅 `NodesRegistryGuard` + `LineOwnerGuard`；3 个守卫文件已删 |
| **AC3** EAV 宿主校验走 nodes | ✅ | `NodesRegistryGuard.NodeExistsAsync` = `nodes.AnyAsync(Id)`；EAV 相关 160+ 用例全绿 |
| **AC4** Edge 端点校验 nodes 单表 | ✅ | [EdgeNodeValidator.cs](file:///d:/APromisedLand/TreeGraph.Api/EdgeSky/Services/EdgeNodeValidator.cs#L36) → `EnsureNodeOfTypeAsync`；`EdgeNodeGuardTests` 4 例全绿（GhostType「未实现 Inode」、幽灵节点「不存在于 UnitTree」） |
| **AC5** edge FK → nodes Restrict | ✅ | 快照 [TreeGraphDbContextModelSnapshot.cs](file:///d:/APromisedLand/TreeGraph.Api/Data/Migrations/TreeGraphDbContextModelSnapshot.cs#L3893-L3899)；迁移 `ALTER TABLE edge_instances ADD CONSTRAINT "FK_edge_instances_nodes_edge_id" ... ON DELETE RESTRICT` |
| **AC6** 边创建/删除同事务维护 nodes | ✅ | [EdgeInstanceService.cs](file:///d:/APromisedLand/TreeGraph.Api/EdgeSky/Services/EdgeInstanceService.cs#L110-L133) 单次 `SaveChanges` 先插 nodes 再插边；删除 452–458 单次 `SaveChanges`；被其他边引用时抛 `InvalidOperationException` → 409 |
| **AC7** 名称投影零变化 | ✅ | `GetNodeNamesAsync`：nodes.name（UnitTree 拼简称）+ 边 `DisplayName ?? "fromId→toId"`；`EdgeHostTests.Edge_AsHost_DisplayNameFallback_ToEdgeId` 通过 |
| **AC8** 全解决方案构建 | ✅ | 0 错误（2 条预存 EF Core 10.0.12/10.0.13 版本警告，与本次无关） |
| **AC9** 三套测试基线 | ✅ | Blazor.Shared 235+1 / Api 322 / MafWfApi 65 |
| **AC10** 迁移幂等可回滚 | ✅ | `Up`：`INSERT ... ON CONFLICT (id) DO NOTHING` → FK；`Down`：删 FK → 删 `host_type='EdgeInstance'` 的 nodes 行。集成测试在真实 PG16 上执行迁移成功 |
| **AC11** 外部模式语义不变 | ✅ | `EavApiFactory` 仍 `RemoveAll<IInodeOwnerGuard>()`，registry 0 守卫放行；EAV 回归全绿 |

补充回归：`TreeGraph.FileStorageApi.Tests` 153、`TreeGraph.Blazor.Tests` 71 亦全绿。

## 2. 实施偏离（相对 spec）

| # | 偏离 | 原因 |
|---|---|---|
| 1 | 守卫最终为 **2 个**而非 1 个（保留 `LineOwnerGuard`） | 用户确认：`lines` 尚未注册进 nodes，强行去 Inode 会造成 EAV 校验缺口与重复真源。已在 spec §2 显式声明 |
| 2 | 保留 `IInodeOwnerGuard` 聚合抽象，新增 `INodesRegistryGuard` 而非把校验写死进 Registry | `EavApiFactory` 依赖「移除全部守卫 = 外部模式」；写死会破坏大量 EAV 测试 |
| 3 | `EdgeInstance.EdgeId` 改客户端生成 `Guid.NewGuid().ToString()` | FK 要求 nodes 行先于边行插入，主键须在插入前已知 |

## 3. 过程中发现并修复的问题

### 3.1 EF 标识外键删除顺序（11 个测试失败）

- 现象：删除边时 409，消息 `The property 'EdgeInstance.EdgeId' is part of a key and so cannot be modified...`
- 根因：共享主键 one-to-one 下，先 `Remove(nodes 主方)` 会让 EF 尝试把依赖方标识外键置空。
- 修复：`DeleteCoreAsync` 中**先 `Remove(EdgeInstance)`（依赖方）再 `Remove(Node)`（主方）**，同一次 `SaveChanges`（EF 隐式事务）完成。
- 复测：EdgeTests / EdgeHostTests / DeletePrincipleTests / LineStructureTests 98 例全绿。

### 3.2 显式事务不可用（中间方案回退）

- 曾尝试用 `BeginTransactionAsync` 拆两次 `SaveChanges`，触发 `NpgsqlRetryingExecutionStrategy` 不支持用户事务。
- 结论：维持**单次 `SaveChanges`**（隐式事务）方案，既满足原子性也满足 AC12。

### 3.3 测试误判

- `dotnet build TreeGraph.Api` 后以 `--no-build` 跑测试，用到的是 Api.Tests 输出目录里的陈旧 DLL，导致修复「看起来无效」。
- 后续统一先 build 测试项目再跑。

## 4. 遗留事项（R4 议题）

1. §8.6 边模块全量重建：删 `edge_definitions` 表/`EdgeDefinition` 实体/`EdgeMetadataService`/`EdgeMetadataController`，18 条边类型种子清空，`edge_instances` 改 `type_name` 裸列 + `is_directed`，`EdgeInstanceService`（29KB）与 `EdgeTests`（37KB）重写。
2. `edge_instances.from_node_id / to_node_id` 加真 FK → `nodes(id)`（当前仅应用层校验 + 全局边例外）。
3. 图同步 `GraphSynchronizer.ResolveNodeAsync` 由「逐宿主表查询」改为 `nodes` 单查（tag ← host_type）。
4. `line_structures.edge_type_id` 退役、`line_structure_segments` 改造。
5. `Line` / `lines.name` 收编进 nodes，随后删除 `LineOwnerGuard`（守卫 2 → 1）。
6. `nodes.name` 可空性是否收紧（需先完成 Line 收编与边派生规则）。

## 5. 结论

R3 范围内 AC1–AC11 全部满足；构建 0 错误，五套测试全绿（235+1 / 322 / 65 / 153 / 71）。
EAV 与 Edge 已只依赖 `nodes` 注册表，边作为一等公民完成注册，守卫从 8 个收敛到 2 个。
