// EAV file 属性下载互操作：通过 DotNetStreamReference 将流下载为文件。
// 宿主 App 需在 index.html / _Host.cshtml 引用：
//   <script src="_content/TreeGraph.Blazor.Shared/js/fileDownload.js"></script>
window.treegraphFile = window.treegraphFile || {};

// 用 Blob + createObjectURL 触发浏览器下载，下载完成撤销 URL。
window.treegraphFile.downloadFromStream = async function (fileName, contentStreamReference) {
    const arrayBuffer = await contentStreamReference.arrayBuffer();
    const blob = new Blob([arrayBuffer]);
    const url = URL.createObjectURL(blob);

    const a = document.createElement('a');
    a.href = url;
    a.download = fileName ?? 'download';
    document.body.appendChild(a);
    a.click();
    a.remove();

    URL.revokeObjectURL(url);
};

// 用 byte[] 生成 Blob URL，返回字符串给 C# 侧用于 <img src> / <iframe src>。
// contentType 可选（空字符串时浏览器猜类型）。
window.treegraphFile.createBlobUrl = function (byteArray, contentType) {
    const blob = new Blob([byteArray], { type: contentType || '' });
    return URL.createObjectURL(blob);
};

// WASM 大文件下载：文件句柄只保存在 JS 内存，流式写入时不在 .NET 侧聚合整文件。
const pendingSaves = new Map();
let nextSaveId = 1;

window.treegraphFile.prepareSave = async function (fileName) {
    const id = nextSaveId++;
    const controller = new AbortController();
    try {
        const handle = window.showSaveFilePicker
            ? await window.showSaveFilePicker({ suggestedName: fileName || 'download' })
            : null;
        pendingSaves.set(id, { handle, controller, fileName: fileName || 'download' });
        return id;
    } catch (error) {
        if (error?.name === 'AbortError') return 0;
        throw error;
    }
};

window.treegraphFile.cancelSave = function (id) {
    const entry = pendingSaves.get(id);
    entry?.controller.abort();
};

window.treegraphFile.downloadToPreparedFile = async function (
    id, contentStreamReference, progressReference, maxBlobBytes) {
    const entry = pendingSaves.get(id);
    if (!entry) throw new Error('下载保存会话不存在或已结束。');

    try {
        if (!entry.handle) {
            // 无 File System Access API 时分块读入 Blob parts，避免 .NET arrayBuffer 全量复制；
            // 此路径仍占用浏览器内存，因此由宿主传入明确上限。
            const reader = contentStreamReference.stream().getReader();
            const parts = [];
            let downloaded = 0;
            let lastReport = 0;
            const report = async () => {
                if (progressReference) {
                    await progressReference.invokeMethodAsync('ReportDownloadProgress', downloaded);
                }
            };
            const abort = () => reader.cancel();
            entry.controller.signal.addEventListener('abort', abort, { once: true });
            try {
                while (true) {
                    if (entry.controller.signal.aborted)
                        throw new DOMException('下载已取消。', 'AbortError');

                    const { done, value } = await reader.read();
                    if (done) break;

                    downloaded += value.byteLength;
                    if (downloaded > maxBlobBytes) {
                        await reader.cancel();
                        throw new Error(`文件超过 ${Math.floor(maxBlobBytes / 1048576)} MiB 的兼容下载上限，请使用支持 File System Access API 的浏览器。`);
                    }

                    parts.push(value);
                    const now = Date.now();
                    if (now - lastReport >= 100) {
                        lastReport = now;
                        await report();
                    }
                }

                if (entry.controller.signal.aborted)
                    throw new DOMException('下载已取消。', 'AbortError');
                await report();

                const blob = new Blob(parts);
                const url = URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = url;
                a.download = entry.fileName;
                document.body.appendChild(a);
                a.click();
                a.remove();
                setTimeout(() => URL.revokeObjectURL(url), 0);
            } finally {
                entry.controller.signal.removeEventListener('abort', abort);
                reader.releaseLock();
            }
            return;
        }

        let downloaded = 0;
        let lastReport = 0;
        const report = async () => {
            if (progressReference) {
                await progressReference.invokeMethodAsync('ReportDownloadProgress', downloaded);
            }
        };
        const progressStream = new TransformStream({
            transform(chunk, controller) {
                downloaded += chunk.byteLength;
                const now = Date.now();
                if (now - lastReport >= 100) {
                    lastReport = now;
                    report();
                }
                controller.enqueue(chunk);
            },
            flush: report
        });
        const writable = await entry.handle.createWritable();
        try {
            await contentStreamReference.stream()
                .pipeThrough(progressStream)
                .pipeTo(writable, { signal: entry.controller.signal });
        } catch (error) {
            try { await writable.abort(); } catch { }
            throw error;
        }
    } finally {
        pendingSaves.delete(id);
    }
};
