using APromisedLand.Shared.Helper;
using APromisedLand.Shared.Models;

namespace APromisedLand.Shared.Services;

public partial class SolutionService
{
    public static PlatformOSEnum PlatformOS { get; set; } = PlatformOSEnum.Unknown;

    public static PlatformTypeEnum PlatformType { get; set; } = PlatformTypeEnum.未知;

    public static ScreenInfo ScreenInfo { get; set; } = new();

}
