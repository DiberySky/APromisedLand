using APromisedLand.Shared.Helper;
using APromisedLand.Shared.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace APromisedLand.Shared.Services;

public partial class ProjectService
{
    public static PlatformOSEnum PlatformOS { get; set; } = PlatformOSEnum.Unknown;

    public static PlatformTypeEnum PlatformType { get; set; } = PlatformTypeEnum.未知;

    public static ScreenInfo ScreenInfo { get; set; } = new();

}
