using Microsoft.Agents.AI;

namespace MafSampleApi.Services;

/// <summary>创建 AIAgent 的工厂。每个会话一个实例。</summary>
public interface IAgentFactory
{
    /// <summary>
    /// 为指定模型创建 AIAgent。
    /// 传 null/空则使用 AgentOptions.ChatModel。
    /// </summary>
    AIAgent GetAgent(string? model = null);
}