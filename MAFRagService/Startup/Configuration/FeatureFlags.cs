namespace MAFRagService.Startup.Configuration;

/// <summary>
/// 启动期特性开关。从 appsettings "Features" 节绑定。
/// 语义：true = 注册对应模块，false = 不注册。
/// ⚠️ 关闭被其他服务依赖的模块会导致运行时 DI 失败，请确认依赖闭环。
/// </summary>
public sealed class FeatureFlags
{
    public const string SectionName = "Features";

    public bool Rag      { get; set; } = true;
    public bool Graph    { get; set; } = true;
    public bool Entity   { get; set; } = true;
    public bool Agents   { get; set; } = true;
    public bool Indexing { get; set; } = true;
}