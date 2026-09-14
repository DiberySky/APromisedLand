// 让聊天容器滚到底部，保证最新消息可见。
window.scrollToBottom = (element) => {
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
};