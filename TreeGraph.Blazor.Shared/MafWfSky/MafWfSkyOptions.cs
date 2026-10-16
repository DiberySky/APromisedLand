namespace TreeGraph.Blazor.Shared.MafWfSky;

/// <summary>MafWfApi 客户端配置（路由前缀，对齐后端控制器 Route）。</summary>
public class MafWfSkyOptions
{
    /// <summary>无工具单轮对话路由前缀，默认 "api/chat"。</summary>
    public string ChatPath { get; set; } = "api/chat";

    /// <summary>Graph Agent 路由前缀，默认 "api/graph-agent"。</summary>
    public string GraphAgentPath { get; set; } = "api/graph-agent";

    /// <summary>通用助手路由前缀，默认 "api/Agents"。</summary>
    public string AgentsPath { get; set; } = "api/Agents";

    /// <summary>工作流路由前缀，默认 "api/Workflows"。</summary>
    public string WorkflowsPath { get; set; } = "api/Workflows";
}
