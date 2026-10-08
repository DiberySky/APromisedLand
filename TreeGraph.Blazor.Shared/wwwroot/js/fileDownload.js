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
