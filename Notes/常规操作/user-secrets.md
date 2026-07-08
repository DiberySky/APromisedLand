`dotnet user-secrets` 是 .NET 开发中的 **“本地保险箱”**，专门用来在开发阶段保存**连接字符串、API 密钥**等敏感信息。它的核心逻辑是：**敏感数据绝不写进 `appsettings.json`，更不提交到 Git**。

以下是 `dotnet user-secrets` 常规操作速查手册：

---

### 1. 初始化（开启保险箱）
在使用之前，需要先给当前项目“上锁”（生成一个独一无二的 ID）。

```bash
dotnet user-secrets init
```
> 执行后，你的 `.csproj` 文件里会多出一个 `<UserSecretsId>` 标签。**一个项目只需执行一次**。

---

### 2. 设置机密（存入数据）
**核心语法**：`dotnet user-secrets set "键" "值"`

- **支持冒号（`:`）分层**（对应 `IConfiguration` 的层级结构）：
  ```bash
  dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost;Database=MyDb;"
  dotnet user-secrets set "ApiKeys:OpenAI" "sk-1234567890"
  ```

- **支持 JSON 对象直接赋值**（存复杂配置）：
  ```bash
  dotnet user-secrets set "SmtpConfig" '{"Host":"smtp.qq.com","Port":465}'
  ```

---

### 3. 查看机密（清点库存）
- **列出所有机密**（显示键值对）：
  ```bash
  dotnet user-secrets list
  ```
  输出示例：
  ```text
  ConnectionStrings:Default = Server=localhost...
  ApiKeys:OpenAI = sk-1234567890
  ```

---

### 4. 移除机密（销毁特定数据）
- **删除单个键**（及其值）：
  ```bash
  dotnet user-secrets remove "ConnectionStrings:Default"
  ```

---

### 5. 清空所有（一键格式化）
- **删除当前项目所有的机密**：
  ```bash
  dotnet user-secrets clear
  ```
  > ⚠️ 这是物理删除，不可恢复，执行前最好 `list` 确认一下。

---

### 6. 在代码中读取（打通保险箱）
无论你把值存在 `secrets.json` 还是 `appsettings.json`，读取方式完全一样。

```csharp
// 在 Program.cs 或 Controller 中
var builder = WebApplication.CreateBuilder(args);

// 默认就会加载 user-secrets（Development 环境自动开启）
var connectionString = builder.Configuration.GetConnectionString("Default");
var openAiKey = builder.Configuration["ApiKeys:OpenAI"];
```
> **关键点**：`user-secrets` 只在 `Development` 环境下自动生效。发布到生产环境时，这些值会被环境变量或真正的配置覆盖。

---

### 7. 手动编辑（进阶：直接改 JSON 文件）
有时在命令行输入带特殊字符的值容易出错，可以直接打开 JSON 文件手动编辑。

- **Windows 路径**：`%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`
- **Linux/macOS 路径**：`~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`

你可以直接用 VS Code 打开这个文件，手写 JSON 结构：
```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost;"
  },
  "ApiKeys": {
    "OpenAI": "sk-123"
  }
}
```

---

### 🔥 常规操作“潜规则”（避坑指南）

1. **自动注入机制**：当 `EnvironmentName` 为 `Development` 时，`CreateBuilder` 会自动加载 `user-secrets`，**不需要额外写代码**。
2. **Git 忽略**：`secrets.json` 存储在用户目录下（不在项目文件夹内），天然避开了 `.git` 追踪，**不需要手动添加到 `.gitignore`**。
3. **多项目共享**：如果有多个项目（如 Service + AppHost），建议在每个需要配置的独立项目下都执行 `init`，并分别设置各自需要的密钥。
4. **Docker/容器开发**：`user-secrets` 通常不适用于 Docker 容器内部，容器环境更推荐使用环境变量传入。
5. **迁移到生产**：部署时，记得将 `secrets` 中的键名映射到服务器的**环境变量**或**Azure Key Vault** 中，代码不需要改动。

---

### ✅ 一句话总结
> 日常只需记住 **`init`（初始化）→ `set`（存值）→ `list`（查岗）** 三板斧。它是本地开发的“隐私模式”，让你的连接字符串永远留在自己的电脑里，不丢进代码仓库。