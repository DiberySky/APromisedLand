既然你提到了 **Docker**，结合你之前一直在折腾 **.NET Aspire**（`aspire publish` 生成 `infra` 目录），我们直接切入正题：**Docker 在日常开发中 = 开箱即用的环境（数据库/中间件） + 标准化部署（容器化应用）**。

为你整理一份 **Docker 常规操作速查手册**，直接对接你的实际工作场景：

---

### 1. 镜像与容器（最核心的两大概念）

| 操作对象 | 命令 | 高频场景 |
| :--- | :--- | :--- |
| **镜像** | `docker images` | 查看本地有哪些镜像（占多少空间） |
| **拉取** | `docker pull postgres:16` | 下载一个镜像到本地（跑基础设施） |
| **构建** | `docker build -t myapp .` | 根据 Dockerfile 给项目打包成镜像 |
| **删除镜像** | `docker rmi <镜像ID>` | 清理无用镜像（加 `-f` 强制删） |
| **运行容器** | `docker run -d --name redis -p 6379:6379 redis` | **最常用**：后台运行一个 Redis |
| **查看运行中容器** | `docker ps` | 看哪些服务还活着（`-a` 看所有包括停止的） |
| **停止/启动** | `docker stop <容器名>` / `docker start <容器名>` | 重启某个中间件 |
| **删除容器** | `docker rm <容器名>` | 清理废弃容器（加 `-f` 强制删运行中的） |

---

### 2. 日常开发调试（救命三件套）

#### ① 查看日志（排查报错第一关）
```bash
docker logs -f <容器名>    # 实时滚动查看日志（Ctrl+C 退出）
docker logs --tail 100 <容器名>  # 只看最后 100 行
```

#### ② 进入容器内部（查看文件/配置/网络）
```bash
docker exec -it <容器名> /bin/bash   # 进 Linux 容器
docker exec -it <容器名> /bin/sh    # 如果 bash 没有，用 sh
```
进去后可以用 `ls`、`cat`、`env` 查看内部环境变量。

#### ③ 端口映射检查
运行容器时必带参数：
```bash
docker run -d -p 宿主机端口:容器端口 --name xxx 镜像
# 例如：-p 8080:80 代表访问宿主机 8080 即访问容器 80
```

---

### 3. Docker Compose（你的核心战场）

因为 Aspire 发布生成的 `infra` 目录下就是 `docker-compose.yaml`，你绝大多数时间在跟它打交道。

| 操作 | 命令 | 场景 |
| :--- | :--- | :--- |
| **启动所有服务** | `docker-compose up -d` | 在 `infra` 目录下执行，一键跑起 Postgres/Redis/Question-Service |
| **停止并删除容器** | `docker-compose down` | 下班或清理环境（加 `-v` 会删数据卷，慎用） |
| **查看状态** | `docker-compose ps` | 看 Compose 管理的容器是否都在运行 |
| **重启单个服务** | `docker-compose restart question-service` | 只重启 Question-Service，不影响数据库 |
| **查看组合日志** | `docker-compose logs -f` | 看所有服务的日志流 |
| **重新构建并启动** | `docker-compose up -d --build` | 修改了代码后，重新打包镜像并启动 |

> **注意**：执行 `docker-compose` 命令时，必须在其所在的目录（即包含 `docker-compose.yml` 的目录）下，或者用 `-f` 指定文件路径。

---

### 4. 清理磁盘空间（Docker 很能吃硬盘）

长期使用 Docker，缓存和悬空镜像会越积越多，需要定期保养：

```bash
docker system prune -a -f    # 一键清理：停止的容器、未使用的网络、悬空镜像、构建缓存
docker volume prune -f      # 删除未使用的数据卷（谨慎操作，会删持久化数据）
```

---

### 5. Dockerfile 常规写法（.NET 专用模版）

如果你要手写 `Dockerfile`（比如给 `Question-Service` 打包），复制以下通用模版（多阶段构建）：

```dockerfile
# 第一阶段：构建（编译）
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["QuestionService/QuestionService.csproj", "QuestionService/"]
RUN dotnet restore "QuestionService/QuestionService.csproj"
COPY . .
WORKDIR "/src/QuestionService"
RUN dotnet build -c Release -o /app/build

# 第二阶段：发布（生成运行文件）
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

# 第三阶段：运行时（最小镜像）
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "QuestionService.dll"]
```

---

### 💡 常规操作“潜规则”（避坑必看）

1. **数据持久化（卷挂载）**：跑数据库（Postgres/Redis）时，务必用 `-v 宿主机目录:容器目录` 挂载，否则 `docker rm` 会彻底丢失数据。
   ```bash
   docker run -d -v /my/data:/var/lib/postgresql/data postgres
   ```

2. **容器内部网络互通**：如果用 `docker-compose`，服务之间可以直接用**服务名**（`question-service`）作为 Hostname 通信，而不是 `localhost`。

3. **时区问题**：容器默认 UTC 时间，日志时间会差 8 小时。在 `docker run` 或 `docker-compose` 中添加：
   ```yaml
   environment:
     - TZ=Asia/Shanghai
   ```

4. **资源限制**：本地跑多个中间件卡顿时，限制 CPU/内存：
   ```bash
   docker run --memory="512m" --cpus="0.5" ...
   ```

---

### ✅ 一句话总结
> 日常高频流程就是：**`docker pull` 拉镜像 → `docker-compose up -d` 起服务 → `docker logs -f` 看日志查 bug → `docker-compose down` 收工**。记得定期 `docker system prune` 给 C 盘腾空间。