// 响应式通用模块：视口监听 + 拖拽 + 本地存储
window.responsive = {
    // ---------- 视口 ----------
    getWidth: () => window.innerWidth,

    registerResize: (dotNetRef) => {
        window.responsive._unregisterResize();
        const handler = () => {
            dotNetRef.invokeMethodAsync('OnResize', window.innerWidth);
        };
        window.responsive._resizeHandler = handler;
        window.addEventListener('resize', handler);
    },

    _unregisterResize: () => {
        if (window.responsive._resizeHandler) {
            window.removeEventListener('resize', window.responsive._resizeHandler);
            window.responsive._resizeHandler = null;
        }
    },

    // ---------- 拖拽分割条 ----------
    startDrag: (dotNetRef, startX, startWidth, min, max) => {
        const onMove = (e) => {
            const delta = e.clientX - startX;
            const w = Math.max(min, Math.min(max, startWidth + delta));
            dotNetRef.invokeMethodAsync('OnDragMove', w);
        };
        const onUp = () => {
            document.removeEventListener('pointermove', onMove);
            document.removeEventListener('pointerup', onUp);
            document.removeEventListener('pointercancel', onUp);
            document.body.style.userSelect = '';
            document.body.style.cursor = '';
            dotNetRef.invokeMethodAsync('OnDragEnd');
        };
        document.addEventListener('pointermove', onMove);
        document.addEventListener('pointerup', onUp);
        document.addEventListener('pointercancel', onUp);
        document.body.style.userSelect = 'none';
        document.body.style.cursor = 'col-resize';
    },

    // ---------- localStorage ----------
    getLocal: (key) => {
        try { return localStorage.getItem(key); } catch { return null; }
    },
    setLocal: (key, value) => {
        try { localStorage.setItem(key, value); } catch { /* 忽略 */ }
    }
};
