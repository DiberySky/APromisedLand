// platform-back.js — Android 物理返回键拦截（MAUI Blazor Hybrid）
//
// 两条触发路径：
//   A. WebView 内后退（系统/手势在 WebView 内产生 popstate）→ popstate 监听
//   B. Android 原生返回键 → MainActivity 经 EvaluateJavascript 同步调用
//      window.__treegraph_handleBack()，按返回值决定是否拦截
//
// 历史模型（关键）：
//   整个对话框栈只维持一个哨兵历史项，随"栈是否为空"创建/消费，避免嵌套对话框多项错乱：
//   - register：栈从 0→1 时 pushState 哨兵；嵌套对话框复用同一个哨兵
//   - 用户后退触发 popstate（哨兵已被浏览器移除）：关闭栈顶；注销时若栈仍非空则重新补哨兵
//   - 代码路径关闭（取消按钮/原生返回键，哨兵仍在）：注销时 history.back() 消费哨兵，
//     并用 suppress 标记吞掉由此产生的 popstate
//
// 注意：每次 .NET→JS 调用，同一 DotNetObjectReference 的 JS 包装对象都是新实例，
// 必须按 __dotNetObject id 比较，不能用 indexOf 引用相等。
(function () {
    if (window.__treegraphBackInitialized) return;
    window.__treegraphBackInitialized = true;

    var MARKER = { __treegraphDialog: true };
    var stack = [];
    var suppressNextPopState = false;

    // 同一对话框 300ms 内的重复触发视为一次（.NET 侧 _closing 之外的第二道防线：
    // 防止栈顶项尚未出栈期间被原生层/popstate 连查两次）
    var lastBackInvoke = { ref: null, time: 0 };
    var BACK_DEBOUNCE_MS = 300;

    function refId(dotNetRef) {
        return dotNetRef ? dotNetRef.__dotNetObject : null;
    }

    function hasMarker() {
        return !!(history.state && history.state.__treegraphDialog);
    }

    function closeTopDialog() {
        if (stack.length === 0) return false;
        var top = stack[stack.length - 1];
        var id = refId(top);

        var now = Date.now();
        if (lastBackInvoke.ref === id && (now - lastBackInvoke.time) < BACK_DEBOUNCE_MS) {
            return true;    // 去抖：同一对话框 300ms 内重复触发视为一次
        }
        lastBackInvoke = { ref: id, time: now };

        Promise.resolve(top.invokeMethodAsync('HandleBackButton')).catch(function (err) {
            console.warn('[treegraph] back handler invoke failed:', err);
        });
        return true;
    }

    window.__treegraph_registerDialog = function (dotNetRef) {
        var id = refId(dotNetRef);
        // 同一引用重复注册先移除旧项，保持一对话框一项
        for (var i = stack.length - 1; i >= 0; i--) {
            if (refId(stack[i]) === id) stack.splice(i, 1);
        }
        stack.push(dotNetRef);

        // 哨兵仅随栈 0→1 创建
        if (stack.length === 1) {
            history.pushState(MARKER, '');
        }
    };

    window.__treegraph_unregisterDialog = function (dotNetRef) {
        var id = refId(dotNetRef);
        var idx = -1;
        for (var i = 0; i < stack.length; i++) {
            if (refId(stack[i]) === id) { idx = i; break; }
        }
        if (idx < 0) return;
        stack.splice(idx, 1);

        if (stack.length > 0) {
            // 仍有下层对话框：哨兵若已被用户后退移除（popstate 路径），补回
            if (!hasMarker()) {
                history.pushState(MARKER, '');
            }
        } else if (hasMarker()) {
            // 最后一个对话框经代码路径关闭（哨兵仍在）：消费哨兵并吞掉本次程序 popstate
            suppressNextPopState = true;
            history.back();
        }
    };

    // 原生层（MainActivity.OnBackPressedDispatcher）同步查询并拦截：
    // 返回 true=已拦截（关闭栈顶对话框）；false=放行系统默认（后退/退到桌面）
    window.__treegraph_handleBack = closeTopDialog;

    window.addEventListener('popstate', function () {
        if (suppressNextPopState) {
            // 注销哨兵引起的程序后退：吞掉，不触发任何关闭
            suppressNextPopState = false;
            return;
        }

        // 哨兵已被浏览器移除；关闭栈顶，unregister 会按剩余栈决定是否补哨兵
        closeTopDialog();
    });
})();
