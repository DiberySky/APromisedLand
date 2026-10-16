# TreeGraph.MafWfApi 实施任务清单

依据：specs/maf-wf-api/spec.md

## 任务

- [x] T1 建项目骨架：TreeGraph.MafWfApi.csproj + 加入 APromisedLand.sln；复制 launchSettings.json / appsettings.Development.json
- [x] T2 平移 Agents（11 文件 + 新建 AgentOptions.cs）：命名空间替换、keyed IChatClient→普通、Redis 前缀改 mawf:、MafAgentService 换 AgentOptions
- [x] T3 平移 Infrastructure（6 文件原样 + ServiceCollectionExtensions 重写）
- [x] T4 移植 Services（VllmChatClient/ThinkStrippingChatClient/VllmWarmupService/StructuredOutput，命名空间改 TreeGraph.MafWfApi）
- [x] T5 平移 Models（AgentDtos/GraphAgentDtos）+ 新建 HealthResponseDto
- [x] T6 平移 Controllers（GraphAgent/Workflows/Agents）+ 新建 ChatController、HealthController
- [x] T7 新写 Program.cs + appsettings.json
- [x] T8 AppHost：TreeGraphMafWfExtension.cs 新建、AppHostResourceContext 加属性、AppHost.cs 接线（AddVllm/AddLiteGraph/AddTreeGraphMafWfApi 入 TreeGraph()）、AppHost.csproj 加 ProjectReference
- [x] T9 编译验证：新项目 dotnet build 0 错误；AppHost --no-dependencies -o 临时目录 0 错误；清理临时目录
- [x] T10 汇报端口/端点/配置清单

## 验收

- [x] 两个 build 均 0 错误
- [x] 旧 MAFWorkFlowApi 文件零改动
- [x] AppHost 中无激活的 AddMafWorkFlowApi 调用
- [x] Redis 前缀为 mawf:session:
- [x] 无 OllamaSharp / CommunityToolkit.Aspire.OllamaSharp 引用
