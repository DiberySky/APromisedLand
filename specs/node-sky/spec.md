# NodeSky：一等公民通用节点 · 前期研讨结论

> 状态：**研讨完成，等待实现通知**。本文档仅含方案与决策，未写任何代码。
> 日期：2026-10-24

## 1. 需求原文与解读

> 创建 NodeSky 模块 → Node 实体 → 实现 Inode。一等公民通用节点：
> 独立生命周期、不隶属任何节点或组件、可自由创建/销毁/调度；
> 完整 EAV 动态属性（运行时增删改查、原子性、一致性、可扩展）；
> 属性与节点本体解耦存储，属性操作不影响节点独立性。

解读出四个硬性约束：

1. **独立生命周期**：Node 无 ParentId、无 LineId 之类的外键归属，创建/销毁不依赖任何其他实体存活。
2. **通用性**：不带业务类型字段（不设 Kind/NodeType 列），"它是什么"完全交给 EAV 表达。
3. **EAV 全权**：能挂任意实体类型、任意属性，且属性操作与节点本体解耦。
4. **一等公民**：可被 Edge 引用为端点、可参与向量/图同步、在实体类型目录中可见——与 LineNode、四棵树同权。

## 2. 现状盘点（已核实的模型事实）

### 2.1 Inode 宿主接入的既有"接缝"

一个实体要成为 Inode 一等公民宿主，需要触碰的接缝已全部确认：

| 接缝 | 位置 | 作用 |
|---|---|---|
| 实体类 | `TreeGraph.Shared/<模块>/Entities/` | 实现 `Inode`（Id/Name/Description/CreatedAt/UpdatedAt） |
| DbSet + 表配置 | `TreeGraphDbContext.cs`（DbSet L31-52；LineNode 表配置 L820-851 为模板） | 落库 |
| 宿主守卫 | 实现 `IInodeOwnerGuard`（模板：`LineSky/Services/LineNodeOwnerGuard.cs`） | EAV 写入前校验宿主存在；边端点校验；显示名解析 |
| DI 注册 | `Program.cs` L118-128 | 守卫入 `InodeOwnerGuardRegistry` |
| 类型目录播种 | `Data/Seeding/BuiltinHostTypeSeeder.cs` | 让该类型出现在边端点下拉/EAV 挂载目录 |
| CRUD + 事件 | 模板：`LineSky/Controllers/LineNodeController.cs` | 生命周期管理；写后发布 `InodeChangedEvent` 驱动向量/图同步 |

### 2.2 EAV 机制是现成的，无需新造

- 任何已注册守卫的 Inode 宿主，自动获得全套 EAV 端点：
  `PUT /api/inode/{id}/types/{entityType}`（声明类型）、`PUT /api/inode/{id}/entities/{entityType}`（全量替换写值）、查询/删除同理。
- 属性存于 `inode_entities` / 属性值表，与宿主主表物理分离 —— 天然满足"属性与本体解耦"。
- 原子性/一致性由 `EavWriteService` + 统一异常管道（`EavValidationException` → 400）保证。
- **结论：NodeSky 不需要写任何 EAV 代码，注册守卫即获得全部能力。**

### 2.3 与既有节点的差异定位

| | 层级 | 归属外键 | 销毁约束 |
|---|---|---|---|
| TreeNode ×4 | ✅ ParentId | 属于树 | 子节点存在时受限 |
| LineNode | ❌ | **LineId 必填**（Restrict FK） | 边/端子/实体数据引用时拒绝 |
| **Node（拟建）** | ❌ | **无任何归属** | 边/实体数据引用时拒绝（沿用 LineNode 语义） |

Node = 唯一"无归属"的节点。这是它存在的全部理由，也是它不可替代的价值。

## 3. 方案选型

### 候选 A：扩展 LineNode（加可空 LineId）

- 优点：零新表。
- 否决理由：归属语义被可空化后，"线路节点必须属线"的约束退化为应用层自觉；现有 `ak_line_nodes_line_id_id` 复合备选键、段端点 FK 全部失效。污染既有模型。

### 候选 B：不加实体，EAV 数据直接漂浮

- 优点：零成本。
- 否决理由：违背系统核心守卫设计——`InodeOwnerGuardRegistry.EnsureNodeExistsAsync` 存在的意义就是**禁止属性数据脱离宿主凭空产生**。无宿主的 EAV 是孤儿数据，违反需求"节点本体独立存在"。

### 候选 C：新建 NodeSky · Node（**选定**）

- 新建最小实体 + 全套接缝接入。
- 满足全部四条硬约束，不污染任何既有模型，成本可控（约 6 个新文件 + 3 处既有文件改动）。

### Node 实体字段（一期，已按 §8 演进方向修订）

```
Id / HostType / Name / Description / CreatedAt / UpdatedAt
```

- **加 HostType**（默认 `"Node"`）：为「LineNode/TreeNode 来源于 Node」重构预留身份注册表地基，详见 §8。
- **不加 Kind/Type 业务分类列**：种类用 EAV 实体类型声明表达（一个 Node 声明 `Equipment` 类型即成为设备），避免硬编码列限制通用性。
- **不加 ParentId/LineId**：归属关系一律走 Edge（跨类型端点校验现成，`EdgeNodeValidator` 按 NodeType 解析守卫）。
- 表名 `nodes`，列风格沿用 snake_case，Id 36 字符 GUID + `gen_random_uuid()::text` 默认值，与 `line_nodes`/`lines` 同构。

## 4. 销毁语义（生命周期管理的关键决策）

沿用 LineNodeController.Delete 的"**拒绝级联，显式清理**"语义，理由是其已被验证且能防误删：

1. 有 Edge 引用（From/To 命中）→ 409 拒绝；
2. 仍挂实体数据（`inode_entities` 命中）→ 409 拒绝，要求先清空；
3. 类型声明（`inode_entitytypes`）无业务数据，随宿主一并删除；
4. 删除成功后发布 `InodeChangedEvent(Deleted)`，向量/图消费端幂等清除残留。

与 LineNode 的唯一差异：Node 不存在"线路端子"（line_terminals）引用检查——它不可能是线路结构端子。

## 5. 风险与注意事项

| 风险 | 等级 | 缓解 |
|---|---|---|
| **命名冲突**：`Node` 是高频词（图论/DOM/Blazor 组件），可能与既有类撞名 | 中 | 实体全名 `TreeGraph.Shared.NodeSky.Entities.Node`；DbSet 名 `Nodes`；表名 `nodes`。引用处一律带命名空间，禁 using 缩写冲突 |
| **通用节点泛滥**：调用方把本该挂树/挂线的点都建成自由 Node，层级与线路语义被稀释 | 中 | 文档约定：能进树进树、能上线上线，Node 只承载"确实无归属"的点（如独立设备、临时设施） |
| **迁移顺序**：新迁移与现有迁移链合并冲突 | 低 | 单人开发，直接 `dotnet ef migrations add AddNodeSkyNode` |
| **前端无入口**：一期后端先行，Blazor 无 Node 管理页 | 可接受 | Node 的价值在被引用（边端点/EAV 宿主），这两处前端是通用组件，注册目录后自动可见 |
| **图同步**：NebulaGraph 侧顶点标签需识别新 NodeType | 低 | `InodeChangedEvent` 管道按 InodeId 同步，消费端对宿主类型不可知；注册守卫后名字解析自动生效 |
| **DiberyTree 解绑面**（重构期）：外部包 `ITreeItemData<T>` 在 TreeEavSky 组件族（约 10+ 文件）中渗透较深，替换为本地类型是逐文件手术 | 中 | 逐文件替换 + 全量构建 + Blazor.Shared.Tests（235+1）回归；树组件本体是本地克隆，UI 行为不应有肉眼变化 |

## 6. 范围界定

### 本期做（收到实现通知后）

1. `TreeGraph.Shared/NodeSky/Entities/Node.cs`（实现 Inode）
2. `TreeGraphDbContext`：DbSet `Nodes` + `ConfigureNodes()`（表 `nodes`）+ 在 `OnModelCreating` 登记
3. EF 迁移 `AddNodeSkyNode`
4. `TreeGraph.Api/NodeSky/Services/NodeOwnerGuard.cs`（NodeType = "Node"）
5. `TreeGraph.Api/NodeSky/Controllers/NodeController.cs`（GetAll/GetById/Create/Update/Delete，克隆 LineNodeController 并去掉 LineId 与端子检查）
6. `Program.cs` 注册守卫；`BuiltinHostTypeSeeder` 增加 "Node" 目录项
7. 构建 + 既有测试回归；视情况补 NodeOwnerGuard 单测

### 本期不做（明确排除）

- Blazor 前端管理页（通用组件自动可见，专门管理页待关联场景明确）
- Node 专属 DTO/验证规则（一期实体即 DTO，与 LineNodeController 同风格）
- 所内设备模型的具体 EAV 类型定义（Equipment 属性清单属于业务建模，是下一步独立议题）
- Node ↔ Tree/Line 的任何外键（关联一律走 Edge）

## 7. 与「所内设备组织」讨论的衔接

Node 落地后，上一轮方案 C 升级为：

- **所/间隔** = SpaceTree 节点（层级包含）；
- **设备** = Node（独立身份，挂 Equipment 等 EAV 类型）；
- **设备属于间隔** = Edge（Node ↔ SpaceTree，跨类型端点校验现成）；
- **所在线路上的位置** = LineNode（与所树节点软关联）。

设备由此获得独立身份，可被多处引用而不复制数据——这正是"通用节点"需求的真实业务动机。

## 8. 演进方向（用户已确认）：LineNode/TreeNode 来源于 Node

> 下一步重构目标：让既有 LineNode 与四棵 TreeNode 都"来源于"Node。
> 这将 Node 的定位从「一种新节点类型」升级为「**全系统节点的身份内核**」。

### 8.1 三条可行路径

**路径 ① 类继承（EF TPT/TPH）**：LineNode/TreeNode 继承 Node 基类，nodes 为基表、各宿主为子表。
- 问题：四棵树已实现 `ITreeNodeBase<T>` 泛型自引用、各有 ParentId/种子数据/既有迁移链；改继承等于重写全部宿主模型 + 大规模数据迁移；EF 每查询多一次 join。**否决**。

**路径 ② 组合引用（宿主表加 NodeId FK）**：每个树节点/线路节点持有一个 nodes 行，NodeId 为外键。
- 问题：双 id 并存（宿主自身 id + node id），边端点、EAV inode_id、前端路由全要区分"用哪个 id"；迁移期一致性难保证。**否决**。

**路径 ③ 身份注册表（选定）**：**宿主表 id 即 Node id**。nodes 表存身份内核（id + host_type + **name（唯一真源）** + 时间戳）：
- 创建宿主节点时，同事务在 nodes 注册一行（host_type = "LineNode"/"SpaceTree"/…）；
- 独立 Node（本期实体）= host_type 为 "Node" 的"裸身份"，不挂任何宿主扩展表；
- 销毁时反向：先清宿主行，再注销 nodes 身份；
- 收益：**id 全局唯一且可溯源**（拿到任意 id 查 nodes 即知类型与名字）；`GetNodeNamesAsync` 收敛为单表查询；Edge 端点校验可先查 nodes 再分流；NebulaGraph 顶点有了统一的"身份层"。
- 代价：写路径多一次 nodes 插入（同事务，开销可忽略）；需要一次性数据回填迁移（把既有宿主 id + name 灌进 nodes）。

**Name 的归属（2026-10-24 定稿：收编）**：**Name 唯一真源在 nodes 表，各宿主表删除 Name 列**——
- 曾议"镜像方案"（宿主保留 Name 为权威、nodes.name 为副本），因双写/漂移/对账是永久税，且项目处于研发阶段、破坏性改动成本最低，**翻转否决镜像**；
- 收编的关键解锁条件：**移除 DiberyTree 组件绑定**（见 §8.4）——实测 `ITreeNodeBase<T>` 接口本就不含 Name，显示契约是 `Text()` 方法，而 `Text()` 存在的唯一理由就是喂给外部 DiberyTree 包的 `ITreeItemData<T>` 包装器；解绑后该理由消失；
- 收编后的读取模式：树/线服务查询一律 join nodes 取 name（或两步查询后在服务层投影）；改名操作只写 nodes 一处，树显示/边端点/图顶点/搜索天然一致。

### 8.2 对一期设计的影响

一期（本期 Node）按路径 ③ **预留地基**，成本极低、避免二次迁移：

1. **nodes 表增加 `host_type` 列**（string，16 字符，非空，默认 `"Node"`）——一期独立 Node 全部为 "Node"；重构期各宿主写入自己的类型名。
2. **NodeType 字符串定名 "Node"** 与 host_type 取值统一。
3. NodeController 的写路径按"注册表语义"写 nodes 行——因为一期 nodes 表本身就是宿主主表，天然满足；重构期只是给 nodes 增加"别人也往里注册"的角色。

> 即：一期的 nodes 表既是「独立 Node 的宿主主表」，也是「未来全系统节点身份注册表」。两个身份合一，不重复建表。

### 8.3 重构期（后续阶段，不在本期范围）预告

执行顺序（依赖关系自上而下）：

1. **前置：DiberyTree 解绑**（详见 §8.4）——清除 Name 收编的障碍；
2. **Name 收编**：四棵宿主树 + LineNode 删除 Name 列，树/线服务全部改 join nodes 取名字（或两步查询后在服务层投影）；`Text()` 随 DiberyTree 解绑一并移除；
3. **EAV/Edge 锚点迁移到注册表**（详见 §8.5）：宿主实体不再实现 Inode，7 个守卫收敛为 1 个 nodes 守卫；宿主表主键兼作指向 nodes(id) 的外键（共享主键模式）；
4. 数据迁移：既有 LineNode/四树节点 id + name 回填 nodes（host_type 各异），幂等脚本；与各宿主表 PK→FK 改造同窗进行；
5. 各宿主 Controller 创建/删除路径接入"同事务注册/注销"（有共享主键 FK 后，DB 层兜底注册一致性）；
6. `InodeOwnerGuardRegistry` 多守卫聚合机制简化或退役；
7. **边系统整体重建**——按 §8.6 定稿执行（边端点真 FK、edge_definitions 表消亡等，原「边端点补真 FK」单项计划被 §8.6 取代）。

### 8.4 DiberyTree 解绑（用户指令 2026-10-24）

> 「移除所有关于 DiberyTree 组件的绑定」。移除的是与**外部 DiberyTree 包**的接口绑定，不是删除树 UI——树组件本体是本地克隆，继续承担树显示。

**绑定清单（实测）**：

| 绑定点 | 来源 | 处置 |
|---|---|---|
| `ITreeNodeBase<T>` / `IHierarchyTreeNodeBase<T>` / `IArchivableTreeNodeBase<T>` | 本地克隆（`TreeGraph.Shared/TreeEavSky/Abstractions/`） | 保留并修订：移除 `Text()`（名字改由服务层投影），保留结构契约（Id/ParentId/SortOrder/HasChildren/CanHaveChildren/Parent/Description） |
| `ITreeItemData<T>` / `TreeItemData<T>` | **外部 NuGet 包**（Rider SourcesCache 反编译路径证实） | **全部替换为本地类型**或去包装化（直接消费实体/DTO） |
| `TreeEavSky.razor` 组件族（Loading/Node/Action/TreeHelper/Dialogs…） | 本地克隆（`TreeGraph.Blazor.Shared/TreeEavSky/`），内部消费外部 `ITreeItemData<T>` | 跟随包装器替换逐文件改造；解除 csproj 对外部 DiberyTree 包的引用 |
| `APromisedLand.Razor/DiberyTree/`（旧主项目同款克隆） | 本地 | 同策略，随 TreeGraph 侧同步处理（旧项目是否仍在服役待确认） |

**解绑后才可行的事**：宿主实体删 Name 列（`Text()` 无米下锅的问题随绑定移除而消失）；树组件改为消费"实体 + 服务层投影的 Name"DTO。

### 8.5 EAV/Edge 锚点迁移到注册表（2026-10-24 用户确认）

> 用户问：「line 和 tree 是否都可以不用实现 eav 和 edge？」——结论：**可以，且 Name 收编后这是必然结果**。

**为什么是必然**：`Inode` 接口要求 `Name` 属性；Name 收编进 nodes 后，宿主实体删 Name 列，在类型上即不再满足 Inode——EAV/Edge 的锚点**必须**迁到 nodes 注册表，而非可选优化。

**终态机制对照**：

| 现在 | 终态 |
|---|---|
| 每个宿主实现 `Inode` + 注册自己的 `IInodeOwnerGuard`（共 7 个） | 只有 **Node 实现 Inode**；守卫收敛为 **1 个**（NodeOwnerGuard 查 nodes 表） |
| EAV 写入前：遍历守卫，任一确认宿主存在即放行 | EAV 写入前：nodes 一行存在即放行 |
| Edge 端点校验：按 FromNodeType/ToNodeType 字符串解析对应守卫再查表 | 查 nodes 行且 `host_type == 声明的类型`（单表单查询） |
| `GetNodeNamesAsync`：跨守卫聚合查询各宿主表 | nodes 单表直查（Name 已收编，天然配套） |
| BuiltinHostTypeSeeder 手工维护类型目录 | 不变（seed 的是类型目录项，与守卫脱钩） |

**一致性保证：共享主键模式**——各宿主表主键兼作指向 nodes(id) 的外键：插入宿主行必须先有 nodes 行，注册一致性由 DB 兜底，而非应用层自觉。反向孤儿（nodes 有行、宿主行缺失）DB 防不住，靠写路径同事务 + 删除顺序（先宿主行后注册表行）控制。

**架构终态**：**EAV 和 Edge 只认识 nodes 注册表，不认识任何具体宿主**；Tree/Line 退化为"给 nodes 身份附加结构数据（ParentId/LineId…）的扩展表"。身份（nodes）、属性（EAV）、关系（Edge）三层各管各的，宿主只剩"形状"。净效果是大规模删代码（7 守卫 → 1，聚合注册表退役）。

### 8.6 Edge 简化重构（2026-10-24 定稿）

> 用户指令：「edge 模块实现过于复杂……仅实现两个 Node 的关系，有向无向。其他的交由 eav。」
> 补充指令：「清空原来有关 edge 所有的 seeds，不必考虑兼容性。」
> 与 Node 的设计哲学同源：Node 不带 Kind 列，边也不带业务字段——"这是什么关系"交给 EAV。

#### 终态表结构（edge_instances 重建）

```
edge_id        PK（注册进 nodes，host_type = "EdgeInstance"）
from_node_id   FK → nodes(id)，OnDelete = Restrict
to_node_id     FK → nodes(id)，OnDelete = Restrict
type_name      string，裸列（无 FK 表）——身兼三职：EAV entity_type 关联键、
               唯一约束判别列、NebulaGraph 边类型名
is_directed    bool，实例列（"有向/无向"是边内核语义，不交 EAV）
created_at / updated_at
```

- 唯一索引 `(type_name, from_node_id, to_node_id)`；无向边反向视为重复（应用层判定，沿用现状）；
- 查询索引：from / to 各一；
- `type_name` 必须留列的理由：无它则 (from,to) 唯一约束会禁止两点间并存多种关系，图同步类型名与 EAV 关联键也无处安放；但从独立表降级为裸字符串列。

#### 删除清单（Edge 模块减重主体）

| 删除项 | 去向/理由 |
|---|---|
| `edge_definitions` 整表、EdgeDefinition 实体、EdgeMetadataService、EdgeMetadataController | 类型目录并入 EAV `entity_type_definitions`；**18 条内置种子全部废弃清空**（用户指令），业务关系类型后续按需在 EAV 目录重建 |
| `IsGlobal` / `IsActive` / `FromNodeType` / `ToNodeType` | 存在性由 nodes FK 强制；类型约束不进库，谁需要谁在业务层自查。**IsGlobal 端点不校验存在的现状漏洞随之消亡** |
| `Weight` / `DisplayName` 列 | EAV 属性（既有数据不迁移，随清库丢弃） |
| `EdgeNodeValidator` | nodes 单查 + FK 取代 |
| `IsLine` / `IsLinear` / `EnforceSingleIncoming` + 部分唯一索引 | 线路语义撤出通用边管道（见 L1） |
| EdgeInstance 显式实现 `Inode`、`EdgeOwnerGuard` | 边注册进 nodes，EAV 宿主校验由唯一 nodes 守卫覆盖 |

#### 边与 nodes 注册表（R3 定稿）

- 建边：同事务写 nodes 行（host_type="EdgeInstance"，**name = null**）+ edge_instances 行；
- 删边：先清 EAV 属性/挂载实体（沿用"显式清理"语义）→ 删 edge_instances 行 → 注销 nodes 行；
- **`nodes.name` 改可空**：边的名字是派生物，显示名投影「from名 → to名」或读 EAV 的 DisplayName 属性；
- 删除检查统一为一句：**任何 nodes 行被 edge_instances.from/to 引用 → 409**（FK Restrict 兜底，应用层保留友好报错）；超边能力（边作端点）机制保留。

#### 线路结构归位（R2 定稿：L1 自立门户）

- `line_structure_segments` 自存 from/to/line_id，**不再引用 edge_instances**；复合 FK（段端点同属该线路）保留；
- 入度/成环/同线等拓扑校验闭环在 LineSky 内部，强度自控；
- `line_structures.edge_type_id` 退役——线路语义类型（输电/馈线…）走 EAV；
- 通用边遍历、NebulaGraph 同步不含线路拓扑；LineSky 如需图同步自行发事件；
- 通用边模块完全不认识"线路"。

#### 图同步与向量

- `EdgeChangedEvent` 保留，边类型名 = `type_name`，同步逻辑不变；
- 顶点解析从"逐守卫试"变 nodes 单查（tag ← host_type，name 已收编）；
- 边注册进 nodes 后，`InodeChangedEvent` 管道天然覆盖边的顶点（如需边顶点入库）。

#### 迁移策略（用户指令：不做兼容）

清空 edge 全部既有数据与种子，`edge_definitions` / `edge_instances` 删表按新结构重建；不做任何旧数据回填。

### 8.7 六元素全景模型（2026-10-24 评估定稿）

> 用户设想：**点(Node)、线(Line)、面(Tree)、边(Edge)、动态属性(EAV)、图(NebulaGraph)** 全元素方案。
> 评估结论：成立，与 §8.5/§8.6 终态同构——六元素不是平权六兄弟，而是**四层结构 + 一个投影**。

**分层模型**：

| 层 | 元素 | 职责 |
|---|---|---|
| 身份层 | 点 Node | nodes 注册表，唯一身份真源；树节点/线节点/自由节点/边都是"点" |
| 属性层 | 动态属性 EAV | 唯一横切层，挂在点与边上；不产生身份，只描述身份 |
| 关系层 | 边 Edge | 点与点的**自由语义关系**（有向/无向 + type_name + EAV 语义） |
| 结构层 | 面 Tree / 线 Line | **受约束的结构关系**：层级包含（单父、无环）/ 段序列拓扑（同线、入度约束）——各有专属表，因需要 DB 级强约束 |
| 投影层 | 图 NebulaGraph | 只读投影，非真源；图内一切坍缩为顶点+边 |

**三条核心洞察**：

1. **真源多元，投影二元**：PG 保约束与事务，图保遍历性能；六张表进图坍缩为顶点+边（点=顶点，边=边，面父子=parent_of 边，线段=结构边）。
2. **面/线是受约束的关系，边是自由关系**——这是结构层独立成表的根本原因，也是 L1 决策的精神内核。
3. **EAV 横切点与边**，终态只认 nodes 注册表。

**两条架构原则（定稿）**：

1. **图是投影，不是真源**——一切数据以 PG 为准，图可重建、可丢弃；
2. **建模选用决策树**——有层级归属→面（树）；有顺序拓扑→线；无归属独立身份→点；点间自由语义→边；"还有什么特征"→EAV；全局遍历分析→图（自动投影）。（承接 §5「能进树进树、能上线上线」公约）

**已知风险与对策**：

| 风险 | 对策 |
|---|---|
| 「面」与 GIS 面要素命名歧义 | 内部术语固定为「树」，「面」仅作口头比喻 |
| 图双写一致性（投影滞后/漂移） | 低优先级补对账工具（PG vs 图抽样比对） |
| 查询分裂（结构内走 PG 递归 CTE、跨结构走图） | 服务层收敛查询门面，屏蔽双引擎 |
| **线入图**：L1 后线路拓扑默认不进图 | **定稿：线要入图**——由 LineSky 自发事件同步结构边，PG 内保持解耦、图内统一拓扑（§8.6 L1 代价的补全） |

## 9. 开放问题（实现前可一并确认）

1. ~~NodeType / host_type 定名 `"Node"` 是否接受？~~ **已定（2026-10-24）：实体名保留 `Node`，NodeType/host_type 字符串 = `"Node"`，DisplayName 配"通用节点"。**（曾评估 NodeSky/BaseNode/NodeBase/FreeNode，均否决：解决方案内无 `class Node` 冲突；Base 系暗示不存在的继承关系；NodeSky 破坏"Sky=模块"惯例）
2. ~~Delete 拒绝语义是否沿用（引用存在时 409），还是希望提供 `?force=true` 级联清理解放？~~ **已定（2026-10-24）：不做 force 级联，一律 409 拒绝 + 显式清理。**
3. ~~是否需要 `GET /api/node/{id}/summary` 这类聚合端点（本体+已声明类型+边计数）？~~ **已定（2026-10-24）：一期不做。等前端节点详情视图有真实字段需求时再按需添加。**
4. ~~确认接受 §8.2 的地基预留——nodes 表一期即带 `host_type` 列（默认 "Node"）？~~ **已定（2026-10-24，用户授权拍板）：接受。§8.6 Edge 重建整体压在此地基上，一期 nodes 表即带 `host_type` 非空列、默认 `"Node"`。**
5. ~~**DiberyTree 解绑的时机**：并入重构期第 1 步（推荐——一期 Node 不依赖它），还是提前到本期先行清场？~~ **已定（2026-10-24，用户授权拍板）：并入重构期第 1 步，一期不动。**

**§9 全部关闭（2026-10-24）。研讨阶段结束，只等实现通知。**

---

**下一步：等待实现通知。收到指令后按 §6「本期做」清单 + §8.2 地基预留执行。**
