namespace TreeGraph.Blazor.Services;

/// <summary>
/// 结构化字段校验错误。
///
/// Path 语义：相对某个 AttributeSchemaDto 的路径。
///   - ""                 → 属性本身
///   - "brand"            → composite 内的 brand 子字段
///   - "brand.name"       → brand 的 name 子字段
///   - "tags[0]"          → 数组 tags 的第 0 个元素
///   - "tags[0].sub"      → 数组 tags 的第 0 个元素的 sub 子字段
///
/// 父级组件按前缀筛选后，剥离前缀传给子组件；子组件只看它负责的部分。
/// </summary>
public sealed record FieldValidationError(string Path, string Message);
