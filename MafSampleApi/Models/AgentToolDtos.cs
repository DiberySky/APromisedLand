namespace MafSampleApi.Models;

public sealed record ToolListDto
{
    public IReadOnlyList<ToolDescriptorDto> Items { get; init; }
        = Array.Empty<ToolDescriptorDto>();
}

public sealed record ToolDescriptorDto
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public string? ParametersSchema { get; init; }
    public bool SafeByDefault { get; init; } = true;
    public bool IsBase { get; init; }
}