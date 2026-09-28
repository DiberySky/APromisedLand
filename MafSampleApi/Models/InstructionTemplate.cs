namespace MafSampleApi.Models;

/// <summary>服务端预置的指令模板。可通过 API 增删改查。</summary>
public sealed record InstructionTemplate
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string Instruction { get; init; }

    /// <summary>"text"（默认）或 "json"。</summary>
    public string? OutputFormat { get; init; }

    /// <summary>可选 few-shot 示例。</summary>
    public IReadOnlyList<InstructExampleDto>? Examples { get; init; }
}

/// <summary>GET /api/vllm/chat/instruct/templates 的响应体。</summary>
public sealed record InstructionTemplateListDto
{
    public IReadOnlyList<InstructionTemplate> Items { get; init; }
        = Array.Empty<InstructionTemplate>();
}