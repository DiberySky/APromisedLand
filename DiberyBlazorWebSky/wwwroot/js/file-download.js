// 通过 DotNetStreamReference 将流下载为文件。
// 用 Blob 分块拉取，避免一次性加载整个文件到 JS 内存。
window.downloadFileFromStream = async function (fileName, contentStreamReference) {
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