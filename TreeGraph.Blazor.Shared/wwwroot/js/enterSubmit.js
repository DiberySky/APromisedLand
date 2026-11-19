// Enter 发送、Shift+Enter 换行的多行输入框支持（NodeSemanticSearch 底栏输入框）
// 用法：enterSubmit.register('#nss-query', dotNetRef) —— dotNetRef 需暴露 OnEnterSubmit 方法
window.enterSubmit = {
    register: (selector, dotNetRef) => {
        window.enterSubmit.unregister(selector);
        const el = document.querySelector(selector);
        if (!el) return;

        const handler = (e) => {
            // 输入法组字中的 Enter 不拦截；Shift+Enter 保留换行；Ctrl/Alt+Enter 交给默认行为
            if (e.key !== 'Enter' || e.shiftKey || e.ctrlKey || e.altKey || e.isComposing) return;
            e.preventDefault();
            dotNetRef.invokeMethodAsync('OnEnterSubmit');
        };

        el.addEventListener('keydown', handler);
        window.enterSubmit._handlers ??= new Map();
        window.enterSubmit._handlers.set(selector, { el, handler });
    },

    unregister: (selector) => {
        const entry = window.enterSubmit._handlers?.get(selector);
        if (!entry) return;
        entry.el.removeEventListener('keydown', entry.handler);
        window.enterSubmit._handlers.delete(selector);
    }
};
