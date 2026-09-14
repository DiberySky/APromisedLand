// 复制文本到剪贴板
window.copyToClipboard = async function (text) {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch (err) {
        console.error('复制失败: ', err);
        return false;
    }
};

// 触发浏览器下载文本文件
window.downloadTextFile = function (fileName, text) {
    try {
        const blob = new Blob([text], {type: 'text/plain;charset=utf-8'});
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    } catch (err) {
        console.error('下载失败: ', err);
    }
};

// 消息容器滚动到底部
window.scrollToBottom = function (element) {
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
};