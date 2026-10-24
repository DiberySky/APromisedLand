# R3 · EAV/Edge 锚点迁移到 nodes 注册表（含边注册）

> 上游蓝图：`specs/node-sky/spec.md` §8.3 第 3 步 / §8.5 / §8.6「边与 nodes 注册表（R3 定稿）」。
> 前置依赖：R2（Name 收编）已完成 —— 宿主表已无 name 列、`nodes.name` 已是唯一真源，`[NotMapped] Name` 读穿。
> 日期：2026-10-24

## 1. 目标

让 **EAV 与 Edge 只认识 `nodes` 注册表，不认识任何具体宿主表**：

- 四棵宿主树 + LineNode + EdgeInstance **不再实现 `Inode`**，只有 `Node` 实现；
- 8 个 `IInodeOwnerGuard` 收敛为 **2 个**（`NodesRegistryGuard` + 保留的 `LineOwnerGuard`）；
- EAV 写入前的宿主校验 = `nodes` 有一行；
- Edge 端点校验 = `nodes` 有一行且 `host_type == 声明的类型`（单表单查询）；
- `EdgeInstance` 同事务注册进 `nodes`（`host_type="EdgeInstance"`、`name=null`），`edge_instances.edge_id` 加 FK → `nodes(id)` ON DELETE RESTRICT。

## 2. 范围（用户 2026-10-24 确认）

**取「§8.5 + 边注册」**：

| 包含 | 不包含（留给 R4） |
|---|---|
| 宿主实体去 `Inode` | §8.6 全量边重建（删 `edge_definitions` 表/实体/`EdgeMetadataService`/`EdgeMetadataController`） |
| 守卫 8 → 2 收敛 | 18 条边类型种子清空 |
| `EdgeInstance` 注册进 `nodes` + FK | `edge_instances` 改 `type_name` 裸列 + `is_directed` |
| EAV/Edge 校验改走 `nodes` | 删除检查改为「nodes 行被 from/to 引用 → 409」的 from/to 真 FK |
| 边作为端点/宿主时的显示名投影 | `line_structures.edge_type_id` 退役、`line_structure_segments` 改造 |
| — | 图同步顶点解析改 nodes 单查（保持现状，R4 一并做） |

**Line 实体（`lines.name` 尚未收编）**：保留 `Inode` 实现与 `LineOwnerGuard`。最终守卫数 = 2，而非 1；这是与蓝图「1 个守卫」的唯一有意偏离，理由见决策 D3。

## 3. 现状（已核实）

| 项 | 现状 |
|---|---|
| `Inode` 实现者 | `Node`、`UnitTree`、`SpaceTree`、`CategoryTree`、`KnowledgeTree`、`LineNode`、`Line`、`EdgeInstance`（后 2 个为显式实现） |
| 守卫注册 | `Program.cs` L118–L129 共 8 个：UnitTree / CategoryTree / KnowledgeTree / SpaceTree / LineNode / Line / Node / EdgeInstance |
| `InodeOwnerGuardRegistry` | 按 `NodeType` 精确 `Resolve`；`EnsureNodeExistsAsync` 任一守卫放行、0 守卫放行；`ResolveNodeNamesAsync` 跨守卫聚合（`TryAdd`） |
| `EdgeInstance` | `edge_instances` 表，PK `edge_id`（DB 默认 `gen_random_uuid()::text`），无 nodes 关联；显式 `Inode.Id => EdgeId`、`Inode.Name => DisplayName ?? "from→to"` |
| 测试特殊装配 | `EavApiFactory` `RemoveAll<IInodeOwnerGuard>()` 模拟「外部模式」（0 守卫放行）；`EdgeGuardApiFactory` 在其上重注册单位树守卫 |

## 4. 决策

| # | 决策 | 理由 |
|---|---|---|
| **D1** | 保留 `IInodeOwnerGuard` 抽象与 `InodeOwnerGuardRegistry` 聚合机制，**不删除**。新增 `INodesRegistryGuard : IInodeOwnerGuard`（类型感知查询）与 `Registry.EnsureNodeOfTypeAsync`。 | `EavApiFactory` 靠「移除全部守卫」实现外部模式，若把校验写死进 registry 会破坏 200+ EAV 测试。保留聚合 = 外部模式语义不变。 |
| **D2** | `NodesRegistryGuard.NodeType = ""`（空串，永不参与精确解析），全类型校验走 `INodesRegistryGuard` 分支：先查 `nodes` 行 + `host_type` 匹配；未命中再判断「该 host_type 是否在 nodes 中存在过」以区分「类型未认领」与「节点不存在」。 | 一个守卫不能有多个 `NodeType`；用空串避开精确匹配，保持 `Resolve` 语义（供 `Line` 精确解析）不变。错误文案与现有测试断言一致。 |
| **D3** | `Line` 保留 `Inode` + `LineOwnerGuard`；`lines.name` 不动。 | `lines` 未注册进 `nodes`，若强行去 Inode 会造成 EAV 宿主校验缺口与重复真源。留给 Line 收编轮次。 |
| **D4** | `NodesRegistryGuard.GetNodeNamesAsync` 单表查 `nodes`：① `name != null` 直接返回（UnitTree 额外 LEFT JOIN 简称拼 `Name 【简称】`）；② `host_type="EdgeInstance"` 且 `name=null` 投影 `DisplayName ?? "fromId→toId"`（与旧 `EdgeOwnerGuard` 行为一致）。 | 保证 R2 的名称投影结果零变化，且边作为端点时仍可显示。 |
| **D5** | `EdgeInstance.EdgeId` 改**客户端生成**（`Guid.NewGuid().ToString()`），不再依赖 DB 默认值。 | FK 要求 nodes 行先插入；主键须在插入前已知。EF 单次 `SaveChanges` 按依赖顺序先插 `nodes` 再插 `edge_instances`。 |
| **D6** | 边创建：同一次 `SaveChanges` 中 `Nodes.Add(new Node{Id=EdgeId,HostType="EdgeInstance",Name=null})` + `EdgeInstances.Add`。边删除：先做「被其他边引用」友好前置检查，再删 EAV 前置校验（沿用），最后 `Remove(edge)` + `Remove(nodeRow)` 同一次 `SaveChanges`。 | 单次 SaveChanges 自带事务；EF 按依赖排序。FK Restrict 兜底。 |
| **D7** | 迁移 `RegisterEdgesInNodes`：`Up` = 回填（`INSERT INTO nodes ... SELECT edge_id, 'EdgeInstance', NULL, created_at FROM edge_instances ON CONFLICT DO NOTHING`）→ `AddForeignKey(edge_instances.edge_id → nodes.id, Restrict)`；`Down` = 删 FK → 删这些 nodes 行。 | 存量边不丢身份；幂等。 |
| **D8** | 图同步 `GraphSynchronizer` 本次不改。 | 它不依赖 `Inode`/守卫，改动只会增加风险；属 §8.6「图同步与向量」范围，随 R4。 |

## 5. 改动清单

### 5.1 共享层（TreeGraph.Shared）

| 文件 | 改动 |
|---|---|
| `TreeEavSky/Entities/UnitTree.cs` | 类声明去 `, Inode`（`Name` 读穿属性保留，供 `ITreeNodeBase`） |
| `TreeEavSky/Entities/SpaceTree.cs` | 同上 |
| `TreeEavSky/Entities/CategoryTree.cs` | `IEquatable<CategoryTree>, Inode` → `IEquatable<CategoryTree>` |
| `TreeEavSky/Entities/KnowledgeTree.cs` | 同上 |
| `LineSky/Entities/LineNode.cs` | 类声明去 `: Inode` |

### 5.2 Api 层（TreeGraph.Api）

| 文件 | 改动 |
|---|---|
| `EdgeSky/Entities/EdgeDefinition.cs` | `EdgeInstance` 去 `: Inode`；删显式 `Inode.Id/Name`；加 `[JsonIgnore] public Node? Node { get; set; }` 共享主键导航 |
| `Data/TreeGraphDbContext.cs` | `ConfigureEdges`：`EdgeInstance` 加 `HasOne(x => x.Node).WithOne().HasForeignKey<EdgeInstance>(x => x.EdgeId).OnDelete(DeleteBehavior.Restrict)` |
| `NodeEavSky/Services/InodeOwnerGuard.cs` | 新增 `INodesRegistryGuard`；`Registry` 新增 `EnsureNodeOfTypeAsync`（精确守卫优先，其次 nodes 类型感知） |
| `NodeSky/Services/NodeOwnerGuard.cs` | 重写并改名 `NodesRegistryGuard`（`NodeType=""`、类型感知、单表名称解析含边投影） |
| `TreeEavSky/Services/TreeEntityOwnerGuard.cs` | **删除**（4 个守卫） |
| `LineSky/Services/LineNodeOwnerGuard.cs` | **删除** |
| `EdgeSky/Services/EdgeOwnerGuard.cs` | **删除** |
| `EdgeSky/Services/EdgeNodeValidator.cs` | 改为 `await _inodeGuards.EnsureNodeOfTypeAsync(nodeType, nodeId, ct)` |
| `EdgeSky/Services/EdgeInstanceService.cs` | Create 客户端生成 `EdgeId` + 同事务注册 nodes；Delete 同事务注销 nodes + 被其他边引用的友好前置检查 |
| `Program.cs` | 6 个守卫注册删除；`NodeOwnerGuard` 替换为 `NodesRegistryGuard`；保留 `LineOwnerGuard` |
| 迁移 `RegisterEdgesInNodes` | 见 D7 |

### 5.3 测试

| 文件 | 改动 |
|---|---|
| `Fixtures/EdgeGuardApiFactory.cs` | 重注册 `NodesRegistryGuard`（替代已删的 `UnitTreeInodeGuard`） |
| `Tests/EdgeNodeValidatorTests.cs` | 无需改动（错误文案由 registry 保持原文案） |
| `Tests/InodeOwnerGuardRegistryTests.cs` | 无需改动（聚合/Resolve/名称解析语义未变） |
| `Tests/EdgeNodeGuardTests.cs` | 无需改动（种子单位树 id 已在 `nodes`） |

## 6. 验收标准

### rule
- **AC1**：`UnitTree` / `SpaceTree` / `CategoryTree` / `KnowledgeTree` / `LineNode` / `EdgeInstance` 均不再实现 `Inode`；`Node` 仍实现（grep `: Inode` 仅 2 处：`Node`、`Line`）。
- **AC2**：`IInodeOwnerGuard` 生产注册数 = 2（`NodesRegistryGuard`、`LineOwnerGuard`）；`TreeEntityOwnerGuard.cs` / `LineNodeOwnerGuard.cs` / `EdgeOwnerGuard.cs` 文件已删除。
- **AC3**：EAV 宿主校验（`InodeEntityService` 三处 `EnsureNodeExistsAsync`）对四树/LineNode/Edge 节点均通过，对不存在的 id 抛 `EavValidationException`。
- **AC4**：Edge 端点校验走 `nodes` 单表 + `host_type` 匹配；`GhostType` → 400「未实现 Inode」；`UnitTree` 幽灵节点 → 400「不存在于 UnitTree」。
- **AC5**：`edge_instances.edge_id` 存在 FK → `nodes(id)` ON DELETE RESTRICT（模型快照 + 迁移验证）。
- **AC6**：创建边时 `nodes` 行同事务写入（`host_type="EdgeInstance"`、`name=null`）；删除边时同事务注销；被其他边引用时友好 409（`InvalidOperationException`）。
- **AC7**：`GetNodeNamesAsync` 对 UnitTree 返回 `Name 【简称】`、对边返回 `DisplayName ?? "fromId→toId"`，与 R2 行为一致。
- **AC8**：`dotnet build APromisedLand.sln` 0 错误。
- **AC9**：`TreeGraph.Blazor.Shared.Tests` 235 通过 + 1 跳过；`TreeGraph.Api.Tests` 322；`TreeGraph.MafWfApi.Tests` 65。

### rubric
- **AC10**：迁移 `Up` 对存量边幂等回填（`ON CONFLICT DO NOTHING`），`Down` 可回滚且不残留 `host_type='EdgeInstance'` 的 nodes 行。
- **AC11**：`EavApiFactory` 外部模式（0 守卫）语义不变：任意 inodeId 的 EAV 写入仍放行。

## 7. 风险与缓解

| 风险 | 缓解 |
|---|---|
| `EdgeId` 改客户端生成影响依赖 DB 默认值的测试 | 全仓只有 `EdgeInstanceService` 一处创建边，统一改；测试不假设主键格式 |
| 删守卫导致某些 EAV 宿主类型漏校验（如 SpaceTree 无种子） | `NodesRegistryGuard` 认领全部 `nodes` 行，与具体宿主表无关；Line 保留专用守卫 |
| 名称解析顺序变化（旧 8 守卫逐个 `TryAdd`） | 新 nodes 守卫一次查全，UnitTree 简称优先拼接；Line 守卫后置 `TryAdd` 不覆盖 |
| 存量边无 nodes 行时删除报并发异常 | 删除前显式 `FirstOrDefaultAsync` 加载，缺失则跳过 nodes 删除 |
