using APromisedLand.Shared.Helper;
using APromisedLand.Shared.Models;
using APromisedLand.Shared.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace APromisedLand.Maui.Services;

public class PlatformServer
{
    public static void PlatformInfo()
    {
        PlatformOSEnum deviceOS;
        // 判断操作系统
        if (DeviceInfo.Current.Platform == DevicePlatform.Android)
            deviceOS = PlatformOSEnum.Android; //"Android";
        else if (DeviceInfo.Current.Platform == DevicePlatform.iOS)
            deviceOS = PlatformOSEnum.iOS; // "iOS";
        else if (DeviceInfo.Current.Platform == DevicePlatform.WinUI)
            deviceOS = PlatformOSEnum.Windows; //"Windows";
        else if (DeviceInfo.Current.Platform == DevicePlatform.macOS)
            deviceOS = PlatformOSEnum.macOS; //"macOS";
        else if (DeviceInfo.Current.Platform == DevicePlatform.Tizen)
            deviceOS = PlatformOSEnum.Tizen; //"Tizen";
        else
            deviceOS = PlatformOSEnum.Unknown; //"其他";

        ProjectService.PlatformOS = deviceOS;

        var idiom = DeviceInfo.Current.Idiom;
        PlatformTypeEnum deviceType;
        if (idiom == DeviceIdiom.Phone)
            deviceType = PlatformTypeEnum.手表; //"手机";
        else if (idiom == DeviceIdiom.Tablet)
            deviceType = PlatformTypeEnum.平板; //"平板";
        else if (idiom == DeviceIdiom.Desktop)
            deviceType = PlatformTypeEnum.桌面; //"桌面";
        else if (idiom == DeviceIdiom.TV)
            deviceType = PlatformTypeEnum.电视; //"电视";
        else
            deviceType = PlatformTypeEnum.手表; //"未知";

        ProjectService.PlatformType = deviceType;
    }

    public static void DisplayInfo()
    { 
        var displayInfo = DeviceDisplay.Current.MainDisplayInfo;

        var screenInfo = new ScreenInfo
        {
            Width = displayInfo.Width,
            Height = displayInfo.Height,
            Density = displayInfo.Density,
            RefreshRate = displayInfo.RefreshRate,
        };

        screenInfo.Orientation = displayInfo.Orientation switch
        {
            DisplayOrientation.Portrait => ScreenOrientationEmnu.纵向,
            DisplayOrientation.Landscape => ScreenOrientationEmnu.横向,
            _ => ScreenOrientationEmnu.未知,
        };

        ProjectService.ScreenInfo = screenInfo;
    }

    public static WindowSize CurrentWindow()
    {
        var size = new WindowSize();

        var currentWindow = Application.Current?.Windows.FirstOrDefault();
        if (currentWindow != null)
        {
            // 初始值（如果有效）
            if (!double.IsNaN(currentWindow.Width))
                size.Width = currentWindow.Width;
            if (!double.IsNaN(currentWindow.Height))
                size.Height = currentWindow.Height;
        }

        return size;
    }

}
