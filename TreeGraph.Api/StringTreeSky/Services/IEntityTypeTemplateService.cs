namespace TreeGraph.Api.StringTreeSky.Services;

/// <summary>
/// EntityType 属性模板复制服务。
///
/// 把一个 EntityType 的属性定义复制到另一个 EntityType。
/// 支持标量属性 + single_choice（选项集深拷贝）；
/// 跳过 composite / table（v1 简化）。
/// </summary>
public interface IEntityTypeTemplateService
{
    /// <summary>
    /// 复制属性定义。
    /// 返回 (成功复制数, 跳过数, 跳过原因列表)。
    /// </summary>
    Task<TemplateCopyResult> CopyAttributesAsync(
        string sourceEntityType,
        string targetEntityType,
        CancellationToken ct = default);
}

public sealed record TemplateCopyResult(
    int Copied,
    int Skipped,
    IReadOnlyList<string> SkipReasons);
