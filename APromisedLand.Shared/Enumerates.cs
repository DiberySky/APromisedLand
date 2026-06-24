using System;
using System.Collections.Generic;
using System.Text;

namespace APromisedLand.Shared;

public enum PlatformOS
{
    Android, iOS, Windows, macOS, Tizen, Unknown
}

// //判断设备类型（手机、平板、桌面、电视等）
// deviceType = DeviceInfo.Current.Idiom switch
// {
//     DeviceIdiom.Phone => "手机",
//     DeviceIdiom.Tablet => "平板",
//     DeviceIdiom.Desktop => "桌面",
//     DeviceIdiom.TV => "电视",
//     DeviceIdiom.Watch => "手表",
//     _ => "未知"
// };

public enum PlatformType
{
    手机, 平板, 桌面, 电视, 手表, 未知
}

public enum ScreenOrientation
{
    纵向, 横向, 未知
}

