# R2 · Name 收编（nodes.name 作为全系统节点名称唯一真源）

> 上游蓝图：`specs/node-sky/spec.md` §8.1 / §8.3 第 2 步 / §8.4。
> 前置依赖：R1（DiberyTree 解绑，已完成）—— `Text()` 存在的唯一理由（喂外部 `ITreeItemData<T>`）已消失，方可删列。
> 日期：2026-10-24

## 1. 目标

将节点名称的唯一真源收编到 `nodes.name`。删除四棵宿主树（UnitTree / SpaceTree / CategoryTree / KnowledgeTree）与 LineNode 宿主表的 `name` 列；树/线服务一律从 `nodes` 取名称投影到 `TreeNodeDto.Text`；移除 `ITreeNodeBase<T>.Text()` 及全部实体实现。UI 行为零变化（显示文本由服务层投影提供）。

## 2. 摸底结论（已核实，勿重复调研）

### 2.1 Name 列分布现状

| 实体 | 宿主表 | Name 列 | 备注 |
|---|---|---|---|
| UnitTree | `unit_trees` | `name` NOT NULL varchar(200) | 另有 `abbreviation` varchar(50)，显示用 `Name 【简称】` |
| SpaceTree | `space_trees` | `name` NOT NULL varchar(200) | 无种子数据 |
| CategoryTree | `category_trees` | `name` NOT NULL varchar(300) `[Required]` | 种子：根「项目类型」 |
| KnowledgeTree | `knowledge_trees` | `name` NOT NULL varchar(300) `[Required]` | 种子：根「知识库」 |
| LineNode | `line_nodes` | `name` NOT NULL varchar(200) | 无种子数据 |
| **Node** | `nodes` | `name` **可空** varchar(200) | **唯一真源（本期确立）** |

`Line` 实体（`lines.name`）**不在 R2 范围**（spec §8.3 只列四树 + LineNode）。

### 2.2 Text() 方法分布

- `ITreeNodeBase<T>.Text()` （`TreeGraph.Shared/TreeEavSky/Abstractions/ITreeNodeBase.cs` L24）
- UnitTree.Text() = `Name` 或 `Name 【Abbreviation】`
- SpaceTree.Text() = Name
- CategoryTree.Text() = Name
- KnowledgeTree.Text() = Name

`Inode` 接口（`TreeGraph.Shared/NodeEavSky/Abstractions/Inode.cs`）有 `string? Name { get; }`——宿主实体删 Name 列后**不再满足 Inode**，这是 R3（EAV/Edge 锚点迁移）的触发点（spec §8.5）。

### 2.3 名称消费点（全仓 grep `.Text()` 与 `.Name`）

**服务层投影：**
- `TreeEavSkyHelper.ToNodeDto<T>()`（`TreeGraph.Shared/TreeEavSky/TreeEavSkyHelper.cs`）：`Text = nodeValue.Text()`。四棵树服务的 GetRootNodes / GetChildren / GetFullTree / QueryNodes 均以 `.Select(c => c.ToNodeDto())` 投影。
- 四服务 `CreateNodeAsync`：`Name = nodeDto.Text ?? string.Empty`
- 四服务 `UpdateNodeAsync`：`entity.Name = nodeDto.Text ?? entity.Name`
- 四服务 `QueryNodesAsync`：`c.Name.Contains(searchTerm)` 搜索
- 四服务错误消息：`$"父节点「{parent.Name}」不允许包含子节点"`

**LineNode：**
- `LineNodeController.GetAll`：`n.Name.Contains(search)` 过滤 + `OrderBy(n => n.Name)`
- `LineNodeController.Create`：`node.Name` 非空校验
- `LineNodeController.Update`：`existing.Name = node.Name`

**宿主守卫（Edge/EAV 显示名解析）：**
- `TreeEntityOwnerGuard.cs`：4 个守卫 `GetNodeNamesAsync` 从各宿主表读 Name（UnitTree 还拼简称）
- `LineNodeOwnerGuard.GetNodeNamesAsync`：从 `line_nodes` 读 Name

**前端（Blazor.Shared）：**
- `TreeEavSky.razor.Action.cs`：`formModel.Text()`（L141）、`node.Value?.Text()` 删除确认（L163）、`node.Value.Parent?.Text()` / `selectResult.Text()` 移动提示（L219）
- `DefaultTreeActionHandler.cs`：`parent.Text()`、`child.Text()`、`node.Text()`（L54/L62/L87）
- `TreeNodeDialogService.cs`：`Text()` 调用（待 Edit 时逐处核对）

### 2.4 关键约束

1. **EF Core `Select(c => c.ToNodeDto())` 翻译问题**：`ToNodeDto` 是自定义扩展方法，当前代码在 IQueryable 中直接调用——若 EF Core 无法翻译会退回客户端求值（需在实施时确认，必要时改为先 `ToListAsync` 再投影，或显式 JOIN 投影）。
2. **Inode.Name 编译断裂**：删 Name 列后，宿主实体不再满足 `Inode`。必须有桥接方案（见决策 D1）。
3. **数据回填**：现有宿主行的 name 必须迁入 `nodes`，否则迁移后名称丢失。迁移必须先回填再删列。
4. **种子数据**：UnitTree（~80 条）/ CategoryTree（1 条）/ KnowledgeTree（1 条）的种子 name 必须同步写入 `nodes`。

## 3. 决策

| # | 决策 | 理由 |
|---|---|---|
| **D1** | **R2 提前落地「共享主键 FK」**：每个宿主实体加 `Node? Node` 导航（one-to-one，principal=Node，dependent=宿主，`HasForeignKey<宿主>(x => x.Id)`），宿主 `Id` 兼作指向 `nodes(id)` 的外键。宿主保留 `Inode` 实现，`Name` 改为 `[NotMapped] public string? Name => Node?.Name;` 读穿。 | Name 收编要求名称从 nodes 取；共享主键导航是最干净的访问方式，且是 spec §8.5 明确的一致性兜底机制（DB 层强制"先有 nodes 行才有宿主行"）。提前落地不越界——它是 Name 收编的**使能机制**，R3（守卫收敛/Inode 移除）在此地基上继续。若改为纯应用层双写（无 FK），则无 DB 兜底、孤儿 nodes 行风险高，且任何绕过服务的直读都拿不到 Name。 |
| **D2** | `Abbreviation` 留在 `unit_trees`（不删列、不迁 EAV）；显示文本 `Name 【简称】` 由服务层/守卫投影时拼接（nodes.name + unit_trees.abbreviation）。 | spec 未要求迁 Abbreviation；它不是 Name，是 UnitTree 特有业务字段。 |
| **D3** | `Description` 留在各宿主表，不迁 nodes。 | spec §8.1 明确 nodes 身份内核 = id + host_type + name + 时间戳，不含 Description。 |
| **D4** | `ToNodeDto` 签名改为 `ToNodeDto<T>(this T nodeValue, string? text, string? icon = null, bool hasChildren = false)`；调用方传入从 nodes 取得的名称。 | 实体不再有 `Text()`；IQueryable 投影无法读 `[NotMapped] Name`（EF Core 不翻译），故由调用方显式传名称。 |
| **D5** | 宿主写路径（Create/Update/Delete）**同事务**维护 nodes 行：Create 时先 Add Node 再 Add 宿主；Update 时同步 `Node.Name`；Delete 时先删宿主再删 Node（FK Restrict 兜底顺序）。 | 共享主键 FK 下，DB 要求 nodes 行先于宿主行存在；删除反向。应用层显式编排 + DB FK 双重保障。 |
| **D6** | 迁移 `Up` 顺序：① 对每个宿主表 `INSERT INTO nodes (id, host_type, name, created_at) SELECT id, '<类型>', name, now() FROM <宿主表> ON CONFLICT (id) DO NOTHING`；② `ALTER TABLE <宿主表> DROP COLUMN name`；③ 加 FK `宿主.id → nodes.id` ON DELETE RESTRICT。`Down` 反向（加列、从 nodes 回填 name、删 FK）。 | 保证现有数据名称不丢失；ON CONFLICT 防 GUID 碰撞（实际概率为 0，防御性）。 |
| **D7** | 种子：UnitTree/CategoryTree/KnowledgeTree 各加 `static List<Node> SeedNodes()` 返回同 Id、host_type、name 的 Node 列表；DbContext `ConfigureXxx` 中 `mb.Entity<Node>().HasData(...)` 播种。SpaceTree/LineNode 无种子无需处理。 | EF Core HasData 按主外键顺序插入（Node 先于宿主）。 |
| **D8** | `nodes.name` 保持**可空**（不改为 NOT NULL）。 | spec §8.6 R3 边注册进 nodes 时 name 为 null（派生物）。R2 不改可空性；宿主 name 的非空约束由应用层（Create 校验）+ 历史数据保证。 |
| **D9** | 前端 `.Text()` 调用替换：能拿到 `TreeNodeItem.Text`（服务投影已赋值）的地方用 `node.Text`；需从实体取显示名的地方，改为读实体 `Name`（[NotMapped] 读穿，需导航已加载）或由调用方传入。 | R1 后 `TreeNodeItem` 继承 `TreeItemData<T>`，`Text` 是属性而非方法。 |

## 4. 改动清单

### 4.1 共享层（TreeGraph.Shared）

| 文件 | 改动 |
|---|---|
| `TreeEavSky/Abstractions/ITreeNodeBase.cs` | 删除 `string Text();` |
| `TreeEavSky/Entities/UnitTree.cs` | 删 `Name` 属性；加 `Node? Node` 导航；`[NotMapped] string? Name => Node?.Name;`；删 `Text()`；`Inode.Name` 由读穿满足；加 `SeedNodes()` |
| `TreeEavSky/Entities/SpaceTree.cs` | 同上（无 SeedNodes，无种子） |
| `TreeEavSky/Entities/CategoryTree.cs` | 同 UnitTree；加 `SeedNodes()`（1 条） |
| `TreeEavSky/Entities/KnowledgeTree.cs` | 同 UnitTree；加 `SeedNodes()`（1 条） |
| `LineSky/Entities/LineNode.cs` | 删 `Name`；加 `Node? Node` 导航；`[NotMapped] Name` 读穿 |
| `TreeEavSky/TreeEavSkyHelper.cs` | `ToNodeDto` 加 `string? text` 参数，`Text = text` |

### 4.2 Api 层（TreeGraph.Api）

| 文件 | 改动 |
|---|---|
| `Data/TreeGraphDbContext.cs` | `ConfigureUnitTrees/SpaceTrees/CategoryTrees/KnowledgeTrees/LineNodes`：删 `name` 列配置；加 `HasOne(x => x.Node).WithOne().HasForeignKey<宿主>(x => x.Id).OnDelete(Restrict)`；`ConfigureNodes` 不变；各 `HasData` 追加对应 `Node.SeedNodes()` |
| 迁移 `DropHostNameColumns` | 回填 nodes → 删 name 列 → 加 FK（见 D6） |
| `TreeEavSky/Services/{Unit,Space,Category,Knowledge}TreeService.cs` | ① 读路径：`.Include(x => x.Node)` + 传 `c.Name` 给 `ToNodeDto`；`QueryNodesAsync` 搜索改 `c.Node!.Name.Contains(term)`；错误消息用 `parent.Name`（读穿，Include 已加载）；② Create：同事务 Add Node（host_type）+ 宿主，Name 从 `nodeDto.Text`；③ Update：同步 `entity.Node!.Name = nodeDto.Text`；④ Delete：同事务删 Node |
| `LineSky/Controllers/LineNodeController.cs` | GetAll 搜索改 `n.Node!.Name.Contains` + `OrderBy(n => n.Node!.Name)`；Create 校验 `node.Name`（读穿，但 Node 未加载时为 null——改为校验入参 DTO 的 Name，构造 Node 行）；Update 同步 `existing.Node!.Name`；Create/Delete 同事务维护 Node |
| `TreeEavSky/Services/TreeEntityOwnerGuard.cs` | 4 个守卫 `GetNodeNamesAsync` 改查 `nodes`（UnitTree 守卫 JOIN unit_trees 取 abbreviation 拼接） |
| `LineSky/Services/LineNodeOwnerGuard.cs` | `GetNodeNamesAsync` 改查 `nodes` |
| `NodeSky/Services/NodeOwnerGuard.cs` | 不变（已查 nodes） |

### 4.3 前端（TreeGraph.Blazor.Shared）

| 文件 | 改动 |
|---|---|
| `TreeEavSky/TreeEavSky.razor.Action.cs` | L141 `formModel.Text()` → 由后端返回的 `updated.Text`（`node.Text = updated.Text`）；L163/L219 `entity.Text()` → `entity.Name`（需确保导航加载，或改用 `node.Text`） |
| `TreeEavSky/Services/DefaultTreeActionHandler.cs` | L54/L62/L87 `X.Text()` → `X.Name`（实体读穿）或由 DTO 传入 |
| `TreeEavSky/Dialogs/TreeNodeDialogService.cs` | 逐处 `Text()` → `Name` |

### 4.4 测试同步

- `TreeGraph.Api.Tests/Tests/{Unit,Category,Knowledge}TreeServiceTests.cs`：mock/断言中涉及 `.Name` / `.Text()` 的处同步更新。
- Blazor.Shared.Tests：若有用到 `Text()` 的测试，同步替换。

### 4.5 明确不做（R3 议题）

- 不从宿主实体移除 `Inode`（保留读穿 Name）；
- 不合并 7 个守卫为 1 个 nodes 守卫；
- 不改 Edge 端点校验、EAV 锚点；
- 不将 EdgeInstance 注册进 nodes；
- 不动 `Line` 实体、`lines.name`；
- 不迁 Abbreviation / Description；
- 不改 `nodes.name` 可空性。

## 5. 验收标准（Acceptance Criteria）

### rule
- **AC1**：`unit_trees` / `space_trees` / `category_trees` / `knowledge_trees` / `line_nodes` 五张表**不含** `name` 列（迁移后 `\d <表>` 或 EF 模型快照验证）。
- **AC2**：五张宿主表均存在外键 `id → nodes(id)` ON DELETE RESTRICT（迁移快照验证）。
- **AC3**：`nodes` 表中存在与既有宿主行同 Id、host_type 对应、name 等于原宿主 name 的行（迁移回填验证）。
- **AC4**：`ITreeNodeBase<T>` 接口**不含** `Text()` 方法；四个树实体**不含** `Text()` 方法与 `Name` 映射属性（仅 `[NotMapped]` 读穿）。
- **AC5**：树服务 GetRootNodes / GetChildren / GetFullTree 返回的 `TreeNodeDto.Text` 等于 `nodes.name`（UnitTree 带简称时为 `name 【abbr】`）。
- **AC6**：树服务 Create / Update 写入的名称落到 `nodes.name`，宿主表无 name 列。
- **AC7**：LineNode GetAll 按名称搜索/排序走 `nodes.name`；Create/Update 名称落到 `nodes.name`。
- **AC8**：5 个宿主守卫 `GetNodeNamesAsync` 返回值来自 `nodes.name`（UnitTree 拼简称）。
- **AC9**：前端无 `.Text()` 方法调用（grep 验证）。
- **AC10**：`dotnet build APromisedLand.sln` 0 错误。
- **AC11**：`TreeGraph.Blazor.Shared.Tests` 235 通过 + 1 跳过；`TreeGraph.Api.Tests` 322；`TreeGraph.MafWfApi.Tests` 65。

### rubric
- **AC12**：删除树节点/线路节点时，对应 nodes 行在同事务内被删除，无孤儿 nodes 行（手动验证 + 审查）。
- **AC13**：创建宿主节点时，nodes 行先于宿主行写入（FK 约束 + 代码顺序审查）。
- **AC14**：UnitTree 种子数据的名称在 `nodes` 中可见（host_type='UnitTree'），且 `unit_trees` 无 name 列。

## 6. 风险与缓解

| 风险 | 缓解 |
|---|---|
| EF Core `Select(c => c.ToNodeDto(c.Name))` 无法翻译（[NotMapped] 导航读穿不能进 SQL） | 读路径改为先 `Include(x => x.Node).ToListAsync()` 再客户端 `Select`；或用 `Select` 投影匿名类型 `new { c, name = c.Node!.Name }` 后映射。实施时以构建 + 集成测试验证。 |
| 迁移回填 SQL 在不同 PG 版本语法差异 | 用标准 `INSERT ... SELECT ... ON CONFLICT DO NOTHING`（PG 9.5+ 支持，项目用 PG16）。 |
| 现有数据中宿主 name 为 null（理论上 NOT NULL，但防御） | 回填时 `COALESCE(name, '')`。 |
| `LineNode` 复合备选键 `(line_id, id)` 与新增 FK `id → nodes.id` 冲突 | 不冲突：PK=id，AK=(line_id,id)，FK 基于 id。 |
| 前端 `entity.Name` 读穿在导航未加载时返回 null | 前端实体来自 DTO 反序列化，`Node` 导航需后端序列化时包含——后端 ToNodeDto 的 `Value = nodeValue` 带 Node 导航，序列化后前端 `entity.Name` 可读。若导航为 null，降级用 `node.Text`（DTO 已有）。 |
