using MafSampleApi.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace MafSampleApi.Services;

public sealed class AgentFactory : IAgentFactory
{
    private readonly IChatClient _chatClient;
    private readonly AgentOptions _options;

    public AgentFactory(IChatClient chatClient, IOptions<AgentOptions> options)
    {
        _chatClient = chatClient;
        _options = options.Value;
    }

    public AIAgent GetAgent(string? model = null)
    {
        var modelName = string.IsNullOrWhiteSpace(model) ? _options.ChatModel : model;

        // ★ 用 ChatClientAgent + ChatOptions.Instructions
        //   ChatOptions.Instructions 是 Microsoft.Extensions.AI 层
        //   标准的 system message 字段，OllamaApiClient 会正确识别。
        return new ChatClientAgent(
            _chatClient,
            new ChatClientAgentOptions
            {
                Name = $"assistant-{modelName}",
                ChatOptions = new ChatOptions
                {
                    Instructions = _options.SystemPrompt,
                },
            });
    }
}