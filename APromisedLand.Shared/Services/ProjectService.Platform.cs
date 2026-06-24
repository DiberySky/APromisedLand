using APromisedLand.Shared.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace APromisedLand.Shared.Services;

public partial class ProjectService
{
    public static PlatformOS PlatformOS { get; set; } = PlatformOS.Unknown;

    public static PlatformType PlatformType { get; set; } = PlatformType.未知;

    public static ScreenInfo ScreenInfo { get; set; } = new();

}
