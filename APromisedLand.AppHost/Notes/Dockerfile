FROM elasticsearch:9.4.3
COPY ./analysis-ik /usr/share/elasticsearch/plugins/analysis-ik

#FROM elasticsearch:9.4.3
#RUN elasticsearch-plugin install --batch https://get.infini.cloud/elasticsearch/analysis-ik/9.4.3

# 第一阶段：仅用于安装插件
#FROM docker.elastic.co/elasticsearch/elasticsearch:9.4.2 AS builder
#COPY elasticsearch-analysis-ik-9.4.2.zip /tmp/elasticsearch-analysis-ik.zip
#RUN elasticsearch-plugin install --batch file:///tmp/elasticsearch-analysis-ik.zip
#
## 第二阶段：复制已安装的插件目录，但不复制 zip 包
#FROM docker.elastic.co/elasticsearch/elasticsearch:9.4.2
#COPY --from=builder /usr/share/elasticsearch/plugins/analysis-ik /usr/share/elasticsearch/plugins/analysis-ik

