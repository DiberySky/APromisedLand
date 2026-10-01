namespace TreeGraph.Blazor.Components.FieldRenderers;

/// <summary>
/// 带单位的数值（DynamicForm 内部表单值）。
/// 对应后端 JSON 形状 { value, unitId }，提交时由 DynamicForm 序列化。
/// </summary>
public class NumericValueDto
{
    public decimal? Value { get; set; }
    public Guid? UnitId { get; set; }
}
