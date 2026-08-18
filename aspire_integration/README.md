# NebulaGraph FastAPI + .NET Aspire 集成指南

## 架构

```
┌─────────────────────────────────────────────────────────────┐
│                    .NET Aspire AppHost                       │
│  ┌─────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │ NebulaGraph │  │ NebulaGraphApi  │  │ DiberyTreeSvc   │ │
│  │ (Container) │  │ (Python/Uvicorn)│  │ (.NET Project)  │ │
│  │  MetaD      │  │  FastAPI        │  │                 │ │
│  │  StorageD   │  │  nebula-python  │  │  NEBULAGRAPHAPI │ │
│  │  GraphD     │  │  OTLP → Dashboard│ │  _HTTP env var  │ │
│  └──────┬──────┘  └────────┬────────┘  └─────────────────┘ │
│         │                  │                                │
│         └──────────────────┘                                │
│              .WithReference()                               │
└─────────────────────────────────────────────────────────────┘
```

## 集成方式

### 1. 使用 AddUvicornApp（推荐，Aspire 13+）

```csharp
var nebulaApi = builder.AddUvicornApp(
        name: "nebula-graph-api",
        projectDirectory: "../NebulaGraphApiService",
        appName: "app.main:app"
    )
    .WithUv()                          // 使用 uv 包管理
    .WithHttpEndpoint(8000, 8000)     // 端口映射
    .WithExternalHttpEndpoints()      // 暴露外部访问
    .WithHttpHealthCheck("/api/v1/health")
    .WaitFor(nebulaGraph);
```

### 2. 服务发现

Python 服务自动参与 Aspire 服务发现：

```csharp
// .NET 服务引用 Python API
builder.AddProject<Projects.MyService>("my-service")
    .WithReference(nebulaApi);  // 自动注入 NEBULAGRAPHAPI_HTTP
```

```python
# Python 服务引用其他 Aspire 资源
import os
# 自动注入：ConnectionStrings__postgres, REDIS_HOST 等
postgres_url = os.environ.get("ConnectionStrings__postgres")
```

### 3. OpenTelemetry 集成

Python FastAPI 的 Trace/Metrics/Logs 自动流向 Aspire Dashboard：

```python
# telemetry.py - 自动配置 OTLP 导出器
configure_telemetry(app, service_name="nebula-graph-api")
```

在 Aspire Dashboard 中可查看：
- HTTP 请求链路追踪
- nGQL 查询性能指标
- Python 应用日志
- 服务依赖关系图

## 启动流程

```bash
# 1. 确保 Python 项目目录存在
mkdir -p ../NebulaGraphApiService
# 复制 Python 代码到该目录

# 2. 安装 Aspire Python 集成
cd AppHost
dotnet add package Aspire.Hosting.Python

# 3. 运行
aspire run
# 或
dotnet run
```

## 环境变量（自动注入）

| 变量名 | 来源 | 说明 |
|--------|------|------|
| `NEBULA_HOSTS` | `.WithEnvironment()` | GraphD 地址 |
| `NEBULAGRAPHAPI_HTTP` | `.WithReference()` | 其他服务调用 Python API 的 URL |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Aspire 自动 | Dashboard OTLP 接收端点 |
| `OTEL_SERVICE_NAME` | Aspire 自动 | 服务名称标识 |
| `PORT` | `.WithHttpEndpoint()` | FastAPI 监听端口 |

## 调试

- **VS Code**: 安装 Aspire 扩展，直接在 Python 代码中打断点
- **Dashboard**: https://localhost:17080（自动打开）
- **Swagger**: http://localhost:8000/docs
