// geolocation.js
// 浏览器 / WebView 定位封装（RCL 静态资产）。
// 宿主引用：<script src="_content/TreeGraph.Blazor.Shared/js/geolocation.js"></script>
//
// 返回 WGS-84 坐标（Geolocation API 标准），调用方负责按需转 GCJ-02。
// 成功：{ latitude, longitude }
// 失败：{ error: "..." }（不支持 / 拒绝授权 / 超时统一走 error 字段）
window.treegraphGeo = {
    getCurrentPosition: function () {
        return new Promise(function (resolve) {
            if (!("geolocation" in navigator)) {
                resolve({ error: "当前环境不支持定位（需 HTTPS 或 localhost）" });
                return;
            }
            navigator.geolocation.getCurrentPosition(
                function (pos) {
                    resolve({
                        latitude: pos.coords.latitude,
                        longitude: pos.coords.longitude
                    });
                },
                function (err) {
                    var msg;
                    switch (err.code) {
                        case 1: msg = "定位权限被拒绝，请在浏览器地址栏允许位置访问"; break;
                        case 2: msg = "位置信息不可用，请检查设备定位开关"; break;
                        case 3: msg = "定位超时，请稍后重试"; break;
                        default: msg = err.message || "定位失败";
                    }
                    resolve({ error: msg });
                },
                { enableHighAccuracy: true, timeout: 10000, maximumAge: 60000 }
            );
        });
    }
};
