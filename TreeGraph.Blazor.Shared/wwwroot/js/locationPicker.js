// locationPicker.js
// 地图选点（RCL 静态资产）：Leaflet + 高德栅格瓦片。
// 宿主引用：<script src="_content/TreeGraph.Blazor.Shared/js/locationPicker.js"></script>
//
// 高德瓦片为 GCJ-02 坐标系，点击取点得到的经纬度可直接存入本系统（无需转换）。
// Leaflet 从 jsdelivr CDN 动态加载（需联网）。
window.treegraphMapPicker = (function () {
    var maps = {};
    var leafletPromise = null;

    function loadLeaflet() {
        if (leafletPromise) return leafletPromise;
        leafletPromise = new Promise(function (resolve, reject) {
            if (window.L) { resolve(); return; }
            var css = document.createElement("link");
            css.rel = "stylesheet";
            css.href = "https://cdn.jsdelivr.net/npm/leaflet@1.9.4/dist/leaflet.css";
            document.head.appendChild(css);
            var script = document.createElement("script");
            script.src = "https://cdn.jsdelivr.net/npm/leaflet@1.9.4/dist/leaflet.js";
            script.onload = function () { resolve(); };
            script.onerror = function () { reject(new Error("地图组件加载失败")); };
            document.head.appendChild(script);
        });
        return leafletPromise;
    }

    // 高德路网瓦片（GCJ-02）
    var TILE_URL = "https://webrd0{s}.is.autonavi.com/appmaptile?lang=zh_cn&size=1&scale=1&style=8&x={x}&y={y}&z={z}";

    // 初始化地图。hasCoord=false 时 lat/lng 忽略，默认定位全国视图。
    function init(elementId, lat, lng, hasCoord, dotNetRef) {
        return loadLeaflet().then(function () {
            var el = document.getElementById(elementId);
            if (!el) return;
            dispose(elementId);

            var map = L.map(el).setView(hasCoord ? [lat, lng] : [35.0, 105.0], hasCoord ? 15 : 4);
            L.tileLayer(TILE_URL, {
                subdomains: ["1", "2", "3", "4"],
                maxZoom: 18,
                attribution: "© 高德地图"
            }).addTo(map);

            var marker = hasCoord ? L.marker([lat, lng]).addTo(map) : null;

            map.on("click", function (e) {
                if (marker) marker.setLatLng(e.latlng);
                else marker = L.marker(e.latlng).addTo(map);
                dotNetRef.invokeMethodAsync("OnMapClicked", e.latlng.lat, e.latlng.lng);
            });

            maps[elementId] = map;

            // 对话框有过渡动画，容器最终尺寸晚于 init 确定；
            // 延迟重算尺寸，否则瓦片只渲染局部区域
            setTimeout(function () { map.invalidateSize(); }, 350);
        });
    }

    // 平移视野（不放置标记）：用于「初始定位到当前位置」
    function setView(elementId, lat, lng, zoom) {
        var map = maps[elementId];
        if (map) map.setView([lat, lng], zoom);
    }

    function dispose(elementId) {
        var map = maps[elementId];
        if (map) {
            try { map.remove(); } catch (e) { /* 忽略 */ }
            delete maps[elementId];
        }
    }

    return { init: init, dispose: dispose, setView: setView };
})();
