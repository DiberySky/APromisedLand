namespace TreeGraph.Api.LineSky.Templates;

/// <summary>
/// 线路模板注册表：启动时从 DI 容器收集所有 ILineTemplate 实现。
/// 新增模板只要注册到 DI，就自动出现在下拉列表里。
/// </summary>
public class LineTemplateRegistry
{
    private readonly IReadOnlyDictionary<string, ILineTemplate> _byKey;
    private readonly IReadOnlyList<ILineTemplate> _all;

    public LineTemplateRegistry(IEnumerable<ILineTemplate> templates)
    {
        _all = templates.OrderBy(t => t.DisplayName).ToList().AsReadOnly();
        _byKey = _all.ToDictionary(t => t.Key);
    }

    public IReadOnlyList<ILineTemplate> All => _all;

    public ILineTemplate? GetByKey(string key) =>
        string.IsNullOrEmpty(key) ? null : _byKey.GetValueOrDefault(key);
}
