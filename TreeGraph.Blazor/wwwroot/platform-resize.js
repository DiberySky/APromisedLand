// platform-resize.js — window.resize 监听（150ms 去抖）
//
// 【幂等性】MainLayout 可能因热重载/布局切换/错误页导航而重建，
// 每次重建都会重新调用 registerResize。重复注册会导致 listener 泄漏，
// 因此注册前先注销旧监听（unregisterResize）。
// 组件 Dispose 时应调用 unregisterResize 清理。

window.treeGraphPlatform = {
  registerResize: function (dotNetRef) {
    // 幂等保护：先清理旧 listener，避免 MainLayout 重建导致重复注册
    window.treeGraphPlatform.unregisterResize();

    window.__treeGraphResizeState = { dotNetRef: dotNetRef, timer: null };
    window.__treeGraphResizeHandler = function () {
      var s = window.__treeGraphResizeState;
      if (!s) { return; }
      clearTimeout(s.timer);
      s.timer = setTimeout(function () {
        s.dotNetRef.invokeMethodAsync('OnWindowResize');
      }, 150);
    };
    window.addEventListener('resize', window.__treeGraphResizeHandler);
  },

  unregisterResize: function () {
    if (window.__treeGraphResizeHandler) {
      window.removeEventListener('resize', window.__treeGraphResizeHandler);
      window.__treeGraphResizeHandler = null;
    }
    if (window.__treeGraphResizeState) {
      clearTimeout(window.__treeGraphResizeState.timer);
      window.__treeGraphResizeState = null;
    }
  }
};
