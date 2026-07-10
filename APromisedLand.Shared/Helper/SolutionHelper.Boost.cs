namespace APromisedLand.Shared.Helper;

public static partial class SolutionHelper
{
    public const float Bm25Boost = 3f; // 文本部分权重
    public const float KnnBoost = 1f; // 向量部分权重

    public const float TitleBoost = 3f; // 标题部分权重
    public const float ContentBoost = 1f; // 内容部分权重
}