## 容器内安装 `plugins/analysis-ik`

#### IK 分词器官方仓库
https://release.infinilabs.com/analysis-ik/stable/elasticsearch-analysis-ik-9.4.3.zip

#### 第一步：退出容器
```bash
docker container ls
docker ps -a | grep Elasticsearch
```

#### 第二步：在宿主机解压 IK 插件 zip 包，获取 `config` 目录
你当前目录（`/e/APromisedLand`）下有 `elasticsearch-analysis-ik-9.4.2.zip`，解压它：
```bash
unzip elasticsearch-analysis-ik-9.4.2.zip -d analysis-ik
```
（如果没有 `unzip` 命令，可以用 Windows 自带解压功能，或者用 `7z` 等，最终得到 `ik_extract/config` 文件夹）

#### 第三步：将解压出的 `config` 目录复制到容器内
```bash
docker cp ./ik_extract/config Elasticsearch-jayueksq:/usr/share/elasticsearch/plugins/analysis-ik/
```

#### 第四步：检查容器内是否成功
```bash
docker exec -it Elasticsearch-jayueksq ls -la ./plugins/analysis-ik/config/
```
应该能看到 `IKAnalyzer.cfg.xml`、`main.dic`、`stopword.dic` 等文件。

#### 第五步：重启容器使配置生效
```bash
docker restart Elasticsearch-jayueksq
```

#### 第六步：再次测试分词
```bash
curl -X POST "localhost:9200/_analyze?pretty" -H 'Content-Type: application/json' -d @test.json
```
返回结果应与之前一致（或更丰富），表明配置完整。

---

### 📌 清理临时文件（可选）
```bash
rm -rf ik_extract
```
