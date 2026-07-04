在 SeaweedFS 中，查看已上传文件列表，最直接的方式是在浏览器中打开：http://localhost:8888/

根据 TUS 协议，`OPTIONS` 请求用于探测服务端能力，其信息是通过**响应头（Response Headers）** 来传递的，响应体本身确实是空的。

所以，虽然响应体是空的，但关键信息都在响应头中。你可以通过以下命令来查看完整的响应头：

```bash
curl -X OPTIONS http://localhost:8888/.tus/ -H "Tus-Resumable: 1.0.0" -i
```

如果 SeaweedFS 的 TUS 功能正常启用，响应头中应包含类似以下内容：

```
Tus-Resumable: 1.0.0
Tus-Version: 1.0.0
Tus-Extension: creation,creation-with-upload,termination
Tus-Max-Size: 5368709120
```

太好了！从你贴出的响应头来看，**SeaweedFS 的 TUS 服务已完全正常启用**，一切配置正确。

现在你的环境已经完全就绪，可以开始进行断点续传了。以下是具体的操作步骤，你可以直接复制运行来上传一个测试文件。

### 第一步：创建上传会话
这个命令会向服务端申请一个上传 ID，并告知文件总大小和元数据。

```bash
# 创建一个 1MB 的测试文件 (用于演示)
dd if=/dev/urandom of=test.bin bs=1M count=1

# 发起创建会话请求
curl -X POST http://localhost:8888/.tus/test.bin \
  -H "Tus-Resumable: 1.0.0" \
  -H "Upload-Length: 1048576" \
  -H "Upload-Metadata: filename dGVzdC5iaW4=,content-type YXBwbGljYXRpb24vb2N0ZXQtc3RyZWFt" \
  -i
```
> **注意**：`Upload-Metadata` 中的 `filename dGVzdC5iaW4=` 是 `test.bin` 的 Base64 编码值。

**关键返回值**：服务端会返回 `201 Created`，并在 `Location` 头中返回上传 URL（例如 `/.tus/.uploads/xxxxxxxxxx`），请记下这个路径。

---

### 第二步：上传文件内容（支持断点续传）
使用上一步返回的 `Location` 地址（替换下面的 `{上传ID}`），将文件分块上传。

```bash
# 首次从偏移量 0 开始上传
curl -X PATCH http://localhost:8888/.tus/.uploads/{上传ID} \
  -H "Tus-Resumable: 1.0.0" \
  -H "Upload-Offset: 0" \
  -H "Content-Type: application/offset+octet-stream" \
  --data-binary @test.bin \
  -i
```
如果成功，服务端会返回 `204 No Content`。

---

### 第三步：模拟断点续传（查询进度并续传）
1.  **查询已上传的偏移量**（相当于检查断点）：
    ```bash
    curl -I http://localhost:8888/.tus/.uploads/{上传ID} -H "Tus-Resumable: 1.0.0"
    ```
    返回的 `Upload-Offset` 头会告诉你已经成功上传了多少字节。

2.  **续传剩余部分**（假设只传了一半，`Upload-Offset` 显示为 `524288`）：
    > 实际使用中，你可以用 `dd` 截取文件后半部分，或者使用支持 TUS 的 SDK 自动处理。

    ```bash
    # 续传时只需将 Upload-Offset 改为当前进度，并发送剩余的数据即可
    curl -X PATCH http://localhost:8888/.tus/.uploads/{上传ID} \
      -H "Tus-Resumable: 1.0.0" \
      -H "Upload-Offset: 524288" \
      -H "Content-Type: application/offset+octet-stream" \
      --data-binary @剩余部分的数据 \
      -i
    ```

---

### 补充说明
- **文件存储位置**：通过 TUS 上传到 `/path/to/file.txt`，文件最终会存储在 Filer 对应的目录下（例如 `/.tus/path/to/file.txt`）。完成后，你可以通过普通 HTTP GET 请求 `http://localhost:8888/path/to/file.txt` 来下载它。
- **大文件建议**：虽然手动 `curl` 演示了原理，但在生产环境上传大文件时，强烈建议使用官方的 TUS 客户端库（如 `tus-js-client`、`tus-android-client` 等），它们会自动处理重试、偏移量获取和分块逻辑。

现在 TUS 功能验证通过，可以放心使用了。如果后续需要调整单次上传的最大限制（目前响应头显示为 5GB），可以通过 Filer 启动参数 `-tusMaxFileSizeBytes` 来修改。