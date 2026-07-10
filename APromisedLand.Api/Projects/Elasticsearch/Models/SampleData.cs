using APromisedLand.Api.Projects.Nats.Models;

namespace APromisedLand.Api.Projects.Elasticsearch.Models;

public static class SampleData
{
    public static List<DocumentData> GetDocuments()
    {
        return
        [
            new DocumentData
            {
                Id = "1",
                Title = "人工智能简介",
                Content = "人工智能是计算机科学的一个分支，致力于创造能够执行通常需要人类智能的任务的系统。"
            },

            new DocumentData
            {
                Id = "2",
                Title = "机器学习基础",
                Content = "机器学习是人工智能的一个子领域，它使计算机能够从数据中学习并改进其性能。"
            },

            new DocumentData
            {
                Id = "3",
                Title = "深度学习与神经网络",
                Content = "深度学习是一种使用多层神经网络的机器学习技术，在图像识别和自然语言处理中表现出色。"
            },

            new DocumentData
            {
                Id = "4",
                Title = "语义搜索原理",
                Content = "语义搜索试图理解用户的意图和查询的上下文含义，而不仅仅是关键词匹配。"
            },

            new DocumentData
            {
                Id = "5",
                Title = "向量数据库应用",
                Content = "向量数据库用于存储和检索高维向量数据，广泛应用于推荐系统和相似性搜索。"
            }
        ];
    }
}