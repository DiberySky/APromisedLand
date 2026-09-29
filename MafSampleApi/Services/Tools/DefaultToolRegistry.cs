using System.Text.Json;
using Microsoft.Extensions.AI;

namespace MafSampleApi.Services.Tools;

public sealed class DefaultToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, ToolDescriptor> _byName;
    private static readonly JsonSerializerOptions SchemaJson = new() { WriteIndented = false };

    public DefaultToolRegistry(
        TimeTools time,
        MathTools math,
        TemplateTools templates,
        KnowledgeTools knowledge)
    {
        var all = new List<ToolDescriptor>();
        all.AddRange(time.GetTools());
        all.AddRange(math.GetTools());
        all.AddRange(templates.GetTools());
        all.AddRange(knowledge.GetTools());

        _byName = all.ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<ToolDescriptor> List()
        => _byName.Values
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public IReadOnlyList<AIFunction> ResolveBase()
        => _byName.Values
            .Where(d => d.IsBase)
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .Select(d => d.Function)
            .ToArray();

    public IReadOnlyList<AIFunction> ResolveDynamic(
        IReadOnlyList<string>? names, IReadOnlyList<string>? tags)
    {
        IEnumerable<ToolDescriptor> result;

        if (names is { Count: > 0 })
        {
            result = names
                .Select(n => _byName.TryGetValue(n, out var d) ? d : null)
                .Where(d => d is { IsBase: false })
                .Select(d => d!);
        }
        else if (tags is { Count: > 0 })
        {
            result = _byName.Values.Where(d =>
                !d.IsBase &&
                d.Tags.Intersect(tags, StringComparer.OrdinalIgnoreCase).Any());
        }
        else
        {
            result = _byName.Values.Where(d => !d.IsBase && d.SafeByDefault);
        }

        return result
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .Select(d => d.Function)
            .ToArray();
    }

    internal static string SerializeSchema(AIFunction fn)
        => JsonSerializer.Serialize(fn.JsonSchema, SchemaJson);
}