using TreeGraph.Shared.LineSky.Entities;

namespace TreeGraph.Api.LineSky.Templates;

/// <summary>
/// 铁路牵引供电模板：多电源、允许成环、多所联络。
/// 默认 Network + TransmissionLine（输电线路：IsLine=true, IsLinear=false，允许 T 接分支的种子 EdgeType）。
/// 端子默认 FeedIn + Maintenance。
/// </summary>
public class RailwayFeedingTemplate : ILineTemplate
{
    public string Key => "RailwayFeeding";
    public string DisplayName => "铁路牵引供电";
    public string Description => "多电源接入、允许成环、分区所/AT所联络；自动选 Network 贯通线类型。";
    public TopologyMode DefaultTopologyMode => TopologyMode.Network;
    public string? DefaultEdgeTypeName => "TransmissionLine";
    public TerminalRole DefaultTerminalRole => TerminalRole.FeedIn;
    public TerminalOperatingState DefaultOperatingState => TerminalOperatingState.Maintenance;
}
