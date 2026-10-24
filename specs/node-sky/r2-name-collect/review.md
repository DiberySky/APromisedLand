# R2 · Name 收编 · 独立评审

> 评审日期：2026-10-24
> 评审范围：`specs/node-sky/r2-name-collect/spec.md` AC1–AC14
> 评审人：独立评审（实施后自查）

## 1. 验收标准核对

| AC | 描述 | 结果 | 证据 |
|---|---|---|---|
| **AC1** | 五张宿主表不含 `name` 列 | ✅ 通过 | `TreeGraphDbContextModelSnapshot.cs` 中 `unit_trees`/`space_trees`/`category_trees`/`knowledge_trees`/`line_nodes` 均无 `name` 属性 |
| **AC2** | 五张宿主表均有 FK `id → nodes(id)` ON DELETE RESTRICT | ✅ 通过 | 快照中 LineNode/CategoryTree/KnowledgeTree/SpaceTree/UnitTree 均 `HasOne(Node).WithOne().HasForeignKey(宿主, "Id").OnDelete(Restrict)` |
| **AC3** | `nodes` 存在与宿主行同 Id/host_type/name 的行 | ✅ 通过 | 迁移 `Up` 含 5 段 `INSERT INTO nodes ... SELECT ... FROM <宿主表> ON CONFLICT DO NOTHING`；种子行由 `HasData` 预置 |
| **AC4** | `ITreeNodeBase<T>` 无 `Text()`；四树实体无 `Text()` 与映射 `Name` | ✅ 通过 | `ITreeNodeBase.cs` 仅 `string? Name { get; }`；四树实体 `Name` 均 `[NotMapped]` 读穿；全仓 `TreeGraph.*` 无 `.Text()` |
| **AC5** | 树服务 DTO.Text 等于 nodes.name（UnitTree 带简称） | ✅ 通过 | 四服务 `BuildText` 读 `c.Node!.Name`（UnitTree 拼 `c.Abbreviation`），`ToNodeDto(text)` 投影 |
| **AC6** | 树服务 Create/Update 名称落到 nodes.name | ✅ 通过 | Create 同事务 `db.Nodes.Add(new Node{Name=nodeDto.Text})`；Update `entity.Node!.Name = nodeDto.Text` |
| **AC7** | LineNode 搜索/排序走 nodes.name；Create/Update 落到 nodes.name | ✅ 通过 | `LineNodeController.GetAll` `.Include(Node)` + `n.Node!.Name.Contains` + `OrderBy(n => n.Node!.Name)`；Create/Update 维护 Node 行 |
| **AC8** | 5 个宿主守卫 GetNodeNamesAsync 来自 nodes.name | ✅ 通过 | `TreeEntityOwnerGuard` 四守卫 + `LineNodeOwnerGuard` 均查 `db.Nodes`；UnitTree 守卫 JOIN `unit_trees` 拼简称 |
| **AC9** | 前端无 `.Text()` 调用 | ✅ 通过 | grep `TreeGraph.{Api,Shared,Blazor,Blazor.Shared,Blazor.Maui}` 无 `.Text()` 命中 |
| **AC10** | `dotnet build APromisedLand.sln` 0 错误 | ✅ 通过 | 构建结果：0 错误，2 警告（EF Core 版本冲突，预存无关） |
| **AC11** | 三套测试数与基线一致 | ✅ 通过 | Blazor.Shared.Tests 235+1 skip；Api.Tests 322；MafWfApi.Tests 65 |
| **AC12** | 删除宿主节点时同事务删 nodes 行 | ✅ 通过 | 四树服务 Delete + LineNodeController.Delete 均同事务 `Remove(entity.Node!)` |
| **AC13** | 创建宿主节点时 nodes 行先于宿主行写入 | ✅ 通过 | Create 路径均先 `db.Nodes.Add(node)` 再 `db.XxxTrees.Add(entity)`，FK Restrict 兜底 |
| **AC14** | UnitTree 种子名称在 nodes 可见且 unit_trees 无 name | ✅ 通过 | `UnitTree.SeedNodes()` + `HasData` 预置；快照 `unit_trees` 无 name 列 |

## 2. 实施偏离说明

| 项 | spec 决策 | 实际实施 | 说明 |
|---|---|---|---|
| D1 `Name` 读穿 | `Name => Node?.Name`（纯读穿） | 改为**带后备字段的可写属性**（`_seedName`/`_inputName`） | 原因：① 种子数据 `SeedData()` 中 `Name = "..."` 需 setter 暂存（EF HasData 忽略 [NotMapped]，SeedNodes 消费后备字段）；② SpaceTree/LineNode 的 Create 请求体模型绑定需 setter；③ 前端编辑对话框需 setter。getter 仍优先 `Node?.Name`，后备字段仅在 Node 未加载时兜底。 |
| D4 `ToNodeDto` 签名 | `ToNodeDto<T>(this T, string? text, ...)` | 一致 | — |
| D7 种子 `SeedNodes()` | `static List<Node> SeedNodes()` | 增加 `CreatedAt = DateTimeOffset.UnixEpoch` | 原因：`Node.CreatedAt` 默认 `DateTimeOffset.UtcNow` 导致 `HasData` 模型哈希每次构建不同（`PendingModelChangesWarning` 升级为异常）。固定为 `UnixEpoch` 保证确定性。 |
| 迁移 `Up` 顺序 | ① 回填 → ② 删列 → ③ 加 FK | ① EF `InsertData`（种子）→ ② 回填 SQL（ON CONFLICT）→ ③ 删列 → ④ 加 FK | 原因：若回填先于 `InsertData`，会与种子行主键冲突（duplicate key）。调整后 `InsertData` 先插种子，回填 SQL 用 `ON CONFLICT DO NOTHING` 跳过种子、补插非种子行（SpaceTree/LineNode）。 |

## 3. 修复的遗留问题（实施中发现）

1. **`TreeGraph.Blazor` / `TreeGraph.Blazor.Maui` 页面 `.Text()`**：4+4 个 `*TreePage.razor` 仍用 `context.Text()`，改为 `context.Name`。
2. **测试夹具 `ITreeNodeBase<T>` 缺 `Name`**：`TreeRouteAttributeTests`（2 个）、`TreeEavSkyServiceCollectionExtensionsTests`（1 个）的测试节点类未实现新增的 `Name` 属性，补 `public string? Name => Id;`。
3. **`SourceTextBuilder` 未 Include Node**：`BuildAsync` 对 SpaceTree/UnitTree/CategoryTree/KnowledgeTree/LineNode 的查询缺 `.Include(x => x.Node)`，`Name` 读穿返回 null；`BuildAncestorChainAsync<T>` 用 `EF.Property<string>(n, "Name")` 无法翻译。修复：加 Include + 泛型约束 `ITreeNodeBase<T>` + 客户端读 `.Name`。
4. **`LineStructureService` LINQ `OrderBy(n => n.Name)`**：`Name` 为 `[NotMapped]` 无法翻译。修复：`.Include(n => n.Node)` + `OrderBy(n => n.Node!.Name)` + 投影 `Name = n.Node!.Name`。
5. **`GraphSynchronizer.ResolveNodeAsync`**：5 个宿主查询 `.Select(n => n.Name)` 无法翻译。修复：加 `.Include(n => n.Node)` + 投影 `Name = n.Node!.Name`。
6. **`SubtreeService`**：`GetUnitNameAsync` + `BuildPathAsync` 对树实体的 `Name` 读取。修复：加 `.Include(x => x.Node)`。
7. **`LineNodeCrudTests`**：原测试直接查 `n.Name.Contains` 且不维护 Node 行。修复：TestDbContext 加 Node + FK；辅助方法同步创建 Node 行；搜索改 `n.Node!.Name.Contains`。
8. **`SourceTextBuilderTests`**：种子 SpaceTree 缺对应 Node 行。修复：同步 `db.Nodes.Add(new Node{...})`。

## 4. 未覆盖 / 后续议题（R3）

- 宿主实体仍实现 `Inode`（`Name` 读穿满足），未移除 Inode 约束 — R3 议题。
- 7 个守卫未合并为单一 nodes 守卫 — R3 议题。
- `EdgeInstance` 未注册进 nodes — R3 议题。
- `Line` 实体 `lines.name` 未动 — 不在 R2 范围。

## 5. 最终结论

**AC1–AC14 全部满足，R2 Name 收编实施通过评审。**

- 构建：0 错误
- 测试：Blazor.Shared.Tests 235+1 / Api.Tests 322 / MafWfApi.Tests 65 / FileStorageApi.Tests 153 / Blazor.Tests 71，全部通过
- 迁移：`20261024014158_DropHostNameColumns.cs` 含回填 + 删列 + FK，`Down` 含反向回填
