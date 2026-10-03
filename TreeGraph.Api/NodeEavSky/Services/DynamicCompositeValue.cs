namespace TreeGraph.Api.NodeEavSky.Services;

/// <summary>组合类型运行时值模型（字段名 -> 值）</summary>
public class DynamicCompositeValue
{
    private readonly Dictionary<string, object?> _fields = new();
    public string TypeName { get; }

    public DynamicCompositeValue(string typeName) => TypeName = typeName;

    public object? this[string fieldName]
    {
        get => _fields.TryGetValue(fieldName, out var v) ? v : null;
        set => _fields[fieldName] = value;
    }

    public IReadOnlyDictionary<string, object?> Fields => _fields;

    public bool TryGet<T>(string fieldName, out T? value)
    {
        if (_fields.TryGetValue(fieldName, out var raw) && raw is T t)
        {
            value = t;
            return true;
        }
        value = default;
        return false;
    }
}
