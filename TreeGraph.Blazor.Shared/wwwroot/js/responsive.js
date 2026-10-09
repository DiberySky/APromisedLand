// 响应式通用模块：视口监听（供 ViewportService 使用）
window.responsive = {
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
    }
};
