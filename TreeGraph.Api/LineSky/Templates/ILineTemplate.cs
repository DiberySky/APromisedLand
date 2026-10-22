using TreeGraph.Shared.LineSky.Dtos;
using TreeGraph.Shared.LineSky.Entities;

namespace TreeGraph.Api.LineSky.Templates;

/// <summary>
/// 线路结构模板：预填拓扑模式、EdgeType 默认值、端子角色/状态默认值。
/// 模板是"建议层"——后端 ValidateCore 不感知模板，
/// 模板只在 UI 层预填默认值，真正的校验引擎仍是通用的 TopologyMode + TerminalRole + OperatingState。
/// 未来新增模板只要实现此接口 + 注册到 TemplateRegistry。
/// </summary>
public interface ILineTemplate
{
    string Key { get; }
    string DisplayName { get; }
    string Description { get; }
    TopologyMode DefaultTopologyMode { get; }
    /// <summary>推荐 EdgeType 的 TypeName（SeedData 里的名字）。null=让用户选。</summary>
    string? DefaultEdgeTypeName { get; }
    TerminalRole DefaultTerminalRole { get; }
    TerminalOperatingState DefaultOperatingState { get; }

    /// <summary>转 DTO 给 API 返回（枚举转 string 避免 MudBlazor source generator bug）。</summary>
    LineTemplateDto ToDto() => new(
        Key, DisplayName, Description,
        DefaultTopologyMode.ToString(), DefaultEdgeTypeName,
        DefaultTerminalRole.ToString(), DefaultOperatingState.ToString());
}
