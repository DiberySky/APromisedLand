# R2 · Name 收编 · 施工任务

> 依据 `specs/node-sky/r2-name-collect/spec.md`。每步可编译性由最终全量构建兜底；涉及 EF 迁移的步骤需在真实 PG 或 `dotnet ef` 下验证。

## 阶段一：共享层（实体 + 接口 + Helper）

- [ ] **T1** `ITreeNodeBase.cs`：删除 `string Text();`
- [ ] **T2** `UnitTree.cs`：删 `Name` 属性；加 `using TreeGraph.Shared.NodeSky.Entities;`；加 `public Node? Node { get; set; }` 导航；加 `[NotMapped] public string? Name => Node?.Name;`；删 `Text()` 方法；新增 `static List<Node> SeedNodes()`（同 Id、host_type="UnitTree"、name=原名）
- [ ] **T3** `SpaceTree.cs`：同 T2（无 SeedNodes）
- [ ] **T4** `CategoryTree.cs`：同 T2；加 `SeedNodes()`（1 条，host_type="CategoryTree"）
- [ ] **T5** `KnowledgeTree.cs`：同 T2；加 `SeedNodes()`（1 条，host_type="KnowledgeTree"）
- [ ] **T6** `LineNode.cs`：删 `Name`；加 `Node? Node` 导航；`[NotMapped] Name` 读穿
- [ ] **T7** `TreeEavSkyHelper.cs`：`ToNodeDto` 加 `string? text` 参数，`Text = text`
- [ ] **T8** 构建 `TreeGraph.Shared` 项目，0 错误（验证接口/实体改动自洽）

## 阶段二：DbContext 配置 + 迁移

- [ ] **T9** `TreeGraphDbContext.cs`：5 个 `ConfigureXxx` 删 name 列配置；加 `HasOne(x => x.Node).WithOne().HasForeignKey<宿主>(x => x.Id).OnDelete(DeleteBehavior.Restrict)`；`ConfigureUnitTrees/CategoryTrees/KnowledgeTrees` 追加 `mb.Entity<Node>().HasData(UnitTree/CategoryTree/KnowledgeTree.SeedNodes())`
- [ ] **T10** `dotnet ef migrations add DropHostNameColumns --project TreeGraph.Api` 生成迁移骨架
- [ ] **T11** 手编迁移 `Up`：① 5 段 `INSERT INTO nodes (id, host_type, name, created_at) SELECT id, '<类型>', COALESCE(name, ''), now() FROM <宿主表> ON CONFLICT (id) DO NOTHING`（类型依次 UnitTree/SpaceTree/CategoryTree/KnowledgeTree/LineNode）；② `DROP COLUMN name`；③ 加 FK（EF 自动生成，确认 ON DELETE RESTRICT）；`Down` 反向（加列、从 nodes 回填 name、删 FK）
- [ ] **T12** `dotnet ef database update` 验证迁移在 PG16 上执行无错（或 Api.Tests 的 Testcontainers 跑迁移）

## 阶段三：Api 服务层（四树 + LineNode + 守卫）

- [ ] **T13** `UnitTreeService.cs`：① 读路径加 `.Include(x => x.Node)`，`ToNodeDto` 传 `c.Name`；`QueryNodesAsync` 搜索改 `c.Node!.Name.Contains`；错误消息 `parent.Name`（Include 已加载）；② Create：同事务 `dbContext.Nodes.Add(new Node{Id=..., HostType="UnitTree", Name=nodeDto.Text})` + Add 宿主；③ Update：`entity.Node!.Name = nodeDto.Text ?? entity.Node!.Name`；④ Delete：同事务 `dbContext.Nodes.Remove(node.Node!)`
- [ ] **T14** `SpaceTreeService.cs`：同 T13（host_type="SpaceTree"）
- [ ] **T15** `CategoryTreeService.cs`：同 T13（host_type="CategoryTree"）
- [ ] **T16** `KnowledgeTreeService.cs`：同 T13（host_type="KnowledgeTree"）
- [ ] **T17** `LineNodeController.cs`：GetAll `.Include(n => n.Node)` 搜索/排序走 `n.Node!.Name`；Create 校验入参 Name、构造 Node 行同事务写入；Update 同步 `existing.Node!.Name`；Delete 同事务删 Node
- [ ] **T18** `TreeEntityOwnerGuard.cs`：4 个守卫 `GetNodeNamesAsync` 改查 `dbContext.Nodes.Where(n => ids.Contains(n.Id))`；UnitTree 守卫 JOIN `unit_trees` 取 abbreviation 拼接 `$"{n.Name} 【{abbr}】"`
- [ ] **T19** `LineNodeOwnerGuard.cs`：`GetNodeNamesAsync` 改查 `nodes`

## 阶段四：前端（Blazor.Shared）

- [ ] **T20** `TreeEavSky.razor.Action.cs`：L141 `node.Text = formModel.Text()` → `node.Text = updated?.Text ?? node.Text`（用后端返回的 DTO.Text）；L163 `node.Value?.Text()` → `node.Text`；L219 `node.Value.Parent?.Text()` → 父节点的 `Text`（从 TreeNodeItem 取），`selectResult.Text()` → `selectResult.Name` 或对应 DTO.Text
- [ ] **T21** `DefaultTreeActionHandler.cs`：L54/L62/L87 `X.Text()` → `X.Name`（实体读穿）；确认导航已加载
- [ ] **T22** `TreeNodeDialogService.cs`：逐处 `Text()` → `Name`
- [ ] **T23** 全仓 grep `\.Text\(\)` 确认 TreeGraph.* 下无残留（旧项目 APromisedLand.* 允许）

## 阶段五：测试同步

- [ ] **T24** `TreeGraph.Api.Tests/Tests/{Unit,Category,Knowledge}TreeServiceTests.cs`：更新 mock DbSet 中 Node 导航、Name 断言、Text() 调用
- [ ] **T25** Blazor.Shared.Tests 中涉及 `Text()` 的用例同步替换

## 阶段六：验证

- [ ] **T26** 无 `TreeGraph.*` / `testhost*` 残留进程锁 DLL
- [ ] **T27** `dotnet build APromisedLand.sln` 0 错误
- [ ] **T28** `dotnet test TreeGraph.Blazor.Shared.Tests` → 235 通过 + 1 跳过
- [ ] **T29** `dotnet test TreeGraph.Api.Tests` → 322
- [ ] **T30** `dotnet test TreeGraph.MafWfApi.Tests` → 65
- [ ] **T31** grep 核对：五张宿主表无 name 列（迁移快照）、无 `.Text()` 残留、`nodes` 有回填数据

## 阶段七：评审

- [ ] **T32** 独立评审（spec review.md）：核对 AC1–AC14 全部满足

---

**本地测试要求（task-level）：**
- T8：rule — `TreeGraph.Shared` 编译 0 错误
- T12：rule — 迁移在 PG16 上 `database update` 成功
- T27：rule — 全解决方案 0 错误
- T28/T29/T30：rule — 三套测试数与基线一致（235+1 / 322 / 65）
- T31：rule — grep 无残留
