# DiberyTree 项目概述与实现功能

---

## 一、项目概述

### 1.1 项目定位

**DiberyTree** 是一个基于 **.NET 10 + ASP.NET Core + EF Core + PostgreSQL + .NET Aspire** 构建的 **树形结构与动态属性系统** 后端服务。

它的核心目标是：为业务系统提供一套**可扩展的树形数据 + 用户自定义属性 + 动态表**的数据建模能力。用户可以在不修改数据库结构的前提下：

- 定义任意层级的树（分类、单位等）
- 定义属性（文本、数值、日期、文件、定位、表格等）
- 给任意树节点挂载属性值
- 定义"动态表"（表结构由用户定义，行数据由用户录入）
- 管理经纬度定位值

### 1.2 技术栈

| 类别 | 技术 |
|---|---|
| 运行时 | .NET 10 |
| Web 框架 | ASP.NET Core Web API |
| ORM | EF Core 10（TPC 继承映射） |
| 数据库 | PostgreSQL（Npgsql） |
| 编排 | .NET Aspire（AppHost） |
| 缓存 | `IMemoryCache`（幂等去重） |
| API 文档 | Microsoft.AspNetCore.OpenApi |
| 日志/追踪 | OpenTelemetry（Aspire 默认） |
| 认证 | 未启用（仅 `UseAuthorization`） |

### 1.3 项目结构

```
DiberyTreeService (Web 启动)
├── APromisedLand.Api
│   ├── Data/               DiberyDbContext
│   ├── DiberyTree/
│   │   ├── Controllers/    控制器基类（TreeControllerBase、AttributesControllerBase 等）
│   │   ├── Interface/      ITreeService<T>、ITreeAttributeService
│   │   └── Services/       TreeService、AttributeService（partial 拆分）
│   └── Services/
├── APromisedLand.ServiceDefaults
└── APromisedLand.Shared
    ├── DiberyTree/
    │   ├── Attributes/
    │   │   ├── DTOs/       数据传输对象
    │   │   ├── Enums/      AttributeTypeEnum
    │   │   ├── Models/     AttributeDefinition、AttributeValueBase 及派生类
    │   │   └── Validation/ DefinitionValidator、ValueValidator
    │   ├── Interfaces/     ITreeNodeBase、IHierarchyTreeNodeBase 等
    │   └── Models/         CategoryTree、UnitTree、TreeNodeDto、TreeQueryParams
    └── DTOs/               ApiResponse 等
```

### 1.4 核心设计理念

1. **树与属性解耦**：树只管结构（父子关系），属性定义（schema）独立于树，属性值挂在树节点上。
2. **属性定义与属性值分离**：`AttributeDefinition` 描述"是什么"，`AttributeValueBase` 存储"值是什么"。
3. **动态表 = 表定义 + 列定义 + 行实例 + 列值**：通过 `ParentId` 自引用表达"表 → 列"关系，行实例是 `TableRowAttributeValue`，列值是各 Typed Value。
4. **TPC 继承映射**：每种属性值类型独立建表，避免单表宽表。
5. **幂等去重**：所有写操作基于 SHA256 指纹 + 内存缓存做 30 秒窗口去重。

---

## 二、领域模型

### 2.1 树模型

| 模型 | 说明 |
|---|---|
| `CategoryTree` | 分类树节点（Id / Name / ParentId / SortOrder / HasChildren 等） |
| `UnitTree` | 单位树节点（额外含 `Abbreviation` 缩写） |
| `ITreeNodeBase<T>` | 树节点接口，定义 `Id / ParentId / Text() / SortOrder` 等 |
| `TreeNodeDto<T>` | 树节点传输对象，含 `Children`、`Expanded`、`Selected` |
| `TreeQueryParams` | 查询参数（ParentId、SearchTerm、OnlyWithChildren、分页） |

### 2.2 属性模型

**属性定义 `AttributeDefinition`**：

| 字段 | 说明 |
|---|---|
| `Id` | GUID 字符串 |
| `Name` | 定义名称 |
| `AttributeTypeId` | 类型 ID（映射到 `AttributeTypeEnum`） |
| `Lines` / `MaxLength` | 文本专用 |
| `Precision` / `Scale` | 数字专用 |
| `UnitId` | 单位引用（指向 `UnitTree`） |
| `ParentId` | 自引用：列为表定义下的列；表定义自身为 null |
| `HasDate` / `HasTime` / `HasRowNo` | 动态表列的元信息 |
| `Order` / `IsRequired` | 列顺序与必填 |

**支持的属性类型（`AttributeTypeEnum`，共 15 种）**：

```
文本、整数、小数、日期、时间、日期时间、文件、定位、表格、
复合、点、标签、边、边类型、属性
```

**属性值 `AttributeValueBase`（TPC 继承）及其派生**：

| 派生类 | 存储表 | 值字段 |
|---|---|---|
| `TextAttributeValue` | TextAttributeValues | `Value: string` |
| `IntegerAttributeValue` | IntegerAttributeValues | `Value: long` |
| `DecimalAttributeValue` | DecimalAttributeValues | `Value: decimal` |
| `DateAttributeValue` | DateAttributeValues | `Value: DateTimeOffset` |
| `TimeAttributeValue` | TimeAttributeValues | `Value: TimeSpan` |
| `DateTimeAttributeValue` | DateTimeAttributeValues | `Value: DateTimeOffset` |
| `FileAttributeValue` | FileAttributeValues | `Value` / `FileName` / `ContentType` / `Size` |
| `LocationAttributeDefValue` | LocationAttributeDefValues | `Value`（名称）+ `Latitude` + `Longitude` |
| `LocationAttributeValue` | LocationAttributeValues | `LocationId` + `Latitude` + `Longitude` |
| `TableAttributeDefValue` | TableAttributeDefValues | `Value`（表名）+ `IsRecord` |
| `TableRowAttributeValue` | TableRowAttributeValues | `TableId` / `RowNo` / `CreatedAt` |

### 2.3 单位模型

| 模型 | 说明 |
|---|---|
| `UnitTree` | 单位树（分类 + 具体单位，如"长度 → 米 / 厘米 / 毫米"） |
| `UnitOfMeasure` | 计量单位（Id / Name / Symbol / Description） |

### 2.4 数据库上下文

`DiberyDbContext` 管理以下 `DbSet`：

- `CategoryTrees` / `UnitTrees` / `UnitsOfMeasure`
- `AttributeDefinitions`
- `TextAttributeValues` / `DecimalAttributeValues` / `IntegerAttributeValues`
- `DateAttributeValues` / `TimeAttributeValues` / `DateTimeAttributeValues`
- `FileAttributeValues`
- `LocationAttributeDefValues` / `LocationAttributeValues`
- `TableAttributeDefValues` / `TableRowAttributeValues`

配置要点：
- `AttributeValueBase` 使用 **TPC** 映射策略
- 所有 `DateTimeOffset` 统一转 UTC
- `AttributeDefinition` 自引用已移除数据库外键（业务层校验）
- 大量种子数据（`HasData`）

---

## 三、已实现功能

### 3.1 通用树服务（`ITreeService<T>`）

对 `CategoryTree` 和 `UnitTree` 分别实现，提供统一接口：

| 方法 | 功能 |
|---|---|
| `GetRootNodesAsync` | 获取根节点（可指定 rootId），并加载子/孙辈 |
| `GetChildrenAsync` | 懒加载指定父节点的子节点（含孙辈） |
| `QueryNodesAsync` | 按条件查询（ParentId / SearchTerm / OnlyWithChildren / 分页） |
| `GetFullTreeAsync` | 获取完整树（当前实现有缺陷） |
| `CreateNodeAsync` | 创建节点，自动更新父节点 `HasChildren` |
| `UpdateNodeAsync` | 更新节点名称与排序 |
| `UpdateChildrenAsync` | 批量更新子节点排序 |
| `DeleteNodeAsync` | 删除节点（当前不处理子节点） |
| `MoveNodeAsync` | 移动节点（含循环检测，通过递归 CTE） |
| `GetAncestorPathAsync` | 获取从根到目标节点的祖先路径 |

### 3.2 属性定义管理

| 端点 | 功能 |
|---|---|
| `POST /attributes/definitions` | 创建属性定义（表定义或列定义） |
| `GET /attributes/definitions/{id}` | 获取单个定义 |
| `GET /attributes/definitions` | 获取所有根定义 |
| `PUT /attributes/definitions/{id}` | 更新定义（类型不可改） |
| `DELETE /attributes/definitions/{id}` | 删除定义（被引用时拒绝） |
| `GET /attributes/tables` | 列出所有"表格"类型的表定义 |
| `GET /attributes/tables/{tableId}/columns` | 列出指定表的列定义（按 Order 排序） |
| `POST /attributes/tables/{tableId}/columns` | 在指定表下新建列 |

**校验逻辑**（`DefinitionValidator`）：
- 表定义（`ParentId == null`）类型必须为「表格」
- 列定义（`ParentId != null`）类型不可为「表格」

### 3.3 属性值管理

| 端点 | 功能 |
|---|---|
| `POST /attributes/{nodeId}/attributes/values` | 为节点添加属性值（幂等） |
| `GET /attributes/{nodeId}/attributes/values/{id}` | 获取单个属性值 |
| `GET /attributes/{nodeId}/attributes/values` | 获取节点所有属性值（聚合） |
| `PUT /attributes/{nodeId}/attributes/values/{id}` | 更新属性值 |
| `DELETE /attributes/{nodeId}/attributes/values/{id}` | 删除属性值 |

**值校验**（`ValueValidator.ValidateAndBuild`）按类型分派：
- 文本：字符串 + 长度校验
- 整数：`TryGetInt64`
- 小数：`TryGetDecimal`
- 日期 / 日期时间：`DateTimeOffset.TryParse`
- 时间：`TimeSpan.TryParse`
- 文件：字符串路径
- 定位：字符串 → `LocationAttributeDefValue`
- 表格：字符串 → `TableAttributeDefValue`

### 3.4 动态表管理

设计思路：**表结构 = 表定义 + 列定义；表数据 = 行实例 + 列值**。

| 端点 | 功能 |
|---|---|
| `POST /attributes/tables/{tableId}/rows` | 添加一行（幂等） |
| `GET /attributes/tables/{tableId}/rows` | 列出指定表的所有行 |
| `GET /attributes/tables/rows/{rowId}` | 获取单行（含各列值） |
| `PUT /attributes/tables/rows/{rowId}` | 更新行（先删旧列值再重建） |
| `DELETE /attributes/tables/rows/{rowId}` | 删除行及其列值 |

**存储结构**：
```
表定义 (AttributeDefinition, ParentId=null, Type=表格)
  ├── 列定义 (AttributeDefinition, ParentId=表定义Id)
  └── 行实例 (TableRowAttributeValue, AttributeDefinitionId=表定义Id)
        └── 列值 (Text/Decimal/…AttributeValue, NodeId=行实例Id, AttributeDefinitionId=列定义Id)
```

### 3.5 定位值管理

| 端点 | 功能 |
|---|---|
| `GET /attributes/locations/{locationId}` | 获取定位值 |
| `PUT /attributes/locations/{locationId}` | 更新定位值 |
| `POST /attributes/node/{nodeId}` | 为节点添加定位值（经纬度） |
| `GET /attributes/node/{nodeId}` | 列出节点的定位值 |
| `GET /attributes/{valueId}` | 获取单个定位值 |
| `PUT /attributes/{valueId}` | 更新定位值 |
| `DELETE /attributes/{valueId}` | 删除定位值 |

### 3.6 幂等去重

所有关键写操作（`AddValue`、`AddRow`、`AddAttributeLocationValue`）实现幂等：

1. 基于 `nodeId + 定义Id + 值` 计算 SHA256 指纹
2. 以指纹为 key 查询 `IMemoryCache`
3. 命中且已完成后 → 直接返回首次结果，`Duplicated = true`
4. 命中占位（首次仍在处理）→ 返回冲突或复用
5. 未命中 → 写占位 → 执行 → 写回真实结果

去重窗口：**30 秒**。

### 3.7 单位管理

`UnitsOfMeasureController` 继承 `UnitsOfMeasureControllerBase`，提供 `UnitOfMeasure` 的 CRUD（具体端点由基类定义，未在提供文件中展示）。

### 3.8 Aspire 集成

| 扩展 | 功能 |
|---|---|
| `PostgresExtension.AddPostgres` | 创建 Postgres 容器 + 6 个数据库（TreeDb、FileTransDb、MetadataDb、HangfireDb、FileMetadataDb、VectorAdminDb） |
| `DiberyTreeExtension.AddDiberyTreeService` | 注册 DiberyTreeService 项目，注入 `TreeDb` 引用，配置 OTLP 导出 |

### 3.9 种子数据

| 模型 | 种子内容 |
|---|---|
| `CategoryTree` | 11 个节点（"Sample Root" 及其 3 层子节点） |
| `UnitTree` | 根 "计量单位" + 15 个分类（货币/长度/质量/时间/温度/电流/电压/功率/面积/体积/速度/压力/能量/频率/角度）+ 若干具体单位 |
| `UnitOfMeasure` | 若干计量单位 |
| `AttributeDefinition` | 常用定义（名称/描述/备注/数量/价格/重量/长度/各类日期时间/附件/位置坐标）+ 表定义（规格表、生长日记、电杆类型、线路类型、线路） |
| `AttributeType` | 已注释，未启用 |

### 3.10 启动时数据库迁移

`Program.cs` 在 `app.Run()` 前：
1. 创建 `IServiceScope`
2. 获取 `DiberyDbContext`
3. 执行 `context.Database.MigrateAsync()`
4. 记录日志（成功/失败）

---

## 四、API 端点总览

### 4.1 树服务

| 方法 | 路由 | 说明 |
|---|---|---|
| GET | `/{controller}/roots` | 根节点列表 |
| GET | `/{controller}/roots/{rootId}` | 指定根节点 |
| GET | `/{controller}/children/{parentId}` | 子节点 |
| POST | `/{controller}/query` | 条件查询 |
| GET | `/{controller}/full` | 完整树 |
| POST | `/{controller}` | 创建节点 |
| POST | `/{controller}/children` | 更新子节点排序 |
| PUT | `/{controller}/{id}` | 更新节点 |
| DELETE | `/{controller}/{id}` | 删除节点 |
| POST | `/{controller}/move` | 移动节点 |
| GET | `/{controller}/{nodeId}/ancestors` | 祖先路径 |

应用于 `CategoryTree`、`UnitTree`。

### 4.2 属性服务（`/attributes`）

见 3.2 / 3.3 / 3.4 / 3.5。

### 4.3 单位服务

见 3.7。

---

## 五、数据流概览

### 5.1 属性值写入流程

```
客户端
  → POST /attributes/{nodeId}/attributes/values
  → AttributesControllerBase.AddValue
  → AttributeService.AddValueAsync
      ├─ 计算 SHA256 指纹
      ├─ 查 IMemoryCache（幂等）
      ├─ AddValueInternalAsync
      │   ├─ 查 AttributeDefinition
      │   ├─ ValueValidator.ValidateAndBuild
      │   └─ 按类型写入对应 DbSet
      └─ 写回缓存
  → 返回 ApiResponse<AttributeValueBase>
```

### 5.2 动态表写入流程

```
客户端
  → POST /attributes/tables/{tableId}/rows
  → AttributesControllerBase.AddRow
  → AttributeService.AddRowAsync
      ├─ 计算 SHA256 指纹（nodeId + tableId + 列值）
      ├─ 查 IMemoryCache
      ├─ AddRowInternalAsync
      │   ├─ 查表定义（校验类型为表格）
      │   ├─ 查列定义
      │   ├─ 校验列归属与类型
      │   ├─ 写 TableRowAttributeValue（行实例）
      │   └─ 逐列 ValueValidator + AddValueEntity
      └─ 写回缓存
  → 返回 ApiResponse<TableRowDto>
```

### 5.3 树查询流程

```
客户端
  → GET /CategoryTree/full?rootId=xxx
  → TreeControllerBase.GetFullTree
  → CategoryTreeService.GetFullTreeAsync
      ├─ 加载全部节点
      ├─ 确定根节点
      ├─ BuildDto（当前递归被注释，只返回根节点）
      └─ 返回
  → 返回 ApiResponse<TreeNodeDto<CategoryTree>>
```

---

## 六、项目现状小结

### 6.1 已具备的能力

- ✅ 通用树服务（分类树、单位树）
- ✅ 属性定义 CRUD（含动态表定义与列定义）
- ✅ 属性值 CRUD（10 种值类型）
- ✅ 动态表行/列 CRUD
- ✅ 定位值管理
- ✅ 幂等去重（SHA256 + IMemoryCache）
- ✅ 统一响应封装（`ApiResponse<T>`）
- ✅ TPC 继承映射
- ✅ Aspire 编排与依赖注入
- ✅ 启动时自动迁移
- ✅ 丰富种子数据

### 6.2 已声明但未完全落地的能力

- ⚠️ `GetFullTreeAsync` 完整树（递归被注释）
- ⚠️ `DefaultValue`（DTO 有，实体无）
- ⚠️ `复合` / `边` / `点` / `标签` / `边类型` / `属性` 类型（枚举有，映射缺）
- ⚠️ 定位值双路径（`LocationAttributeDefValue` / `LocationAttributeValue`）未统一
- ⚠️ 认证授权（仅 `UseAuthorization`，无 `AddAuthentication`）
- ⚠️ 健康检查端点
- ⚠️ 分布式幂等（仅进程内）

### 6.3 关键约束

- `AttributeDefinition` 自引用**无数据库外键**，业务层负责校验
- `DateTimeOffset` 统一以 UTC 存储
- 幂等去重窗口 **30 秒**，仅单实例有效
- 数据库连接通过 Aspire 注入，独立运行需自备 `ConnectionStrings:TreeDb`

---

## 七、总结

DiberyTree 是一个**面向可扩展数据建模的树形 + 属性 + 动态表服务**。它通过"定义与值分离"、"树与属性解耦"、"TPC 多态存储"等设计，提供了较强的数据建模灵活性。当前已实现树管理、属性定义、属性值、动态表、定位、单位、幂等去重、Aspire 编排等核心能力，并在种子数据中预置了分类树、单位树、常用属性定义与动态表定义。

项目整体功能框架已搭建完成，但部分能力仍处于半落地状态（完整树查询、`DefaultValue`、复合/边类型、定位模型统一、认证授权等），需在后续迭代中补齐。