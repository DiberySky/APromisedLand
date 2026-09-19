// 触发浏览器下载文本文件
window.downloadTextFile = function (filename, content, mimeType) {
    try {
        const blob = new Blob([content], { type: mimeType || 'text/plain;charset=utf-8' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
        return true;
    } catch (e) {
        console.error('downloadTextFile failed:', e);
        return false;
    }
};