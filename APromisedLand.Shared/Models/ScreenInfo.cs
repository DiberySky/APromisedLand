using APromisedLand.Shared.Helper;
using System;
using System.Collections.Generic;
using System.Text;

namespace APromisedLand.Shared.Models;

public class ScreenInfo
{
    public double Width { get; set; } //逻辑宽度
    public double Height { get; set; } //逻辑高度
    public double Density { get; set; } //屏幕密度
    public ScreenOrientationEmnu Orientation { get; set; } = ScreenOrientationEmnu.未知; //方向
    public double RefreshRate { get; set; } //刷新率
}
