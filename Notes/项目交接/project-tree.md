+-- .github
|   \-- workflows
|       \-- ci.yml
+-- APromisedLand.Api
|   +-- Configuration
|   |   \-- ApiConfigs.cs
|   +-- Contracts
|   |   +-- AnswerAccepted.cs
|   |   +-- AnswerCountUpdated.cs
|   |   +-- QuestionCreated.cs
|   |   +-- QuestionDeleted.cs
|   |   +-- QuestionUpdated.cs
|   |   \-- UpdatedAnswerCount.cs
|   +-- Controllers
|   |   +-- TreeController.cs
|   |   \-- UnitsOfMeasureControllerBase.cs
|   +-- Data
|   |   +-- Migrations
|   |   |   +-- MafRag
|   |   |   +-- 20260906120800_Initial.cs
|   |   |   +-- 20260906120800_Initial.Designer.cs
|   |   |   \-- DiberyDbContextModelSnapshot.cs
|   |   +-- DiberyDbContext.cs
|   |   +-- DiberyDbContext.DynamicTable.cs
|   |   +-- MafRagContext.cs
|   |   \-- MafRagContextFactory.cs
|   +-- DiberyTree
|   |   +-- Controllers
|   |   |   +-- AttributeLocationValueControllerBase.cs
|   |   |   +-- AttributesControllerBase.cs
|   |   |   +-- AttributesControllerBase.Definition.cs
|   |   |   +-- AttributesControllerBase.Location.cs
|   |   |   +-- AttributesControllerBase.Table.cs
|   |   |   +-- AttributesControllerBase.Value.cs
|   |   |   +-- AttributeTableValueControllerBase.cs
|   |   |   +-- TableValuesControllerBase.cs
|   |   |   \-- TreeControllerBase.cs
|   |   +-- Interface
|   |   |   +-- ITreeAttributeService.cs
|   |   |   \-- ITreeService.cs
|   |   \-- Services
|   |       +-- AttributeDefinitionService.cs
|   |       +-- AttributeLocationValueService.cs
|   |       +-- AttributeService.cs
|   |       +-- AttributeService.Definition.cs
|   |       +-- AttributeService.Location.cs
|   |       +-- AttributeService.Table.cs
|   |       +-- AttributeService.Value.cs
|   |       +-- AttributeTableValueService.cs
|   |       +-- CategoryTreeService.cs
|   |       +-- TreeAttributeService.cs
|   |       \-- UnitTreeService.cs
|   +-- Helper
|   |   +-- AuthHelper.cs
|   |   +-- NatsOperation.cs
|   |   \-- ProjectHelper.cs
|   +-- Interfaces
|   |   \-- IUnitOfMeasureService.cs
|   +-- MafRag
|   |   \-- Entities
|   |       +-- DocumentAuditEntity.cs
|   |       +-- DocumentMetadataEntity.cs
|   |       +-- DomainEventEntity.cs
|   |       \-- IndexTaskEntity.cs
|   +-- MessageContracts
|   |   +-- ElasticQuestion.cs
|   |   +-- QuestionData.cs
|   |   +-- QuestionMessage.cs
|   |   \-- QuestionSearchResult.cs
|   +-- NebulaGraph
|   |   +-- ControllerBases
|   |   |   +-- ConnectionControllerBase.cs
|   |   |   +-- EdgesControllerBase.cs
|   |   |   +-- JobsControllerBase.cs
|   |   |   +-- QueryControllerBase.cs
|   |   |   +-- SchemaControllerBase.cs
|   |   |   +-- SpacesControllerBase.cs
|   |   |   +-- SystemControllerBase.cs
|   |   |   +-- UsersControllerBase.cs
|   |   |   \-- VerticesControllerBase.cs
|   |   +-- Dtos
|   |   |   +-- EdgeDtos.cs
|   |   |   +-- JobDtos.cs
|   |   |   +-- QueryDtos.cs
|   |   |   +-- RawStmtIn.cs
|   |   |   +-- SchemaDtos.cs
|   |   |   +-- SpaceDtos.cs
|   |   |   +-- UserDtos.cs
|   |   |   \-- VertexDtos.cs
|   |   +-- Models
|   |   |   +-- ApiResult.cs
|   |   |   +-- NebulaFastApiOptions.cs
|   |   |   \-- ProxyResult.cs
|   |   \-- Services
|   |       \-- NebulaApiService.cs
|   +-- Projects
|   |   +-- Elasticsearch
|   |   |   +-- Embedding
|   |   |   +-- Models
|   |   |   +-- Nats
|   |   |   +-- Services
|   |   |   \-- ElasticsearchController.cs
|   |   +-- Nats
|   |   |   +-- Models
|   |   |   \-- Services
|   |   +-- SeaweedFS
|   |   |   +-- Data
|   |   |   +-- Models
|   |   |   +-- Services
|   |   |   \-- SeaweedFsController.cs
|   |   \-- Typesense
|   |       +-- Models
|   |       +-- Nats
|   |       \-- Services
|   +-- Services
|   |   \-- UnitOfMeasureService.cs
|   \-- APromisedLand.Api.csproj
+-- APromisedLand.AppHost
|   +-- aspire-output
|   |   +-- .env
|   |   \-- docker-compose.yaml
|   +-- Configs
|   |   \-- seaweedfs-s3.json
|   +-- data
|   |   +-- meta0
|   |   |   \-- nebula
|   |   \-- storage0
|   |       \-- nebula
|   +-- Extensions
|   |   +-- BlazorWebExtension.cs
|   |   +-- DevTunnelExtension.cs
|   |   +-- DiberyMauiExtension.cs
|   |   +-- DiberyTreeExtension.cs
|   |   +-- ElasticKibanaExtension.cs
|   |   +-- ElasticsearchExtension.cs
|   |   +-- ElasticvueExtension.cs
|   |   +-- ExternalServiceBinding.cs
|   |   +-- FileStorageApiExtension.cs
|   |   +-- FoundryExtension.cs
|   |   +-- KeycloakExtension.cs
|   |   +-- LiteGraphExtension.cs
|   |   +-- MafAIExtension.cs
|   |   +-- MafRagApiExtension.cs
|   |   +-- MafRagExtension.cs
|   |   +-- MafRagTreeGraphExtension.cs
|   |   +-- MafStatefulExtension.cs
|   |   +-- MafVectorSearchApiExtension.cs
|   |   +-- MafWorkFlowExtension.cs
|   |   +-- MauiExtension.cs
|   |   +-- NatsExtension.cs
|   |   +-- NebulaApiProxyExtension.cs
|   |   +-- NebulaGraphExtension.cs
|   |   +-- NebulaGraphFastApiExtension.cs
|   |   +-- NebulaGraphFastApiServiceExtension.cs
|   |   +-- NebulaGraphGatewayExtension.cs
|   |   +-- NebulaStudioExtension.cs
|   |   +-- NornicDbExtension.cs
|   |   +-- OllamaExtension.cs
|   |   +-- PostgresExtension.cs
|   |   +-- QuestionElasticExtension.cs
|   |   +-- QuestionExtension.cs
|   |   +-- QuestionTypesenseExtension.cs
|   |   +-- RabbitMqExtension.cs
|   |   +-- RedisExtension.cs
|   |   +-- RerankerExtension.cs
|   |   +-- ResourceBuilderExtensions.cs
|   |   +-- SeaweedFsExtension.cs
|   |   +-- SemanticSearchExtension.cs
|   |   +-- TreeGraphApiExtension.cs
|   |   +-- TreeGraphBlazorExtension.cs
|   |   +-- TreeGraphEavApiExtension.cs
|   |   +-- TypesenseExtension.cs
|   |   +-- VllmExtension.cs
|   |   +-- WeatherExtension.cs
|   |   +-- WeaviateExtension.cs
|   |   \-- YarpExtension.cs
|   +-- hf-cache
|   +-- logs
|   |   +-- graph
|   |   |   +-- graphd-stderr.log
|   |   |   +-- graphd-stdout.log
|   |   |   +-- nebula-graphd.02dbf746853c.root.log.ERROR.20260818-222632.1
|   |   |   +-- nebula-graphd.02dbf746853c.root.log.INFO.20260818-222629.1
|   |   |   +-- nebula-graphd.02dbf746853c.root.log.WARNING.20260818-222632.1
|   |   |   +-- nebula-graphd.12a9dc241664.root.log.ERROR.20260818-233449.1
|   |   |   +-- nebula-graphd.12a9dc241664.root.log.INFO.20260818-233446.1
|   |   |   +-- nebula-graphd.12a9dc241664.root.log.WARNING.20260818-233449.1
|   |   |   +-- nebula-graphd.131bc4c54a99.root.log.ERROR.20260819-233319.1
|   |   |   +-- nebula-graphd.131bc4c54a99.root.log.INFO.20260819-233312.1
|   |   |   +-- nebula-graphd.131bc4c54a99.root.log.WARNING.20260819-233319.1
|   |   |   +-- nebula-graphd.13a0445e2929.root.log.ERROR.20260819-233450.1
|   |   |   +-- nebula-graphd.13a0445e2929.root.log.INFO.20260819-233443.1
|   |   |   +-- nebula-graphd.13a0445e2929.root.log.WARNING.20260819-233450.1
|   |   |   +-- nebula-graphd.13c585173387.root.log.ERROR.20260819-002319.1
|   |   |   +-- nebula-graphd.13c585173387.root.log.INFO.20260819-002316.1
|   |   |   +-- nebula-graphd.13c585173387.root.log.WARNING.20260819-002319.1
|   |   |   +-- nebula-graphd.1941ad9fddc6.root.log.ERROR.20260817-161858.1
|   |   |   +-- nebula-graphd.1941ad9fddc6.root.log.INFO.20260817-161856.1
|   |   |   +-- nebula-graphd.1941ad9fddc6.root.log.WARNING.20260817-161858.1
|   |   |   +-- nebula-graphd.1b53b90a03a8.root.log.ERROR.20260819-235315.1
|   |   |   +-- nebula-graphd.1b53b90a03a8.root.log.INFO.20260819-235308.1
|   |   |   +-- nebula-graphd.1b53b90a03a8.root.log.WARNING.20260819-235315.1
|   |   |   +-- nebula-graphd.1b5e44340f93.root.log.ERROR.20260819-004714.1
|   |   |   +-- nebula-graphd.1b5e44340f93.root.log.INFO.20260819-004710.1
|   |   |   +-- nebula-graphd.1b5e44340f93.root.log.WARNING.20260819-004714.1
|   |   |   +-- nebula-graphd.1e505ecc89b7.root.log.ERROR.20260817-150924.1
|   |   |   +-- nebula-graphd.1e505ecc89b7.root.log.INFO.20260817-150924.1
|   |   |   +-- nebula-graphd.1e505ecc89b7.root.log.WARNING.20260817-150924.1
|   |   |   +-- nebula-graphd.1f877924c3a2.root.log.ERROR.20260817-164552.1
|   |   |   +-- nebula-graphd.1f877924c3a2.root.log.INFO.20260817-164552.1
|   |   |   +-- nebula-graphd.1f877924c3a2.root.log.WARNING.20260817-164552.1
|   |   |   +-- nebula-graphd.22221b1ef3cc.root.log.ERROR.20260819-221335.1
|   |   |   +-- nebula-graphd.22221b1ef3cc.root.log.INFO.20260819-221331.1
|   |   |   +-- nebula-graphd.22221b1ef3cc.root.log.WARNING.20260819-221335.1
|   |   |   +-- nebula-graphd.33fb49a6dcc5.root.log.ERROR.20260818-065010.1
|   |   |   +-- nebula-graphd.33fb49a6dcc5.root.log.INFO.20260818-065009.1
|   |   |   +-- nebula-graphd.33fb49a6dcc5.root.log.WARNING.20260818-065010.1
|   |   |   +-- nebula-graphd.37108ed5e08d.root.log.ERROR.20260818-051942.1
|   |   |   +-- nebula-graphd.37108ed5e08d.root.log.INFO.20260818-051939.1
|   |   |   +-- nebula-graphd.37108ed5e08d.root.log.WARNING.20260818-051942.1
|   |   |   +-- nebula-graphd.39f85cf1a79c.root.log.ERROR.20260819-234827.1
|   |   |   +-- nebula-graphd.39f85cf1a79c.root.log.INFO.20260819-234823.1
|   |   |   +-- nebula-graphd.39f85cf1a79c.root.log.WARNING.20260819-234827.1
|   |   |   +-- nebula-graphd.3c593a237027.root.log.ERROR.20260818-232918.1
|   |   |   +-- nebula-graphd.3c593a237027.root.log.INFO.20260818-232915.1
|   |   |   +-- nebula-graphd.3c593a237027.root.log.WARNING.20260818-232918.1
|   |   |   +-- nebula-graphd.3c7edc980e39.root.log.ERROR.20260818-111839.1
|   |   |   +-- nebula-graphd.3c7edc980e39.root.log.INFO.20260818-111836.1
|   |   |   +-- nebula-graphd.3c7edc980e39.root.log.WARNING.20260818-111839.1
|   |   |   +-- nebula-graphd.3df405508e60.root.log.ERROR.20260819-233848.1
|   |   |   +-- nebula-graphd.3df405508e60.root.log.INFO.20260819-233841.1
|   |   |   +-- nebula-graphd.3df405508e60.root.log.WARNING.20260819-233848.1
|   |   |   +-- nebula-graphd.3fe06fea16bd.root.log.ERROR.20260819-023411.1
|   |   |   +-- nebula-graphd.3fe06fea16bd.root.log.INFO.20260819-023408.1
|   |   |   +-- nebula-graphd.3fe06fea16bd.root.log.WARNING.20260819-023411.1
|   |   |   +-- nebula-graphd.406ba14168a3.root.log.ERROR.20260817-161533.1
|   |   |   +-- nebula-graphd.406ba14168a3.root.log.INFO.20260817-161533.1
|   |   |   +-- nebula-graphd.406ba14168a3.root.log.WARNING.20260817-161533.1
|   |   |   +-- nebula-graphd.41296c012646.root.log.ERROR.20260817-145628.1
|   |   |   +-- nebula-graphd.41296c012646.root.log.INFO.20260817-145628.1
|   |   |   +-- nebula-graphd.41296c012646.root.log.WARNING.20260817-145628.1
|   |   |   +-- nebula-graphd.43dc24118460.root.log.ERROR.20260817-143408.1
|   |   |   +-- nebula-graphd.43dc24118460.root.log.INFO.20260817-143407.1
|   |   |   +-- nebula-graphd.43dc24118460.root.log.WARNING.20260817-143408.1
|   |   |   +-- nebula-graphd.447980b83696.root.log.ERROR.20260817-164916.1
|   |   |   +-- nebula-graphd.447980b83696.root.log.INFO.20260817-164916.1
|   |   |   +-- nebula-graphd.447980b83696.root.log.WARNING.20260817-164916.1
|   |   |   +-- nebula-graphd.490136e0d1ff.root.log.ERROR.20260819-234215.1
|   |   |   +-- nebula-graphd.490136e0d1ff.root.log.INFO.20260819-234212.1
|   |   |   +-- nebula-graphd.490136e0d1ff.root.log.WARNING.20260819-234215.1
|   |   |   +-- nebula-graphd.4d871fb24c3b.root.log.ERROR.20260817-155016.1
|   |   |   +-- nebula-graphd.4d871fb24c3b.root.log.INFO.20260817-155016.1
|   |   |   +-- nebula-graphd.4d871fb24c3b.root.log.WARNING.20260817-155016.1
|   |   |   +-- nebula-graphd.5859c8f65f64.root.log.ERROR.20260818-101543.1
|   |   |   +-- nebula-graphd.5859c8f65f64.root.log.INFO.20260818-101539.1
|   |   |   +-- nebula-graphd.5859c8f65f64.root.log.WARNING.20260818-101543.1
|   |   |   +-- nebula-graphd.590f47ae3863.root.log.ERROR.20260818-102518.1
|   |   |   +-- nebula-graphd.590f47ae3863.root.log.INFO.20260818-102515.1
|   |   |   +-- nebula-graphd.590f47ae3863.root.log.WARNING.20260818-102518.1
|   |   |   +-- nebula-graphd.60c1c14e2bc8.root.log.ERROR.20260818-233738.1
|   |   |   +-- nebula-graphd.60c1c14e2bc8.root.log.INFO.20260818-233735.1
|   |   |   +-- nebula-graphd.60c1c14e2bc8.root.log.WARNING.20260818-233738.1
|   |   |   +-- nebula-graphd.7153aef388d3.root.log.ERROR.20260819-022836.1
|   |   |   +-- nebula-graphd.7153aef388d3.root.log.INFO.20260819-022833.1
|   |   |   +-- nebula-graphd.7153aef388d3.root.log.WARNING.20260819-022836.1
|   |   |   +-- nebula-graphd.77a31b265149.root.log.ERROR.20260817-164242.1
|   |   |   +-- nebula-graphd.77a31b265149.root.log.INFO.20260817-164241.1
|   |   |   +-- nebula-graphd.77a31b265149.root.log.WARNING.20260817-164242.1
|   |   |   +-- nebula-graphd.890b4eef14f7.root.log.ERROR.20260818-112025.1
|   |   |   +-- nebula-graphd.890b4eef14f7.root.log.INFO.20260818-112022.1
|   |   |   +-- nebula-graphd.890b4eef14f7.root.log.WARNING.20260818-112025.1
|   |   |   +-- nebula-graphd.89b822eefb33.root.log.ERROR.20260817-152456.1
|   |   |   +-- nebula-graphd.89b822eefb33.root.log.INFO.20260817-152456.1
|   |   |   +-- nebula-graphd.89b822eefb33.root.log.WARNING.20260817-152456.1
|   |   |   +-- nebula-graphd.8afd5b1eb543.root.log.ERROR.20260817-141152.1
|   |   |   +-- nebula-graphd.8afd5b1eb543.root.log.INFO.20260817-141152.1
|   |   |   +-- nebula-graphd.8afd5b1eb543.root.log.WARNING.20260817-141152.1
|   |   |   +-- nebula-graphd.90aa804fcef9.root.log.ERROR.20260818-095442.1
|   |   |   +-- nebula-graphd.90aa804fcef9.root.log.INFO.20260818-095439.1
|   |   |   +-- nebula-graphd.90aa804fcef9.root.log.WARNING.20260818-095442.1
|   |   |   +-- nebula-graphd.9135739ed00a.root.log.ERROR.20260817-163455.1
|   |   |   +-- nebula-graphd.9135739ed00a.root.log.INFO.20260817-163453.1
|   |   |   +-- nebula-graphd.9135739ed00a.root.log.WARNING.20260817-163455.1
|   |   |   +-- nebula-graphd.9216ceb53acf.root.log.ERROR.20260818-231950.1
|   |   |   +-- nebula-graphd.9216ceb53acf.root.log.INFO.20260818-231947.1
|   |   |   +-- nebula-graphd.9216ceb53acf.root.log.WARNING.20260818-231950.1
|   |   |   +-- nebula-graphd.979e266cb68a.root.log.ERROR.20260818-221500.1
|   |   |   +-- nebula-graphd.979e266cb68a.root.log.INFO.20260818-221457.1
|   |   |   +-- nebula-graphd.979e266cb68a.root.log.WARNING.20260818-221500.1
|   |   |   +-- nebula-graphd.97bde79786f9.root.log.ERROR.20260819-003622.1
|   |   |   +-- nebula-graphd.97bde79786f9.root.log.INFO.20260819-003619.1
|   |   |   +-- nebula-graphd.97bde79786f9.root.log.WARNING.20260819-003622.1
|   |   |   +-- nebula-graphd.9cc2a76b4136.root.log.ERROR.20260819-024050.1
|   |   |   +-- nebula-graphd.9cc2a76b4136.root.log.INFO.20260819-024047.1
|   |   |   +-- nebula-graphd.9cc2a76b4136.root.log.WARNING.20260819-024050.1
|   |   |   +-- nebula-graphd.a128c8545289.root.log.ERROR.20260819-225706.1
|   |   |   +-- nebula-graphd.a128c8545289.root.log.INFO.20260819-225702.1
|   |   |   +-- nebula-graphd.a128c8545289.root.log.WARNING.20260819-225706.1
|   |   |   +-- nebula-graphd.a302328103a0.root.log.ERROR.20260818-234636.1
|   |   |   +-- nebula-graphd.a302328103a0.root.log.INFO.20260818-234633.1
|   |   |   +-- nebula-graphd.a302328103a0.root.log.WARNING.20260818-234636.1
|   |   |   +-- nebula-graphd.ae4f721d8f6c.root.log.ERROR.20260819-010314.1
|   |   |   +-- nebula-graphd.ae4f721d8f6c.root.log.INFO.20260819-010311.1
|   |   |   +-- nebula-graphd.ae4f721d8f6c.root.log.WARNING.20260819-010314.1
|   |   |   +-- nebula-graphd.aeec9b09822f.root.log.ERROR.20260817-164425.1
|   |   |   +-- nebula-graphd.aeec9b09822f.root.log.INFO.20260817-164424.1
|   |   |   +-- nebula-graphd.aeec9b09822f.root.log.WARNING.20260817-164425.1
|   |   |   +-- nebula-graphd.b838c2debd79.root.log.ERROR.20260819-023234.1
|   |   |   +-- nebula-graphd.b838c2debd79.root.log.INFO.20260819-023231.1
|   |   |   +-- nebula-graphd.b838c2debd79.root.log.WARNING.20260819-023234.1
|   |   |   +-- nebula-graphd.b92ae5db87a4.root.log.ERROR.20260818-232043.1
|   |   |   +-- nebula-graphd.b92ae5db87a4.root.log.INFO.20260818-232040.1
|   |   |   +-- nebula-graphd.b92ae5db87a4.root.log.WARNING.20260818-232043.1
|   |   |   +-- nebula-graphd.bc70aa836af6.root.log.ERROR.20260817-160258.1
|   |   |   +-- nebula-graphd.bc70aa836af6.root.log.INFO.20260817-160257.1
|   |   |   +-- nebula-graphd.bc70aa836af6.root.log.WARNING.20260817-160258.1
|   |   |   +-- nebula-graphd.c3e1a1338d45.root.log.ERROR.20260818-234541.1
|   |   |   +-- nebula-graphd.c3e1a1338d45.root.log.INFO.20260818-234538.1
|   |   |   +-- nebula-graphd.c3e1a1338d45.root.log.WARNING.20260818-234541.1
|   |   |   +-- nebula-graphd.c47e99517600.root.log.ERROR.20260818-220952.1
|   |   |   +-- nebula-graphd.c47e99517600.root.log.INFO.20260818-220949.1
|   |   |   +-- nebula-graphd.c47e99517600.root.log.WARNING.20260818-220952.1
|   |   |   +-- nebula-graphd.c8507b918fdd.root.log.ERROR.20260817-151359.1
|   |   |   +-- nebula-graphd.c8507b918fdd.root.log.INFO.20260817-151359.1
|   |   |   +-- nebula-graphd.c8507b918fdd.root.log.WARNING.20260817-151359.1
|   |   |   +-- nebula-graphd.ce86a4086c2e.root.log.ERROR.20260817-152938.1
|   |   |   +-- nebula-graphd.ce86a4086c2e.root.log.INFO.20260817-152938.1
|   |   |   +-- nebula-graphd.ce86a4086c2e.root.log.WARNING.20260817-152938.1
|   |   |   +-- nebula-graphd.d1f796a4fa96.root.log.ERROR.20260819-010847.1
|   |   |   +-- nebula-graphd.d1f796a4fa96.root.log.INFO.20260819-010844.1
|   |   |   +-- nebula-graphd.d1f796a4fa96.root.log.WARNING.20260819-010847.1
|   |   |   +-- nebula-graphd.d38b57b4f80f.root.log.ERROR.20260817-165051.1
|   |   |   +-- nebula-graphd.d38b57b4f80f.root.log.INFO.20260817-165048.1
|   |   |   +-- nebula-graphd.d38b57b4f80f.root.log.WARNING.20260817-165051.1
|   |   |   +-- nebula-graphd.d49db6626068.root.log.ERROR.20260818-100157.1
|   |   |   +-- nebula-graphd.d49db6626068.root.log.INFO.20260818-100154.1
|   |   |   +-- nebula-graphd.d49db6626068.root.log.WARNING.20260818-100157.1
|   |   |   +-- nebula-graphd.d6e282953d1e.root.log.ERROR.20260817-163305.1
|   |   |   +-- nebula-graphd.d6e282953d1e.root.log.INFO.20260817-163301.1
|   |   |   +-- nebula-graphd.d6e282953d1e.root.log.WARNING.20260817-163305.1
|   |   |   +-- nebula-graphd.dabe7742319a.root.log.ERROR.20260817-154142.1
|   |   |   +-- nebula-graphd.dabe7742319a.root.log.INFO.20260817-154142.1
|   |   |   +-- nebula-graphd.dabe7742319a.root.log.WARNING.20260817-154142.1
|   |   |   +-- nebula-graphd.db145df80ca0.root.log.ERROR.20260819-234120.1
|   |   |   +-- nebula-graphd.db145df80ca0.root.log.INFO.20260819-234116.1
|   |   |   +-- nebula-graphd.db145df80ca0.root.log.WARNING.20260819-234120.1
|   |   |   +-- nebula-graphd.dd718cffd933.root.log.ERROR.20260818-234413.1
|   |   |   +-- nebula-graphd.dd718cffd933.root.log.INFO.20260818-234410.1
|   |   |   +-- nebula-graphd.dd718cffd933.root.log.WARNING.20260818-234413.1
|   |   |   +-- nebula-graphd.df31d00909c6.root.log.ERROR.20260819-232845.1
|   |   |   +-- nebula-graphd.df31d00909c6.root.log.INFO.20260819-232842.1
|   |   |   +-- nebula-graphd.df31d00909c6.root.log.WARNING.20260819-232845.1
|   |   |   +-- nebula-graphd.e57bcb521e4d.root.log.ERROR.20260818-231237.1
|   |   |   +-- nebula-graphd.e57bcb521e4d.root.log.INFO.20260818-231233.1
|   |   |   +-- nebula-graphd.e57bcb521e4d.root.log.WARNING.20260818-231237.1
|   |   |   +-- nebula-graphd.edc6e619bb31.root.log.ERROR.20260819-002006.1
|   |   |   +-- nebula-graphd.edc6e619bb31.root.log.INFO.20260819-002003.1
|   |   |   +-- nebula-graphd.edc6e619bb31.root.log.WARNING.20260819-002006.1
|   |   |   +-- nebula-graphd.ee5e3b8984c8.root.log.ERROR.20260819-003218.1
|   |   |   +-- nebula-graphd.ee5e3b8984c8.root.log.INFO.20260819-003215.1
|   |   |   +-- nebula-graphd.ee5e3b8984c8.root.log.WARNING.20260819-003218.1
|   |   |   +-- nebula-graphd.ERROR
|   |   |   +-- nebula-graphd.f1e1911d77cc.root.log.ERROR.20260817-151858.1
|   |   |   +-- nebula-graphd.f1e1911d77cc.root.log.INFO.20260817-151858.1
|   |   |   +-- nebula-graphd.f1e1911d77cc.root.log.WARNING.20260817-151858.1
|   |   |   +-- nebula-graphd.f2c8baf5edef.root.log.ERROR.20260817-155351.1
|   |   |   +-- nebula-graphd.f2c8baf5edef.root.log.INFO.20260817-155350.1
|   |   |   +-- nebula-graphd.f2c8baf5edef.root.log.WARNING.20260817-155351.1
|   |   |   +-- nebula-graphd.f3d43361a2c7.root.log.ERROR.20260818-095259.1
|   |   |   +-- nebula-graphd.f3d43361a2c7.root.log.INFO.20260818-095256.1
|   |   |   +-- nebula-graphd.f3d43361a2c7.root.log.WARNING.20260818-095259.1
|   |   |   +-- nebula-graphd.fc9ac66fe6f1.root.log.ERROR.20260819-220935.1
|   |   |   +-- nebula-graphd.fc9ac66fe6f1.root.log.INFO.20260819-220931.1
|   |   |   +-- nebula-graphd.fc9ac66fe6f1.root.log.WARNING.20260819-220935.1
|   |   |   +-- nebula-graphd.INFO
|   |   |   \-- nebula-graphd.WARNING
|   |   +-- meta0
|   |   |   +-- 0128f9e5-cccf-41a1-783cffa9-f4779ff0.dmp
|   |   |   +-- 033066d5-0f11-47cd-5b355a8c-da730703.dmp
|   |   |   +-- 397fc419-431b-4002-ae2680aa-3f617a4e.dmp
|   |   |   +-- 4a702f65-dbfc-4bec-0e5653b2-02ef1863.dmp
|   |   |   +-- 5d7b5494-b431-401d-551e63ad-dcf2b616.dmp
|   |   |   +-- 623e4a44-2443-4c84-dad6ccb4-8fe2c57a.dmp
|   |   |   +-- 71e32b19-e8ff-4d51-4ea6d288-da494177.dmp
|   |   |   +-- 74d8c123-6251-443d-b40a50b8-e462b924.dmp
|   |   |   +-- 7fbc35be-c5e8-409b-fd15f4be-d9522006.dmp
|   |   |   +-- 8464d628-a1c2-44ff-4acd3a91-5c1ee0a9.dmp
|   |   |   +-- bd4509ae-9137-485b-f1d3d183-345bfbcc.dmp
|   |   |   +-- ebd2eb58-c4f9-49b1-cedeed84-ab385828.dmp
|   |   |   +-- f8e2744e-edc2-45ae-8532d6b2-4c8d8288.dmp
|   |   |   +-- metad-stderr.log
|   |   |   +-- metad-stdout.log
|   |   |   +-- nebula-metad.02db3379816d.root.log.ERROR.20260818-102530.1
|   |   |   +-- nebula-metad.02db3379816d.root.log.INFO.20260818-102510.1
|   |   |   +-- nebula-metad.02db3379816d.root.log.WARNING.20260818-102530.1
|   |   |   +-- nebula-metad.06f9a51f120c.root.log.ERROR.20260819-023245.1
|   |   |   +-- nebula-metad.06f9a51f120c.root.log.INFO.20260819-023214.1
|   |   |   +-- nebula-metad.06f9a51f120c.root.log.WARNING.20260819-023245.1
|   |   |   +-- nebula-metad.0924e3d3c477.root.log.ERROR.20260818-233801.1
|   |   |   +-- nebula-metad.0924e3d3c477.root.log.INFO.20260818-233727.1
|   |   |   +-- nebula-metad.0924e3d3c477.root.log.WARNING.20260818-233801.1
|   |   |   +-- nebula-metad.0a65c4cdbbe7.root.log.ERROR.20260817-141049.1
|   |   |   +-- nebula-metad.0a65c4cdbbe7.root.log.INFO.20260817-141049.1
|   |   |   +-- nebula-metad.0a65c4cdbbe7.root.log.WARNING.20260817-141049.1
|   |   |   +-- nebula-metad.0f3c3b2b756e.root.log.ERROR.20260818-221524.1
|   |   |   +-- nebula-metad.0f3c3b2b756e.root.log.INFO.20260818-221451.1
|   |   |   +-- nebula-metad.0f3c3b2b756e.root.log.WARNING.20260818-221524.1
|   |   |   +-- nebula-metad.10bf93292f2e.root.log.ERROR.20260819-010337.1
|   |   |   +-- nebula-metad.10bf93292f2e.root.log.INFO.20260819-010304.1
|   |   |   +-- nebula-metad.10bf93292f2e.root.log.WARNING.20260819-010337.1
|   |   |   +-- nebula-metad.14a6799f28ce.root.log.ERROR.20260817-145634.1
|   |   |   +-- nebula-metad.14a6799f28ce.root.log.INFO.20260817-145620.1
|   |   |   +-- nebula-metad.14a6799f28ce.root.log.WARNING.20260817-145634.1
|   |   |   +-- nebula-metad.18cc1a10a11e.root.log.ERROR.20260817-163456.1
|   |   |   +-- nebula-metad.18cc1a10a11e.root.log.INFO.20260817-163444.1
|   |   |   +-- nebula-metad.18cc1a10a11e.root.log.WARNING.20260817-163456.1
|   |   |   +-- nebula-metad.194c86f53409.root.log.ERROR.20260817-161902.1
|   |   |   +-- nebula-metad.194c86f53409.root.log.INFO.20260817-161850.1
|   |   |   +-- nebula-metad.194c86f53409.root.log.WARNING.20260817-161902.1
|   |   |   +-- nebula-metad.19bcdfb2f064.root.log.ERROR.20260817-152944.1
|   |   |   +-- nebula-metad.19bcdfb2f064.root.log.INFO.20260817-152930.1
|   |   |   +-- nebula-metad.19bcdfb2f064.root.log.WARNING.20260817-152944.1
|   |   |   +-- nebula-metad.1a0a3a149a7b.root.log.ERROR.20260819-234205.1
|   |   |   +-- nebula-metad.1a0a3a149a7b.root.log.FATAL.20260819-234205.1
|   |   |   +-- nebula-metad.1a0a3a149a7b.root.log.INFO.20260819-234205.1
|   |   |   +-- nebula-metad.1a0a3a149a7b.root.log.WARNING.20260819-234205.1
|   |   |   +-- nebula-metad.1b9f48883b96.root.log.ERROR.20260817-152502.1
|   |   |   +-- nebula-metad.1b9f48883b96.root.log.INFO.20260817-152448.1
|   |   |   +-- nebula-metad.1b9f48883b96.root.log.WARNING.20260817-152502.1
|   |   |   +-- nebula-metad.1c2ee6e4fe1a.root.log.ERROR.20260817-164557.1
|   |   |   +-- nebula-metad.1c2ee6e4fe1a.root.log.INFO.20260817-164540.1
|   |   |   +-- nebula-metad.1c2ee6e4fe1a.root.log.WARNING.20260817-164557.1
|   |   |   +-- nebula-metad.1cc9a1ffbf6e.root.log.ERROR.20260818-051953.1
|   |   |   +-- nebula-metad.1cc9a1ffbf6e.root.log.INFO.20260818-051930.1
|   |   |   +-- nebula-metad.1cc9a1ffbf6e.root.log.WARNING.20260818-051953.1
|   |   |   +-- nebula-metad.210feef05779.root.log.ERROR.20260818-101554.1
|   |   |   +-- nebula-metad.210feef05779.root.log.INFO.20260818-101535.1
|   |   |   +-- nebula-metad.210feef05779.root.log.WARNING.20260818-101554.1
|   |   |   +-- nebula-metad.21e4ebb0f5a9.root.log.ERROR.20260818-232942.1
|   |   |   +-- nebula-metad.21e4ebb0f5a9.root.log.INFO.20260818-232905.1
|   |   |   +-- nebula-metad.21e4ebb0f5a9.root.log.WARNING.20260818-232942.1
|   |   |   +-- nebula-metad.29eb39e9e1dc.root.log.ERROR.20260819-003636.1
|   |   |   +-- nebula-metad.29eb39e9e1dc.root.log.INFO.20260819-003609.1
|   |   |   +-- nebula-metad.29eb39e9e1dc.root.log.WARNING.20260819-003636.1
|   |   |   +-- nebula-metad.3243a5d99fb9.root.log.ERROR.20260819-234801.1
|   |   |   +-- nebula-metad.3243a5d99fb9.root.log.FATAL.20260819-234801.1
|   |   |   +-- nebula-metad.3243a5d99fb9.root.log.INFO.20260819-234801.1
|   |   |   +-- nebula-metad.3243a5d99fb9.root.log.WARNING.20260819-234801.1
|   |   |   +-- nebula-metad.354f11dcddc5.root.log.ERROR.20260818-111849.1
|   |   |   +-- nebula-metad.354f11dcddc5.root.log.INFO.20260818-111827.1
|   |   |   +-- nebula-metad.354f11dcddc5.root.log.WARNING.20260818-111849.1
|   |   |   +-- nebula-metad.387406ea7500.root.log.ERROR.20260819-225650.1
|   |   |   +-- nebula-metad.387406ea7500.root.log.FATAL.20260819-225650.1
|   |   |   +-- nebula-metad.387406ea7500.root.log.INFO.20260819-225649.1
|   |   |   +-- nebula-metad.387406ea7500.root.log.WARNING.20260819-225650.1
|   |   |   +-- nebula-metad.45f9edd10379.root.log.ERROR.20260817-163307.1
|   |   |   +-- nebula-metad.45f9edd10379.root.log.INFO.20260817-163251.1
|   |   |   +-- nebula-metad.45f9edd10379.root.log.WARNING.20260817-163307.1
|   |   |   +-- nebula-metad.46f588572ef8.root.log.ERROR.20260817-154147.1
|   |   |   +-- nebula-metad.46f588572ef8.root.log.INFO.20260817-154136.1
|   |   |   +-- nebula-metad.46f588572ef8.root.log.WARNING.20260817-154147.1
|   |   |   +-- nebula-metad.47198c217c79.root.log.ERROR.20260818-234605.1
|   |   |   +-- nebula-metad.47198c217c79.root.log.INFO.20260818-234529.1
|   |   |   +-- nebula-metad.47198c217c79.root.log.WARNING.20260818-234605.1
|   |   |   +-- nebula-metad.47b680703870.root.log.ERROR.20260817-161538.1
|   |   |   +-- nebula-metad.47b680703870.root.log.INFO.20260817-161522.1
|   |   |   +-- nebula-metad.47b680703870.root.log.WARNING.20260817-161538.1
|   |   |   +-- nebula-metad.4c2154a08da0.root.log.ERROR.20260817-155020.1
|   |   |   +-- nebula-metad.4c2154a08da0.root.log.INFO.20260817-155007.1
|   |   |   +-- nebula-metad.4c2154a08da0.root.log.WARNING.20260817-155020.1
|   |   |   +-- nebula-metad.4e6d44738330.root.log.ERROR.20260818-232003.1
|   |   |   +-- nebula-metad.4e6d44738330.root.log.INFO.20260818-231937.1
|   |   |   +-- nebula-metad.4e6d44738330.root.log.WARNING.20260818-232003.1
|   |   |   +-- nebula-metad.4f0b60df696a.root.log.ERROR.20260819-233836.1
|   |   |   +-- nebula-metad.4f0b60df696a.root.log.FATAL.20260819-233836.1
|   |   |   +-- nebula-metad.4f0b60df696a.root.log.INFO.20260819-233836.1
|   |   |   +-- nebula-metad.4f0b60df696a.root.log.WARNING.20260819-233836.1
|   |   |   +-- nebula-metad.50ececa61001.root.log.ERROR.20260818-221004.1
|   |   |   +-- nebula-metad.50ececa61001.root.log.INFO.20260818-220940.1
|   |   |   +-- nebula-metad.50ececa61001.root.log.WARNING.20260818-221004.1
|   |   |   +-- nebula-metad.5122ee7ed191.root.log.ERROR.20260818-234437.1
|   |   |   +-- nebula-metad.5122ee7ed191.root.log.INFO.20260818-234405.1
|   |   |   +-- nebula-metad.5122ee7ed191.root.log.WARNING.20260818-234437.1
|   |   |   +-- nebula-metad.59b030e2002e.root.log.ERROR.20260818-222655.1
|   |   |   +-- nebula-metad.59b030e2002e.root.log.INFO.20260818-222621.1
|   |   |   +-- nebula-metad.59b030e2002e.root.log.WARNING.20260818-222655.1
|   |   |   +-- nebula-metad.59ce8e46a656.root.log.ERROR.20260819-233305.1
|   |   |   +-- nebula-metad.59ce8e46a656.root.log.FATAL.20260819-233305.1
|   |   |   +-- nebula-metad.59ce8e46a656.root.log.INFO.20260819-233305.1
|   |   |   +-- nebula-metad.59ce8e46a656.root.log.WARNING.20260819-233305.1
|   |   |   +-- nebula-metad.5a38be9e002c.root.log.ERROR.20260818-233512.1
|   |   |   +-- nebula-metad.5a38be9e002c.root.log.INFO.20260818-233440.1
|   |   |   +-- nebula-metad.5a38be9e002c.root.log.WARNING.20260818-233512.1
|   |   |   +-- nebula-metad.5ddeaed26b8c.root.log.INFO.20260819-003520.1
|   |   |   +-- nebula-metad.63d099fe27fd.root.log.ERROR.20260818-232106.1
|   |   |   +-- nebula-metad.63d099fe27fd.root.log.INFO.20260818-232033.1
|   |   |   +-- nebula-metad.63d099fe27fd.root.log.WARNING.20260818-232106.1
|   |   |   +-- nebula-metad.6dcdf69e0c36.root.log.ERROR.20260817-150930.1
|   |   |   +-- nebula-metad.6dcdf69e0c36.root.log.INFO.20260817-150915.1
|   |   |   +-- nebula-metad.6dcdf69e0c36.root.log.WARNING.20260817-150930.1
|   |   |   +-- nebula-metad.728ad787b921.root.log.ERROR.20260817-160303.1
|   |   |   +-- nebula-metad.728ad787b921.root.log.INFO.20260817-160252.1
|   |   |   +-- nebula-metad.728ad787b921.root.log.WARNING.20260817-160303.1
|   |   |   +-- nebula-metad.77399cd07123.root.log.ERROR.20260817-164245.1
|   |   |   +-- nebula-metad.77399cd07123.root.log.INFO.20260817-164230.1
|   |   |   +-- nebula-metad.77399cd07123.root.log.WARNING.20260817-164245.1
|   |   |   +-- nebula-metad.78c6f3353af6.root.log.ERROR.20260817-155355.1
|   |   |   +-- nebula-metad.78c6f3353af6.root.log.INFO.20260817-155344.1
|   |   |   +-- nebula-metad.78c6f3353af6.root.log.WARNING.20260817-155355.1
|   |   |   +-- nebula-metad.811905392aaa.root.log.ERROR.20260818-100207.1
|   |   |   +-- nebula-metad.811905392aaa.root.log.INFO.20260818-100145.1
|   |   |   +-- nebula-metad.811905392aaa.root.log.WARNING.20260818-100207.1
|   |   |   +-- nebula-metad.8b975b59d5fa.root.log.ERROR.20260817-151904.1
|   |   |   +-- nebula-metad.8b975b59d5fa.root.log.INFO.20260817-151847.1
|   |   |   +-- nebula-metad.8b975b59d5fa.root.log.WARNING.20260817-151904.1
|   |   |   +-- nebula-metad.8bafe3926488.root.log.ERROR.20260819-234049.1
|   |   |   +-- nebula-metad.8bafe3926488.root.log.FATAL.20260819-234049.1
|   |   |   +-- nebula-metad.8bafe3926488.root.log.INFO.20260819-234049.1
|   |   |   +-- nebula-metad.8bafe3926488.root.log.WARNING.20260819-234049.1
|   |   |   +-- nebula-metad.93b375ecb3ab.root.log.ERROR.20260819-024114.1
|   |   |   +-- nebula-metad.93b375ecb3ab.root.log.INFO.20260819-024040.1
|   |   |   +-- nebula-metad.93b375ecb3ab.root.log.WARNING.20260819-024114.1
|   |   |   +-- nebula-metad.963c9fd26595.root.log.ERROR.20260818-095310.1
|   |   |   +-- nebula-metad.963c9fd26595.root.log.INFO.20260818-095249.1
|   |   |   +-- nebula-metad.963c9fd26595.root.log.WARNING.20260818-095310.1
|   |   |   +-- nebula-metad.9c576e287ddf.root.log.ERROR.20260817-164430.1
|   |   |   +-- nebula-metad.9c576e287ddf.root.log.INFO.20260817-164414.1
|   |   |   +-- nebula-metad.9c576e287ddf.root.log.WARNING.20260817-164430.1
|   |   |   +-- nebula-metad.9d60cc576aee.root.log.ERROR.20260818-112035.1
|   |   |   +-- nebula-metad.9d60cc576aee.root.log.INFO.20260818-112013.1
|   |   |   +-- nebula-metad.9d60cc576aee.root.log.WARNING.20260818-112035.1
|   |   |   +-- nebula-metad.aa7cd63992c5.root.log.ERROR.20260819-221310.1
|   |   |   +-- nebula-metad.aa7cd63992c5.root.log.FATAL.20260819-221310.1
|   |   |   +-- nebula-metad.aa7cd63992c5.root.log.INFO.20260819-221310.1
|   |   |   +-- nebula-metad.aa7cd63992c5.root.log.WARNING.20260819-221310.1
|   |   |   +-- nebula-metad.b88ea9822e94.root.log.ERROR.20260819-233016.1
|   |   |   +-- nebula-metad.b88ea9822e94.root.log.FATAL.20260819-233016.1
|   |   |   +-- nebula-metad.b88ea9822e94.root.log.INFO.20260819-233016.1
|   |   |   +-- nebula-metad.b88ea9822e94.root.log.WARNING.20260819-233016.1
|   |   |   +-- nebula-metad.bd0c7f413d20.root.log.ERROR.20260819-220919.1
|   |   |   +-- nebula-metad.bd0c7f413d20.root.log.FATAL.20260819-220919.1
|   |   |   +-- nebula-metad.bd0c7f413d20.root.log.INFO.20260819-220919.1
|   |   |   +-- nebula-metad.bd0c7f413d20.root.log.WARNING.20260819-220919.1
|   |   |   +-- nebula-metad.c1790eda0df1.root.log.ERROR.20260819-233435.1
|   |   |   +-- nebula-metad.c1790eda0df1.root.log.FATAL.20260819-233435.1
|   |   |   +-- nebula-metad.c1790eda0df1.root.log.INFO.20260819-233435.1
|   |   |   +-- nebula-metad.c1790eda0df1.root.log.WARNING.20260819-233435.1
|   |   |   +-- nebula-metad.d128b0bc755e.root.log.ERROR.20260817-164922.1
|   |   |   +-- nebula-metad.d128b0bc755e.root.log.INFO.20260817-164904.1
|   |   |   +-- nebula-metad.d128b0bc755e.root.log.WARNING.20260817-164922.1
|   |   |   +-- nebula-metad.d29d86658453.root.log.ERROR.20260819-022900.1
|   |   |   +-- nebula-metad.d29d86658453.root.log.INFO.20260819-022824.1
|   |   |   +-- nebula-metad.d29d86658453.root.log.WARNING.20260819-022900.1
|   |   |   +-- nebula-metad.d43893565ca4.root.log.ERROR.20260818-065014.1
|   |   |   +-- nebula-metad.d43893565ca4.root.log.INFO.20260818-064958.1
|   |   |   +-- nebula-metad.d43893565ca4.root.log.WARNING.20260818-065014.1
|   |   |   +-- nebula-metad.d707a3ad55ba.root.log.ERROR.20260819-002343.1
|   |   |   +-- nebula-metad.d707a3ad55ba.root.log.INFO.20260819-002310.1
|   |   |   +-- nebula-metad.d707a3ad55ba.root.log.WARNING.20260819-002343.1
|   |   |   +-- nebula-metad.d8fd01d3cd9e.root.log.INFO.20260817-143358.1
|   |   |   +-- nebula-metad.da3d7190b641.root.log.ERROR.20260819-010911.1
|   |   |   +-- nebula-metad.da3d7190b641.root.log.INFO.20260819-010836.1
|   |   |   +-- nebula-metad.da3d7190b641.root.log.WARNING.20260819-010911.1
|   |   |   +-- nebula-metad.da6da9789aa6.root.log.ERROR.20260818-231250.1
|   |   |   +-- nebula-metad.da6da9789aa6.root.log.INFO.20260818-231225.1
|   |   |   +-- nebula-metad.da6da9789aa6.root.log.WARNING.20260818-231250.1
|   |   |   +-- nebula-metad.ea345016eb22.root.log.ERROR.20260819-232835.1
|   |   |   +-- nebula-metad.ea345016eb22.root.log.FATAL.20260819-232835.1
|   |   |   +-- nebula-metad.ea345016eb22.root.log.INFO.20260819-232834.1
|   |   |   +-- nebula-metad.ea345016eb22.root.log.WARNING.20260819-232835.1
|   |   |   +-- nebula-metad.eba497d5dc2e.root.log.ERROR.20260819-002030.1
|   |   |   +-- nebula-metad.eba497d5dc2e.root.log.INFO.20260819-001954.1
|   |   |   +-- nebula-metad.eba497d5dc2e.root.log.WARNING.20260819-002030.1
|   |   |   +-- nebula-metad.edf8732d4cd6.root.log.ERROR.20260818-095452.1
|   |   |   +-- nebula-metad.edf8732d4cd6.root.log.INFO.20260818-095430.1
|   |   |   +-- nebula-metad.edf8732d4cd6.root.log.WARNING.20260818-095452.1
|   |   |   +-- nebula-metad.ERROR
|   |   |   +-- nebula-metad.f0c5b26bf88d.root.log.ERROR.20260817-151404.1
|   |   |   +-- nebula-metad.f0c5b26bf88d.root.log.INFO.20260817-151349.1
|   |   |   +-- nebula-metad.f0c5b26bf88d.root.log.WARNING.20260817-151404.1
|   |   |   +-- nebula-metad.f3a11495c359.root.log.ERROR.20260818-234659.1
|   |   |   +-- nebula-metad.f3a11495c359.root.log.INFO.20260818-234627.1
|   |   |   +-- nebula-metad.f3a11495c359.root.log.WARNING.20260818-234659.1
|   |   |   +-- nebula-metad.f4038ccc478d.root.log.ERROR.20260819-023434.1
|   |   |   +-- nebula-metad.f4038ccc478d.root.log.INFO.20260819-023357.1
|   |   |   +-- nebula-metad.f4038ccc478d.root.log.WARNING.20260819-023434.1
|   |   |   +-- nebula-metad.f689f8d8c49f.root.log.ERROR.20260817-165102.1
|   |   |   +-- nebula-metad.f689f8d8c49f.root.log.INFO.20260817-165040.1
|   |   |   +-- nebula-metad.f689f8d8c49f.root.log.WARNING.20260817-165102.1
|   |   |   +-- nebula-metad.f6a63d07dada.root.log.ERROR.20260819-003232.1
|   |   |   +-- nebula-metad.f6a63d07dada.root.log.INFO.20260819-003204.1
|   |   |   +-- nebula-metad.f6a63d07dada.root.log.WARNING.20260819-003232.1
|   |   |   +-- nebula-metad.f9a92cd9c36f.root.log.ERROR.20260819-004737.1
|   |   |   +-- nebula-metad.f9a92cd9c36f.root.log.INFO.20260819-004702.1
|   |   |   +-- nebula-metad.f9a92cd9c36f.root.log.WARNING.20260819-004737.1
|   |   |   +-- nebula-metad.FATAL
|   |   |   +-- nebula-metad.INFO
|   |   |   +-- nebula-metad.nebula-metad0.root.log.ERROR.20260819-235302.1
|   |   |   +-- nebula-metad.nebula-metad0.root.log.FATAL.20260819-235302.1
|   |   |   +-- nebula-metad.nebula-metad0.root.log.INFO.20260819-235302.1
|   |   |   +-- nebula-metad.nebula-metad0.root.log.WARNING.20260819-235302.1
|   |   |   \-- nebula-metad.WARNING
|   |   \-- storage0
|   |       +-- nebula-storaged.02bb9ac9d0c7.root.log.ERROR.20260819-022833.1
|   |       +-- nebula-storaged.02bb9ac9d0c7.root.log.INFO.20260819-022830.1
|   |       +-- nebula-storaged.02bb9ac9d0c7.root.log.WARNING.20260819-022830.1
|   |       +-- nebula-storaged.055faedc1ef4.root.log.ERROR.20260817-165048.1
|   |       +-- nebula-storaged.055faedc1ef4.root.log.INFO.20260817-165045.1
|   |       +-- nebula-storaged.055faedc1ef4.root.log.WARNING.20260817-165045.1
|   |       +-- nebula-storaged.05a73c5013fa.root.log.ERROR.20260817-155350.1
|   |       +-- nebula-storaged.05a73c5013fa.root.log.INFO.20260817-155347.1
|   |       +-- nebula-storaged.05a73c5013fa.root.log.WARNING.20260817-155347.1
|   |       +-- nebula-storaged.0ab0138718fc.root.log.ERROR.20260817-155016.1
|   |       +-- nebula-storaged.0ab0138718fc.root.log.INFO.20260817-155012.1
|   |       +-- nebula-storaged.0ab0138718fc.root.log.WARNING.20260817-155012.1
|   |       +-- nebula-storaged.110b31b9ecab.root.log.ERROR.20260819-234106.1
|   |       +-- nebula-storaged.110b31b9ecab.root.log.INFO.20260819-234102.1
|   |       +-- nebula-storaged.110b31b9ecab.root.log.WARNING.20260819-234102.1
|   |       +-- nebula-storaged.17decfce3b49.root.log.ERROR.20260819-004710.1
|   |       +-- nebula-storaged.17decfce3b49.root.log.INFO.20260819-004707.1
|   |       +-- nebula-storaged.17decfce3b49.root.log.WARNING.20260819-004707.1
|   |       +-- nebula-storaged.18b4b14394ed.root.log.ERROR.20260819-003617.1
|   |       +-- nebula-storaged.18b4b14394ed.root.log.INFO.20260819-003614.1
|   |       +-- nebula-storaged.18b4b14394ed.root.log.WARNING.20260819-003614.1
|   |       +-- nebula-storaged.192860af649f.root.log.ERROR.20260818-234411.1
|   |       +-- nebula-storaged.192860af649f.root.log.INFO.20260818-234408.1
|   |       +-- nebula-storaged.192860af649f.root.log.WARNING.20260818-234408.1
|   |       +-- nebula-storaged.1a72b871a778.root.log.ERROR.20260819-010310.1
|   |       +-- nebula-storaged.1a72b871a778.root.log.INFO.20260819-010307.1
|   |       +-- nebula-storaged.1a72b871a778.root.log.WARNING.20260819-010307.1
|   |       +-- nebula-storaged.1c46a7b5ddf2.root.log.ERROR.20260817-164422.1
|   |       +-- nebula-storaged.1c46a7b5ddf2.root.log.INFO.20260817-164418.1
|   |       +-- nebula-storaged.1c46a7b5ddf2.root.log.WARNING.20260817-164418.1
|   |       +-- nebula-storaged.214bed078905.root.log.ERROR.20260817-151357.1
|   |       +-- nebula-storaged.214bed078905.root.log.INFO.20260817-151355.1
|   |       +-- nebula-storaged.214bed078905.root.log.WARNING.20260817-151355.1
|   |       +-- nebula-storaged.2c9498680662.root.log.ERROR.20260817-152936.1
|   |       +-- nebula-storaged.2c9498680662.root.log.INFO.20260817-152935.1
|   |       +-- nebula-storaged.2c9498680662.root.log.WARNING.20260817-152935.1
|   |       +-- nebula-storaged.2ea7e825c541.root.log.ERROR.20260819-232841.1
|   |       +-- nebula-storaged.2ea7e825c541.root.log.INFO.20260819-232838.1
|   |       +-- nebula-storaged.2ea7e825c541.root.log.WARNING.20260819-232838.1
|   |       +-- nebula-storaged.2f236bc73e29.root.log.ERROR.20260818-231233.1
|   |       +-- nebula-storaged.2f236bc73e29.root.log.INFO.20260818-231230.1
|   |       +-- nebula-storaged.2f236bc73e29.root.log.WARNING.20260818-231230.1
|   |       +-- nebula-storaged.35c85f6cbb96.root.log.ERROR.20260819-235308.1
|   |       +-- nebula-storaged.35c85f6cbb96.root.log.INFO.20260819-235305.1
|   |       +-- nebula-storaged.35c85f6cbb96.root.log.WARNING.20260819-235305.1
|   |       +-- nebula-storaged.3658126206fa.root.log.ERROR.20260818-233446.1
|   |       +-- nebula-storaged.3658126206fa.root.log.INFO.20260818-233443.1
|   |       +-- nebula-storaged.3658126206fa.root.log.WARNING.20260818-233443.1
|   |       +-- nebula-storaged.43c16cf15314.root.log.ERROR.20260818-234535.1
|   |       +-- nebula-storaged.43c16cf15314.root.log.INFO.20260818-234532.1
|   |       +-- nebula-storaged.43c16cf15314.root.log.WARNING.20260818-234532.1
|   |       +-- nebula-storaged.47f4a48eda06.root.log.ERROR.20260818-222629.1
|   |       +-- nebula-storaged.47f4a48eda06.root.log.INFO.20260818-222626.1
|   |       +-- nebula-storaged.47f4a48eda06.root.log.WARNING.20260818-222626.1
|   |       +-- nebula-storaged.4d106e8d83e7.root.log.ERROR.20260818-221457.1
|   |       +-- nebula-storaged.4d106e8d83e7.root.log.INFO.20260818-221454.1
|   |       +-- nebula-storaged.4d106e8d83e7.root.log.WARNING.20260818-221454.1
|   |       +-- nebula-storaged.4f1894e752f3.root.log.ERROR.20260819-023226.1
|   |       +-- nebula-storaged.4f1894e752f3.root.log.INFO.20260819-023223.1
|   |       +-- nebula-storaged.4f1894e752f3.root.log.WARNING.20260819-023223.1
|   |       +-- nebula-storaged.52e1c0834603.root.log.ERROR.20260818-095439.1
|   |       +-- nebula-storaged.52e1c0834603.root.log.INFO.20260818-095436.1
|   |       +-- nebula-storaged.52e1c0834603.root.log.WARNING.20260818-095436.1
|   |       +-- nebula-storaged.5403d8213481.root.log.ERROR.20260817-145626.1
|   |       +-- nebula-storaged.5403d8213481.root.log.INFO.20260817-145625.1
|   |       +-- nebula-storaged.5403d8213481.root.log.WARNING.20260817-145625.1
|   |       +-- nebula-storaged.5acc270b45e0.root.log.ERROR.20260819-233841.1
|   |       +-- nebula-storaged.5acc270b45e0.root.log.INFO.20260819-233838.1
|   |       +-- nebula-storaged.5acc270b45e0.root.log.WARNING.20260819-233838.1
|   |       +-- nebula-storaged.5bf7e571c8f8.root.log.ERROR.20260819-003523.1
|   |       +-- nebula-storaged.5bf7e571c8f8.root.log.INFO.20260819-003523.1
|   |       +-- nebula-storaged.5bf7e571c8f8.root.log.WARNING.20260819-003523.1
|   |       +-- nebula-storaged.6355b57833d1.root.log.ERROR.20260817-164239.1
|   |       +-- nebula-storaged.6355b57833d1.root.log.INFO.20260817-164236.1
|   |       +-- nebula-storaged.6355b57833d1.root.log.WARNING.20260817-164236.1
|   |       +-- nebula-storaged.68594eff46c8.root.log.ERROR.20260818-220949.1
|   |       +-- nebula-storaged.68594eff46c8.root.log.INFO.20260818-220946.1
|   |       +-- nebula-storaged.68594eff46c8.root.log.WARNING.20260818-220946.1
|   |       +-- nebula-storaged.72519a7373a1.root.log.ERROR.20260817-150922.1
|   |       +-- nebula-storaged.72519a7373a1.root.log.INFO.20260817-150921.1
|   |       +-- nebula-storaged.72519a7373a1.root.log.WARNING.20260817-150921.1
|   |       +-- nebula-storaged.73e861981301.root.log.ERROR.20260817-152454.1
|   |       +-- nebula-storaged.73e861981301.root.log.INFO.20260817-152453.1
|   |       +-- nebula-storaged.73e861981301.root.log.WARNING.20260817-152453.1
|   |       +-- nebula-storaged.7abe66280dd8.root.log.ERROR.20260817-141125.1
|   |       +-- nebula-storaged.7abe66280dd8.root.log.INFO.20260817-141122.1
|   |       +-- nebula-storaged.7abe66280dd8.root.log.WARNING.20260817-141122.1
|   |       +-- nebula-storaged.869ecbcf1c27.root.log.ERROR.20260817-143407.1
|   |       +-- nebula-storaged.869ecbcf1c27.root.log.INFO.20260817-143404.1
|   |       +-- nebula-storaged.869ecbcf1c27.root.log.WARNING.20260817-143404.1
|   |       +-- nebula-storaged.8a60681a2887.root.log.ERROR.20260818-232915.1
|   |       +-- nebula-storaged.8a60681a2887.root.log.INFO.20260818-232912.1
|   |       +-- nebula-storaged.8a60681a2887.root.log.WARNING.20260818-232912.1
|   |       +-- nebula-storaged.8cc136f88420.root.log.ERROR.20260819-010842.1
|   |       +-- nebula-storaged.8cc136f88420.root.log.INFO.20260819-010839.1
|   |       +-- nebula-storaged.8cc136f88420.root.log.WARNING.20260819-010839.1
|   |       +-- nebula-storaged.92c73d51c2d7.root.log.ERROR.20260817-154143.1
|   |       +-- nebula-storaged.92c73d51c2d7.root.log.INFO.20260817-154139.1
|   |       +-- nebula-storaged.92c73d51c2d7.root.log.WARNING.20260817-154139.1
|   |       +-- nebula-storaged.92d40c7c23bf.root.log.ERROR.20260818-231947.1
|   |       +-- nebula-storaged.92d40c7c23bf.root.log.INFO.20260818-231944.1
|   |       +-- nebula-storaged.92d40c7c23bf.root.log.WARNING.20260818-231944.1
|   |       +-- nebula-storaged.9fbffb4fb898.root.log.ERROR.20260818-233735.1
|   |       +-- nebula-storaged.9fbffb4fb898.root.log.INFO.20260818-233732.1
|   |       +-- nebula-storaged.9fbffb4fb898.root.log.WARNING.20260818-233732.1
|   |       +-- nebula-storaged.9fd981f51b2a.root.log.ERROR.20260818-051937.1
|   |       +-- nebula-storaged.9fd981f51b2a.root.log.INFO.20260818-051934.1
|   |       +-- nebula-storaged.9fd981f51b2a.root.log.WARNING.20260818-051934.1
|   |       +-- nebula-storaged.a1041d88d663.root.log.ERROR.20260819-220930.1
|   |       +-- nebula-storaged.a1041d88d663.root.log.INFO.20260819-220927.1
|   |       +-- nebula-storaged.a1041d88d663.root.log.WARNING.20260819-220927.1
|   |       +-- nebula-storaged.a18890c14e85.root.log.ERROR.20260819-234212.1
|   |       +-- nebula-storaged.a18890c14e85.root.log.INFO.20260819-234209.1
|   |       +-- nebula-storaged.a18890c14e85.root.log.WARNING.20260819-234209.1
|   |       +-- nebula-storaged.a30ff3cfa530.root.log.ERROR.20260818-112020.1
|   |       +-- nebula-storaged.a30ff3cfa530.root.log.INFO.20260818-112017.1
|   |       +-- nebula-storaged.a30ff3cfa530.root.log.WARNING.20260818-112017.1
|   |       +-- nebula-storaged.a8383c09a9cb.root.log.ERROR.20260818-111836.1
|   |       +-- nebula-storaged.a8383c09a9cb.root.log.INFO.20260818-111833.1
|   |       +-- nebula-storaged.a8383c09a9cb.root.log.WARNING.20260818-111833.1
|   |       +-- nebula-storaged.a9360de720c5.root.log.ERROR.20260818-102516.1
|   |       +-- nebula-storaged.a9360de720c5.root.log.INFO.20260818-102513.1
|   |       +-- nebula-storaged.a9360de720c5.root.log.WARNING.20260818-102513.1
|   |       +-- nebula-storaged.ad7271fe33cb.root.log.ERROR.20260818-100151.1
|   |       +-- nebula-storaged.ad7271fe33cb.root.log.INFO.20260818-100148.1
|   |       +-- nebula-storaged.ad7271fe33cb.root.log.WARNING.20260818-100148.1
|   |       +-- nebula-storaged.b03957c01a4d.root.log.ERROR.20260819-221325.1
|   |       +-- nebula-storaged.b03957c01a4d.root.log.INFO.20260819-221321.1
|   |       +-- nebula-storaged.b03957c01a4d.root.log.WARNING.20260819-221321.1
|   |       +-- nebula-storaged.b40fb6523b64.root.log.ERROR.20260819-233312.1
|   |       +-- nebula-storaged.b40fb6523b64.root.log.INFO.20260819-233309.1
|   |       +-- nebula-storaged.b40fb6523b64.root.log.WARNING.20260819-233309.1
|   |       +-- nebula-storaged.b59276ca69b1.root.log.ERROR.20260819-023408.1
|   |       +-- nebula-storaged.b59276ca69b1.root.log.INFO.20260819-023405.1
|   |       +-- nebula-storaged.b59276ca69b1.root.log.WARNING.20260819-023405.1
|   |       +-- nebula-storaged.b6e2e8bcf29c.root.log.ERROR.20260817-164549.1
|   |       +-- nebula-storaged.b6e2e8bcf29c.root.log.INFO.20260817-164546.1
|   |       +-- nebula-storaged.b6e2e8bcf29c.root.log.WARNING.20260817-164546.1
|   |       +-- nebula-storaged.baa0f12fc93d.root.log.ERROR.20260817-163258.1
|   |       +-- nebula-storaged.baa0f12fc93d.root.log.INFO.20260817-163255.1
|   |       +-- nebula-storaged.baa0f12fc93d.root.log.WARNING.20260817-163255.1
|   |       +-- nebula-storaged.baf3811a461b.root.log.ERROR.20260818-232040.1
|   |       +-- nebula-storaged.baf3811a461b.root.log.INFO.20260818-232037.1
|   |       +-- nebula-storaged.baf3811a461b.root.log.WARNING.20260818-232037.1
|   |       +-- nebula-storaged.bfe10307b54b.root.log.ERROR.20260819-003213.1
|   |       +-- nebula-storaged.bfe10307b54b.root.log.INFO.20260819-003210.1
|   |       +-- nebula-storaged.bfe10307b54b.root.log.WARNING.20260819-003210.1
|   |       +-- nebula-storaged.c1a018478582.root.log.ERROR.20260819-233443.1
|   |       +-- nebula-storaged.c1a018478582.root.log.INFO.20260819-233440.1
|   |       +-- nebula-storaged.c1a018478582.root.log.WARNING.20260819-233440.1
|   |       +-- nebula-storaged.cb371780dde9.root.log.ERROR.20260819-234816.1
|   |       +-- nebula-storaged.cb371780dde9.root.log.INFO.20260819-234812.1
|   |       +-- nebula-storaged.cb371780dde9.root.log.WARNING.20260819-234812.1
|   |       +-- nebula-storaged.ce68edec14a0.root.log.ERROR.20260817-161856.1
|   |       +-- nebula-storaged.ce68edec14a0.root.log.INFO.20260817-161853.1
|   |       +-- nebula-storaged.ce68edec14a0.root.log.WARNING.20260817-161853.1
|   |       +-- nebula-storaged.d3d7ba155cb3.root.log.ERROR.20260817-163453.1
|   |       +-- nebula-storaged.d3d7ba155cb3.root.log.INFO.20260817-163450.1
|   |       +-- nebula-storaged.d3d7ba155cb3.root.log.WARNING.20260817-163450.1
|   |       +-- nebula-storaged.d527c7e5e1b8.root.log.ERROR.20260818-234633.1
|   |       +-- nebula-storaged.d527c7e5e1b8.root.log.INFO.20260818-234630.1
|   |       +-- nebula-storaged.d527c7e5e1b8.root.log.WARNING.20260818-234630.1
|   |       +-- nebula-storaged.d53fad4302b8.root.log.ERROR.20260819-002004.1
|   |       +-- nebula-storaged.d53fad4302b8.root.log.INFO.20260819-002001.1
|   |       +-- nebula-storaged.d53fad4302b8.root.log.WARNING.20260819-002001.1
|   |       +-- nebula-storaged.d6270d66b642.root.log.ERROR.20260819-024047.1
|   |       +-- nebula-storaged.d6270d66b642.root.log.INFO.20260819-024044.1
|   |       +-- nebula-storaged.d6270d66b642.root.log.WARNING.20260819-024044.1
|   |       +-- nebula-storaged.de9cd4fd3ade.root.log.ERROR.20260819-002316.1
|   |       +-- nebula-storaged.de9cd4fd3ade.root.log.INFO.20260819-002313.1
|   |       +-- nebula-storaged.de9cd4fd3ade.root.log.WARNING.20260819-002313.1
|   |       +-- nebula-storaged.df291f59979a.root.log.ERROR.20260819-225659.1
|   |       +-- nebula-storaged.df291f59979a.root.log.INFO.20260819-225656.1
|   |       +-- nebula-storaged.df291f59979a.root.log.WARNING.20260819-225656.1
|   |       +-- nebula-storaged.e0953d86e0fc.root.log.ERROR.20260818-095256.1
|   |       +-- nebula-storaged.e0953d86e0fc.root.log.INFO.20260818-095253.1
|   |       +-- nebula-storaged.e0953d86e0fc.root.log.WARNING.20260818-095253.1
|   |       +-- nebula-storaged.e49cbff75040.root.log.ERROR.20260817-161530.1
|   |       +-- nebula-storaged.e49cbff75040.root.log.INFO.20260817-161528.1
|   |       +-- nebula-storaged.e49cbff75040.root.log.WARNING.20260817-161528.1
|   |       +-- nebula-storaged.e9f78fd7b4b1.root.log.ERROR.20260818-101540.1
|   |       +-- nebula-storaged.e9f78fd7b4b1.root.log.INFO.20260818-101537.1
|   |       +-- nebula-storaged.e9f78fd7b4b1.root.log.WARNING.20260818-101537.1
|   |       +-- nebula-storaged.ed417f950793.root.log.ERROR.20260817-151853.1
|   |       +-- nebula-storaged.ed417f950793.root.log.INFO.20260817-151853.1
|   |       +-- nebula-storaged.ed417f950793.root.log.WARNING.20260817-151853.1
|   |       +-- nebula-storaged.ERROR
|   |       +-- nebula-storaged.f4aa5fb24713.root.log.ERROR.20260818-065006.1
|   |       +-- nebula-storaged.f4aa5fb24713.root.log.INFO.20260818-065003.1
|   |       +-- nebula-storaged.f4aa5fb24713.root.log.WARNING.20260818-065003.1
|   |       +-- nebula-storaged.fb34a9d461c7.root.log.ERROR.20260817-164913.1
|   |       +-- nebula-storaged.fb34a9d461c7.root.log.INFO.20260817-164910.1
|   |       +-- nebula-storaged.fb34a9d461c7.root.log.WARNING.20260817-164910.1
|   |       +-- nebula-storaged.fdb0e82f1a4e.root.log.ERROR.20260817-160258.1
|   |       +-- nebula-storaged.fdb0e82f1a4e.root.log.INFO.20260817-160255.1
|   |       +-- nebula-storaged.fdb0e82f1a4e.root.log.WARNING.20260817-160255.1
|   |       +-- nebula-storaged.INFO
|   |       +-- nebula-storaged.WARNING
|   |       +-- storaged-stderr.log
|   |       \-- storaged-stdout.log
|   +-- Properties
|   |   \-- launchSettings.json
|   +-- Segmentation
|   |   +-- analysis-ik
|   |   |   +-- config
|   |   |   +-- commons-codec-1.11.jar
|   |   |   +-- commons-logging-1.2.jar
|   |   |   +-- elasticsearch-analysis-ik-9.4.3.jar
|   |   |   +-- entitlement-policy.yaml
|   |   |   +-- httpclient-4.5.13.jar
|   |   |   +-- httpcore-4.4.13.jar
|   |   |   +-- ik-core-1.0.jar
|   |   |   +-- plugin-descriptor.properties
|   |   |   \-- plugin-security.policy
|   |   \-- Dockerfile
|   +-- AppHost.cs
|   +-- AppHostResourceContext.cs
|   +-- appsettings.Development.json
|   +-- appsettings.json
|   +-- APromisedLand.AppHost.csproj
|   +-- DeepSeek.txt
|   +-- DiberySky.http
|   +-- KeycloakRealmpart.json
|   +-- litegraph.json
|   +-- LiteGraphResource.cs
|   \-- ReadMe.md
+-- APromisedLand.Maui
|   +-- Authentication
|   |   +-- AuthenticationService.cs
|   |   +-- JwtAuthenticationStateProvider.cs
|   |   +-- JwtAuthorizationMessageHandler.cs
|   |   \-- StorageKeys.cs
|   +-- Configs
|   |   +-- AppConfig.cs
|   |   +-- BuilderConfig.cs
|   |   +-- PlatformInfo.cs
|   |   \-- WeatherHttpClient.cs
|   +-- DiberyTree
|   |   \-- DiberyTreeClient.cs
|   +-- Helper
|   |   \-- MauiHelper.cs
|   +-- Platforms
|   |   +-- Android
|   |   |   \-- PlatformClass1.cs
|   |   +-- iOS
|   |   |   \-- PlatformClass1.cs
|   |   +-- MacCatalyst
|   |   |   \-- PlatformClass1.cs
|   |   \-- Windows
|   |       \-- PlatformClass1.cs
|   +-- Services
|   |   \-- MauiService.cs
|   \-- APromisedLand.Maui.csproj
+-- APromisedLand.MauiBlazor
|   +-- DiberyTree
|   |   +-- Interfaces
|   |   |   \-- ITreeClientService.cs
|   |   \-- Services
|   |       +-- AttributeApiClient.cs
|   |       +-- AttributeApiClient.Definition.cs
|   |       +-- AttributeApiClient.Location.cs
|   |       +-- AttributeApiClient.Table.cs
|   |       +-- AttributeApiClient.Value.cs
|   |       +-- AttributeLocationValueApiClient.cs
|   |       +-- DiberyTreeApiClient.cs
|   |       +-- TableValueApiClient.cs
|   |       \-- UnitsOfMeasureApiClient.cs
|   +-- Weather
|   |   +-- WeatherApiClient.cs
|   |   \-- WeatherForecast.cs
|   \-- APromisedLand.MauiBlazor.csproj
+-- APromisedLand.MauiServiceDefaults
|   +-- APromisedLand.MauiServiceDefaults.csproj
|   \-- Extensions.cs
+-- APromisedLand.Razor
|   +-- Components
|   |   +-- Dialogs
|   |   |   \-- DialogSubmitSky.razor
|   |   +-- Layout
|   |   |   +-- DialogPageSky.razor
|   |   |   +-- DialogSky.razor
|   |   |   +-- LayoutSky.razor
|   |   |   +-- NotFound.razor
|   |   |   +-- PageSky.razor
|   |   |   \-- StartPage.razor
|   |   +-- Loading
|   |   |   +-- ButtonLoadingSky.razor
|   |   |   +-- IconButtonLoadingSky.razor
|   |   |   \-- ProgressCircularSky.razor
|   |   +-- Projects
|   |   |   +-- AboutDialog.razor
|   |   |   +-- TestingDialog.razor
|   |   |   \-- TestingDialog.razor.cs
|   |   +-- SignIn
|   |   |   +-- PasswordSky.razor
|   |   |   +-- SignInSky.razor
|   |   |   \-- SignInUser.cs
|   |   +-- BoolFieldSky.razor
|   |   +-- MessageDialog.razor
|   |   \-- ReadOnlySky.razor
|   +-- Dialogs
|   |   +-- UnitsOfMeasure
|   |   |   +-- UnitOfMeasureDialog.razor
|   |   |   +-- UnitOfMeasurePage.razor
|   |   |   +-- UnitOfMeasureSelectDialog.razor
|   |   |   \-- UnitsOfMeasureDialogPage.razor
|   |   +-- DialogConfig.cs
|   |   \-- DialogHelper.cs
|   +-- DiberyTree
|   |   +-- Attributes
|   |   |   +-- Items
|   |   |   +-- Locations
|   |   |   +-- Tables
|   |   |   +-- Values
|   |   |   +-- AddAttributeValueDialog.razor
|   |   |   +-- AttributeDefinitionDialog.razor
|   |   |   +-- AttributeDefinitionListDialog.razor
|   |   |   +-- AttributeTypeField.razor
|   |   |   +-- AttributeValueInputSky.razor
|   |   |   +-- NodeAttributeItemsSky.razor
|   |   |   +-- NodeAttributeItemsSky.razor.cs
|   |   |   +-- NodeAttributeItemsSky.razor.Table.cs
|   |   |   +-- NodeAttributesDialog.razor
|   |   |   +-- NodeAttributesPanel.razor
|   |   |   \-- NodeAttributesSky.razor
|   |   +-- Base
|   |   |   +-- TreeDialogPageSky.razor
|   |   |   +-- TreeItemDataExtensions.cs
|   |   |   +-- TreeNodeDialog.razor
|   |   |   +-- TreeNodeDialogResult.cs
|   |   |   +-- TreePageSky.razor
|   |   |   +-- TreeSelectDialogSky.razor
|   |   |   +-- TreeSky.razor
|   |   |   +-- TreeSky.razor.Action.cs
|   |   |   +-- TreeSky.razor.cs
|   |   |   +-- TreeSky.razor.Helper.cs
|   |   |   +-- TreeSky.razor.Loading.cs
|   |   |   \-- TreeSky.razor.Node.cs
|   |   +-- Enums
|   |   |   \-- NodeAction.cs
|   |   +-- Helpers
|   |   |   \-- TreeDragDropHelper.cs
|   |   +-- Models
|   |   |   +-- NodeActionResult.cs
|   |   |   +-- NodeOperationOutcome.cs
|   |   |   +-- NodeTemplate.cs
|   |   |   +-- ParentSelectResult.cs
|   |   |   +-- SortResult.cs
|   |   |   \-- TreeNodeFormModel.cs
|   |   +-- Navigation
|   |   |   +-- HistoryEntry.cs
|   |   |   +-- ITreeNavigationHistoryService.cs
|   |   |   \-- TreeNavigationHistoryService.cs
|   |   +-- Nodes
|   |   |   +-- DialogTreeSky.razor
|   |   |   +-- TreeNodeActionsDialog.razor
|   |   |   +-- TreeNodeDeleteDialog.razor
|   |   |   +-- TreeNodeEditDialog.razor
|   |   |   +-- TreeNodeParentSelectDialog.razor
|   |   |   +-- TreeNodeSortDialog.razor
|   |   |   \-- TreeNodeViewDialog.razor
|   |   +-- Pages
|   |   |   +-- TreeFileDialogPage.razor
|   |   |   +-- TreeImageDialogPage.razor
|   |   |   +-- TreeLocationDialogPage.razor
|   |   |   \-- TreeVideoDialogPage.razor
|   |   +-- Services
|   |   |   +-- CategoryTreeClientService.cs
|   |   |   +-- TreeNodeDialogService.cs
|   |   |   \-- UnitTreeClientService.cs
|   |   +-- Trees
|   |   |   +-- Category
|   |   |   \-- Unit
|   |   \-- TreePage.razor.cs
|   +-- Helper
|   |   +-- Blazor
|   |   |   +-- BlazorHelper.Assemblies.cs
|   |   |   +-- BlazorHelper.cs
|   |   |   +-- BlazorHelper.MessageBox.cs
|   |   |   +-- BlazorHelper.Snackbar.cs
|   |   |   +-- BlazorHelper.StartSeeds.cs
|   |   |   \-- BlazorHelper.Tree.cs
|   |   \-- Dialog
|   |       +-- DialogHelper.Attribute.cs
|   |       \-- DialogHelper.cs
|   +-- Pages
|   |   \-- TestingPage.razor
|   +-- Services
|   |   +-- BlazorService.About.cs
|   |   +-- BlazorService.CategoryTree.cs
|   |   +-- BlazorService.cs
|   |   +-- BlazorService.MessageBox.cs
|   |   +-- BlazorService.Snackbar.cs
|   |   +-- BlazorService.StartPage.cs
|   |   +-- BlazorService.UnitOfMeasure.cs
|   |   +-- BlazorService.UnitTree.cs
|   |   +-- BlazorService.Validation.cs
|   |   \-- MessageService.cs
|   +-- TreeGraph
|   |   +-- Attributes
|   |   +-- Nodes
|   |   \-- Trees
|   +-- Weather
|   |   +-- LocalWeather.razor
|   |   +-- WeatherClient.razor
|   |   \-- WeatherFactory.razor
|   +-- wwwroot
|   |   +-- background.png
|   |   \-- exampleJsInterop.js
|   +-- _Imports.razor
|   +-- APromisedLand.Razor.csproj
|   +-- Component1.razor
|   +-- Component1.razor.css
|   \-- ExampleJsInterop.cs
+-- APromisedLand.ServiceDefaults
|   +-- APromisedLand.ServiceDefaults.csproj
|   \-- Extensions.cs
+-- APromisedLand.Shared
|   +-- DiberyTree
|   |   +-- Attributes
|   |   |   +-- DTOs
|   |   |   +-- Enums
|   |   |   +-- Models
|   |   |   \-- Validation
|   |   +-- Interfaces
|   |   |   +-- IArchivableTreeNodeBase.cs
|   |   |   +-- IHierarchyTreeNodeBase.cs
|   |   |   \-- ITreeNodeBase.cs
|   |   +-- Models
|   |   |   +-- CategoryTree.cs
|   |   |   +-- MoveNodeRequest.cs
|   |   |   +-- TreeItemData.cs
|   |   |   +-- TreeNodeDto.cs
|   |   |   +-- TreeQueryParams.cs
|   |   |   \-- UnitTree.cs
|   |   \-- DiberyTreeHelper.cs
|   +-- DTOs
|   |   +-- Overflow
|   |   |   +-- CreateAnswerDto.cs
|   |   |   \-- CreateQuestion.cs
|   |   +-- Shared
|   |   |   +-- PagedRequest.cs
|   |   |   \-- PagedResponse.cs
|   |   +-- Units
|   |   |   +-- CreateUnitOfMeasureCommand.cs
|   |   |   +-- UnitOfMeasureDto.cs
|   |   |   \-- UpdateUnitOfMeasureCommand.cs
|   |   \-- ApiResponse.cs
|   +-- Helper
|   |   +-- Enumerates.cs
|   |   +-- SharedHelper.cs
|   |   +-- SolutionHelper.Boost.cs
|   |   \-- SolutionHelper.cs
|   +-- Interfaces
|   |   \-- IAuthenticationService.cs
|   +-- Models
|   |   +-- PageInfo.cs
|   |   +-- ScreenInfo.cs
|   |   +-- UnitOfMeasure.cs
|   |   \-- WindowSize.cs
|   +-- Services
|   |   \-- Solution
|   |       +-- SolutionService.cs
|   |       +-- SolutionService.Host.cs
|   |       +-- SolutionService.Platform.cs
|   |       \-- SolutionService.SignIn.cs
|   +-- TreeGraph
|   |   \-- Models
|   |       +-- ITreeNode.cs
|   |       +-- TreeNodeBase.cs
|   |       +-- TreeNodeDto.cs
|   |       \-- TreeQueryOptions.cs
|   +-- Validators
|   |   \-- TagListValidator.cs
|   \-- APromisedLand.Shared.csproj
+-- APromisedLand.SharedApi
|   +-- MafRag
|   |   \-- Dtos
|   |       +-- EmbeddingDtos.cs
|   |       +-- RagIngestDtos.cs
|   |       +-- RagRetrieveDtos.cs
|   |       +-- RagStatsDto.cs
|   |       \-- RerankDtos.cs
|   \-- APromisedLand.SharedApi.csproj
+-- APromisedLand.SharedRazor
|   +-- Components
|   |   +-- Layout
|   |   |   +-- DialogSky.razor
|   |   |   +-- LayoutSky.razor
|   |   |   +-- NotFound.razor
|   |   |   +-- PageSky.razor
|   |   |   \-- StartPage.razor
|   |   +-- Loading
|   |   |   +-- ButtonLoadingSky.razor
|   |   |   +-- IconButtonLoadingSky.razor
|   |   |   \-- ProgressCircularSky.razor
|   |   +-- Projects
|   |   |   \-- AboutDialog.razor
|   |   +-- SignIn
|   |   |   +-- PasswordSky.razor
|   |   |   +-- SignInSky.razor
|   |   |   \-- SignInUser.cs
|   |   +-- TreeGraph
|   |   |   +-- TreeApiClient.cs
|   |   |   +-- TreeDataAdapter.cs
|   |   |   \-- TreeGraph.razor
|   |   +-- BoolFieldSky.razor
|   |   +-- MessageDialog.razor
|   |   \-- ReadOnlySky.razor
|   +-- Models
|   |   \-- TreeGraph
|   +-- _Imports.razor
|   \-- APromisedLand.SharedRazor.csproj
+-- aspire_integration
|   +-- AppHost.cs
|   +-- AppHost.csproj
|   +-- AppHostContext.cs
|   +-- ConsumerServiceExtensions.cs
|   +-- main_aspire.py
|   +-- NebulaGraphApiServiceExtension.cs
|   +-- PROJECT_STRUCTURE.txt
|   +-- pyproject.toml
|   +-- README.md
|   +-- ServiceReferenceExample.cs
|   \-- telemetry.py
+-- ConsoleFoundry_local_samples
|   +-- ConsoleFoundry_local_samples.csproj
|   \-- Program.cs
+-- ConsoleFoundryLocal
|   +-- ConsoleFoundryLocal.csproj
|   \-- Program.cs
+-- ConsoleFoundryLocalFunction
|   +-- ConsoleFoundryLocalFunction.csproj
|   \-- Program.cs
+-- ConsoleFoundryLocalMultiTurn
|   +-- ConsoleFoundryLocalMultiTurn.csproj
|   \-- Program.cs
+-- ConsoleFoundryLocalWeb
|   +-- ConsoleFoundryLocalWeb.csproj
|   \-- Program.cs
+-- DiberyBlazorWebSky
|   +-- Components
|   |   +-- Diagrams
|   |   |   +-- GraphDiagram.razor
|   |   |   +-- GraphDiagram.razor.css
|   |   |   +-- GraphNodeWidget.razor
|   |   |   +-- GraphNodeWidget.razor.css
|   |   |   +-- NodeDetailPanel.razor
|   |   |   \-- NodeDetailPanel.razor.css
|   |   +-- Layout
|   |   |   +-- MainLayout.razor
|   |   |   +-- MainLayout.razor.css
|   |   |   +-- NavMenu.razor
|   |   |   +-- NavMenu.razor.css
|   |   |   +-- ReconnectModal.razor
|   |   |   +-- ReconnectModal.razor.css
|   |   |   \-- ReconnectModal.razor.js
|   |   +-- Pages
|   |   |   +-- GraphExplorerForce
|   |   |   +-- Chat.razor
|   |   |   +-- Counter.razor
|   |   |   +-- Error.razor
|   |   |   +-- Files.razor
|   |   |   +-- Files.razor.css
|   |   |   +-- GraphAgentChat.razor
|   |   |   +-- GraphAgentChat.razor.cs
|   |   |   +-- GraphAgentChat.razor.css
|   |   |   +-- GraphChat.razor
|   |   |   +-- GraphChat.razor.css
|   |   |   +-- GraphExplorer.razor
|   |   |   +-- GraphExplorer.razor.css
|   |   |   +-- Home.razor
|   |   |   +-- McpChat.razor
|   |   |   +-- McpChat.razor.css
|   |   |   +-- McpGraphChat.razor
|   |   |   +-- McpGraphChat.razor.cs
|   |   |   +-- McpGraphChat.razor.css
|   |   |   +-- McpGraphExplorer.razor
|   |   |   +-- McpGraphExplorer.razor.cs
|   |   |   +-- McpGraphExplorer.razor.css
|   |   |   +-- McpGraphExplorerForce.razor
|   |   |   +-- McpGraphExplorerForce.razor.cs
|   |   |   +-- McpGraphExplorerForce.razor.css
|   |   |   +-- McpGraphExplorerForceAll.razor
|   |   |   +-- McpGraphExplorerForceAll.razor.cs
|   |   |   +-- McpGraphExplorerForceAll.razor.css
|   |   |   +-- McpQuery.razor
|   |   |   +-- McpQuery.razor.css
|   |   |   +-- NotFound.razor
|   |   |   +-- Upload.razor
|   |   |   +-- Upload.razor.css
|   |   |   +-- Weather.razor
|   |   |   +-- WriterCritic.razor
|   |   |   \-- WriterCritic.razor.css
|   |   +-- _Imports.razor
|   |   +-- App.razor
|   |   \-- Routes.razor
|   +-- Endpoints
|   |   \-- DownloadEndpoints.cs
|   +-- Models
|   |   +-- Graph
|   |   |   +-- CreateGraphRequest.cs
|   |   |   +-- EdgeDto.cs
|   |   |   +-- EdgeUpsertRequest.cs
|   |   |   +-- EdgeUpsertResult.cs
|   |   |   +-- GraphChatMessage.cs
|   |   |   +-- GraphContextBuilder.cs
|   |   |   +-- GraphDto.cs
|   |   |   +-- GraphImportExportModels.cs
|   |   |   +-- GraphNodeModel.cs
|   |   |   +-- LiteGraphDto.cs
|   |   |   +-- LiteGraphListResponse.cs
|   |   |   +-- NodeDto.cs
|   |   |   +-- NodeUpsertRequest.cs
|   |   |   \-- NodeUpsertResult.cs
|   |   +-- Mcp
|   |   |   \-- McpModels.cs
|   |   +-- ChatMessage.cs
|   |   +-- ChatRequest.cs
|   |   +-- ChatResponse.cs
|   |   +-- CompleteUploadRequest.cs
|   |   +-- CompleteUploadResponse.cs
|   |   +-- EmbedResponseDto.cs
|   |   +-- FileMetadataDto.cs
|   |   +-- InitiateUploadRequest.cs
|   |   +-- InitiateUploadResponse.cs
|   |   +-- SessionMessage.cs
|   |   +-- SessionMessagesReply.cs
|   |   +-- SessionsResponse.cs
|   |   +-- UploadStatusResponse.cs
|   |   +-- WorkflowReply.cs
|   |   +-- WorkflowRunRequest.cs
|   |   +-- WorkflowStep.cs
|   |   \-- WorkflowStreamEvent.cs
|   +-- Properties
|   |   \-- launchSettings.json
|   +-- Services
|   |   +-- ChatApiClient.cs
|   |   +-- FileStorageApiClient.cs
|   |   +-- GraphAdminApiClient.cs
|   |   +-- GraphAgentApiClient.cs
|   |   +-- GraphApiClient.cs
|   |   +-- GraphDynamicContextService.cs
|   |   +-- GraphImportExportService.cs
|   |   +-- MarkdownRenderer.cs
|   |   \-- WorkflowApiClient.cs
|   +-- wwwroot
|   |   +-- js
|   |   |   +-- file-download.js
|   |   |   +-- fileDownload.js
|   |   |   \-- site.js
|   |   +-- lib
|   |   |   \-- bootstrap
|   |   +-- app.css
|   |   +-- app.js
|   |   \-- favicon.png
|   +-- appsettings.Development.json
|   +-- appsettings.json
|   +-- DiberyBlazorWebSky.csproj
|   +-- Program.cs
|   +-- ReadMe.md
|   \-- test.json
+-- DiberyMauiSky
|   +-- Components
|   |   +-- Layout
|   |   |   +-- MainLayout.razor
|   |   |   +-- MainLayout.razor.css
|   |   |   +-- NavMenu.razor
|   |   |   \-- NavMenu.razor.css
|   |   +-- Pages
|   |   |   +-- Counter.razor
|   |   |   +-- Home.razor
|   |   |   \-- Weather.razor
|   |   +-- _Imports.razor
|   |   \-- Routes.razor
|   +-- Platforms
|   |   +-- Android
|   |   |   +-- Resources
|   |   |   +-- AndroidManifest.xml
|   |   |   +-- MainActivity.cs
|   |   |   \-- MainApplication.cs
|   |   +-- iOS
|   |   |   +-- Resources
|   |   |   +-- AppDelegate.cs
|   |   |   +-- Info.plist
|   |   |   \-- Program.cs
|   |   +-- MacCatalyst
|   |   |   +-- AppDelegate.cs
|   |   |   +-- Entitlements.plist
|   |   |   +-- Info.plist
|   |   |   \-- Program.cs
|   |   \-- Windows
|   |       +-- app.manifest
|   |       +-- App.xaml
|   |       +-- App.xaml.cs
|   |       \-- Package.appxmanifest
|   +-- Properties
|   |   \-- launchSettings.json
|   +-- Resources
|   |   +-- AppIcon
|   |   |   +-- appicon.svg
|   |   |   \-- appiconfg.svg
|   |   +-- Fonts
|   |   |   \-- OpenSans-Regular.ttf
|   |   +-- Images
|   |   |   \-- dotnet_bot.svg
|   |   +-- Raw
|   |   |   \-- AboutAssets.txt
|   |   \-- Splash
|   |       \-- splash.svg
|   +-- wwwroot
|   |   +-- lib
|   |   |   \-- bootstrap
|   |   +-- app.css
|   |   \-- index.html
|   +-- App.xaml
|   +-- App.xaml.cs
|   +-- DiberyMauiSky.csproj
|   +-- MainPage.xaml
|   +-- MainPage.xaml.cs
|   \-- MauiProgram.cs
+-- DiberySky
|   +-- Components
|   |   +-- Layout
|   |   |   +-- MainLayout.razor
|   |   |   +-- MainLayout.razor.css
|   |   |   +-- NavMenu.razor
|   |   |   +-- NavMenu.razor.css
|   |   |   \-- NotFound.razor
|   |   +-- Pages
|   |   |   +-- Counter.razor
|   |   |   +-- Home.razor
|   |   |   +-- Login.razor
|   |   |   +-- NotFound.razor
|   |   |   +-- OS_Info.razor
|   |   |   +-- ScreenInfo.razor
|   |   |   +-- seaweedfstus.html
|   |   |   +-- TestingApl.razor
|   |   |   \-- WindowSize.razor
|   |   +-- Projects
|   |   |   +-- SignInPage.razor
|   |   |   \-- StartPage.razor
|   |   +-- _Imports.razor
|   |   \-- Routes.razor
|   +-- Platforms
|   |   +-- Android
|   |   |   +-- Resources
|   |   |   +-- AndroidManifest.xml
|   |   |   +-- MainActivity.cs
|   |   |   \-- MainApplication.cs
|   |   +-- iOS
|   |   |   +-- Resources
|   |   |   +-- AppDelegate.cs
|   |   |   +-- Info.plist
|   |   |   \-- Program.cs
|   |   +-- MacCatalyst
|   |   |   +-- AppDelegate.cs
|   |   |   +-- Entitlements.plist
|   |   |   +-- Info.plist
|   |   |   \-- Program.cs
|   |   \-- Windows
|   |       +-- app.manifest
|   |       +-- App.xaml
|   |       +-- App.xaml.cs
|   |       \-- Package.appxmanifest
|   +-- Properties
|   |   \-- launchSettings.json
|   +-- Resources
|   |   +-- AppIcon
|   |   |   +-- appicon.svg
|   |   |   \-- appiconfg.svg
|   |   +-- Fonts
|   |   |   \-- OpenSans-Regular.ttf
|   |   +-- Images
|   |   |   \-- dotnet_bot.svg
|   |   +-- Raw
|   |   |   \-- AboutAssets.txt
|   |   \-- Splash
|   |       \-- splash.svg
|   +-- wwwroot
|   |   +-- lib
|   |   |   \-- bootstrap
|   |   +-- app.css
|   |   \-- index.html
|   +-- App.xaml
|   +-- App.xaml.cs
|   +-- DiberySky.csproj
|   +-- MainPage.xaml
|   +-- MainPage.xaml.cs
|   \-- MauiProgram.cs
+-- DiberyTreeService
|   +-- Controllers
|   |   +-- AttributeLocationValueController.cs
|   |   +-- AttributesController.cs
|   |   +-- AttributeTableValueController.cs
|   |   +-- CategoryTreeController.cs
|   |   +-- DiberyTreeController.cs
|   |   +-- UnitsOfMeasureController.cs
|   |   \-- UnitTreeController.cs
|   +-- Properties
|   |   \-- launchSettings.json
|   +-- appsettings.Development.json
|   +-- appsettings.json
|   +-- DiberyTreeService.csproj
|   +-- DiberyTreeService.http
|   +-- Program.cs
|   \-- ReadMe.md
+-- docker
|   +-- .env
|   \-- docker-compose.yaml
+-- docs
|   +-- skills
|   |   \-- tree-sky-doc-to-patch.md
|   +-- dev-seed.sql
|   +-- handoff-maui-blazor-adaptation.md
|   +-- project-tree.md
|   +-- smoke-checklist.md
|   \-- StringTree-DesignNotes.md
+-- ElasticsearchService
|   +-- Controllers
|   |   +-- SearchController.cs
|   |   \-- WeatherForecastController.cs
|   +-- Embeds
|   |   +-- EmbeddingService.cs
|   |   \-- IEmbeddingService.cs
|   +-- MessageHandlers
|   |   +-- QuestionCreatedHandler.cs
|   |   +-- QuestionDeletedHandler.cs
|   |   \-- QuestionUpdatedHandler.cs
|   +-- Models
|   |   +-- ElasticQuestion.cs
|   |   \-- SearchQuestion.cs
|   +-- Properties
|   |   \-- launchSettings.json
|   +-- Services
|   |   +-- ElasticIndexInitializer.cs
|   |   +-- ElasticsearchIndexInitializer.cs
|   |   +-- ElasticsearchIndexSetup.cs
|   |   \-- OllamaClientExtensions.cs
|   +-- appsettings.Development.json
|   +-- appsettings.json
|   +-- ElasticsearchService.csproj
|   +-- ElasticsearchService.http
|   +-- Program.cs
|   \-- WeatherForecast.cs
+-- FileStorageApi
|   +-- Controllers
|   |   +-- FilesController.cs
|   |   \-- UploadsController.cs
|   +-- Data
|   |   +-- Migrations
|   |   |   +-- 20260915052930_Initial.cs
|   |   |   +-- 20260915052930_Initial.Designer.cs
|   |   |   \-- FileStorageContextModelSnapshot.cs
|   |   \-- FileStorageContext.cs
|   +-- Entities
|   |   +-- DocumentAuditEntity.cs
|   |   +-- DocumentMetadataEntity.cs
|   |   +-- IndexTaskEntity.cs
|   |   +-- UploadChunkEntity.cs
|   |   \-- UploadSessionEntity.cs
|   +-- Files
|   |   +-- AuditEntry.cs
|   |   +-- AuditQueue.cs
|   |   +-- AuditWriterService.cs
|   |   +-- FileMetadataService.cs
|   |   \-- IFileMetadataService.cs
|   +-- Models
|   |   \-- FileMetadataDto.cs
|   +-- Properties
|   |   \-- launchSettings.json
|   +-- Security
|   |   \-- HttpCallerContext.cs
|   +-- Storage
|   |   +-- ChunkedReadStream.cs
|   |   +-- HashingReadStream.cs
|   |   +-- ICallerContext.cs
|   |   +-- IObjectStorage.cs
|   |   +-- ObjectNotFoundException.cs
|   |   +-- ObjectStorageOptions.cs
|   |   +-- ObjectStream.cs
|   |   +-- RangeNotSatisfiableException.cs
|   |   \-- S3ObjectStorage.cs
|   +-- Uploads
|   |   +-- CompleteUploadDtos.cs
|   |   +-- CompleteUploadResponse.cs
|   |   +-- FileUploadService.cs
|   |   +-- IFileUploadService.cs
|   |   +-- InitiateUploadRequest.cs
|   |   +-- InitiateUploadResponse.cs
|   |   +-- UploadChunkResponse.cs
|   |   +-- UploadCleanupOptions.cs
|   |   +-- UploadExceptions.cs
|   |   +-- UploadSessionCleanupService.cs
|   |   \-- UploadStatusResponse.cs
|   +-- appsettings.Development.json
|   +-- appsettings.json
|   +-- FileStorageApi.csproj
|   +-- FileStorageApi.http
|   \-- Program.cs
+-- FileTransService
|   +-- Controllers
|   |   +-- FileTransController.cs
|   |   \-- WeatherForecastController.cs
|   +-- Data
|   |   +-- Migrations
|   |   |   +-- 20260704053710_Initial.cs
|   |   |   +-- 20260704053710_Initial.Designer.cs
|   |   |   \-- FileTransDbContextModelSnapshot.cs
|   |   \-- FileTransDbContext.cs
|   +-- Models
|   |   +-- CompleteUploadRequest.cs
|   |   +-- FileMetadata.cs
|   |   +-- ISeaweedFsClient.cs
|   |   +-- SeaweedFsOptions.cs
|   |   +-- UploadFileRequest.cs
|   |   \-- UploadResponse.cs
|   +-- Properties
|   |   \-- launchSettings.json
|   +-- Services
|   |   \-- SeaweedFsClient.cs
|   +-- appsettings.Development.json
|   +-- appsettings.json
|   +-- FileTransService.csproj
|   +-- FileTransService.csproj.Backup.tmp
|   +-- FileTransService.http
|   +-- hello.txt
|   +-- Program.cs
|   +-- test.bin
|   +-- test.txt
|   +-- testOrg.txt
|   +-- TUS 功能验证.md
|   \-- WeatherForecast.cs
+-- FoundryLocalService
|   +-- Controllers
|   |   +-- FoundryLocalController.cs
|   |   \-- FoundryLocalControllerBase.cs
|   +-- Models
|   |   +-- ChatRequest.cs
|   |   +-- ChatResponse.cs
|   |   \-- LoadModelRequest.cs
|   +-- Properties
|   |   \-- launchSettings.json
|   +-- appsettings.Development.json
|   +-- appsettings.json
|   +-- FoundryLocalService.csproj
|   +-- FoundryLocalService.http
|   \-- Program.cs
+-- FunAsrService
|   +-- Dockerfile
|   \-- server.py
+-- infra
|   +-- .env
|   \-- docker-compose.yaml
+-- JwtTokenGenerator
|   +-- JwtTokenGenerator.csproj
|   \-- Program.cs
+-- MafAIService
|   +-- Agents
|   |   \-- AgentRunner.cs
|   +-- Controllers
|   |   +-- ChatController.cs
|   |   \-- WeatherForecastController.cs
|   +-- Models
|   |   +-- ChatRequest.cs
|   |   \-- ChatResponse.cs
|   +-- Properties
|   |   \-- launchSettings.json
|   +-- State
|   |   +-- IAgentSessionStore.cs
|   |   +-- InMemoryAgentSessionStore.cs
|   |   \-- RedisAgentSessionStore.cs
|   +-- appsettings.Development.json
|   +-- appsettings.json
|   +-- MafAIService.csproj
|   +-- MafAIService.http
|   +-- Program.cs
|   \-- WeatherForecast.cs
+-- MafRagApi
|   +-- Controllers
|   |   +-- AgentChatController.cs
|   |   +-- AgentToolsController.cs
|   |   +-- ChatController.cs
|   |   +-- HealthController.cs
|   |   +-- InstructTemplatesController.cs
|   |   +-- RagController.cs
|   |   +-- SessionsController.cs
|   |   +-- VllmChatController.cs
|   |   +-- VllmProxyController.cs
|   |   \-- WorkflowsController.cs
|   +-- Models
|   |   +-- AgentChatDto.cs
|   |   +-- AgentOptions.cs
|   |   +-- AgentToolDtos.cs
|   |   +-- ChatDtos.cs
|   |   +-- IInstructionTemplateStore.cs
|   |   +-- InstructionTemplate.cs
|   |   +-- LoopChatRequestDto.cs
|   |   +-- LoopChatResponseDto.cs
|   |   +-- LoopStreamChunkDto.cs
|   |   +-- RagDtos.cs
|   |   +-- SemanticModels.cs
|   |   +-- SessionListDto.cs
|   |   +-- VllmChatDto.cs
|   |   \-- WorkflowDtos.cs
|   +-- Properties
|   |   \-- launchSettings.json
|   +-- Services
|   |   +-- Tools
|   |   |   +-- Builtin
|   |   |   +-- DefaultToolRegistry.cs
|   |   |   +-- IToolRegistry.cs
|   |   |   \-- ToolDescriptor.cs
|   |   +-- AgentFactory.cs
|   |   +-- AgentSessionStore.cs
|   |   +-- IAgentFactory.cs
|   |   +-- InMemoryInstructionTemplateStore.cs
|   |   +-- InMemorySessionStore.cs
|   |   +-- ISessionStore.cs
|   |   +-- JsonSchemaValidator.cs
|   |   +-- NoThinkHandler.cs
|   |   +-- NoThinkPipelinePolicy.cs
|   |   +-- OllamaWarmupService.cs
|   |   +-- RagChatOrchestrator.cs
|   |   +-- StructuredOutput.cs
|   |   +-- ThinkStrippingChatClient.cs
|   |   +-- VectorSearchClient.cs
|   |   +-- VllmChatClient.cs
|   |   +-- VllmWarmupService.cs
|   |   \-- WorkflowService.cs
|   +-- tests
|   |   +-- MafRag.http
|   |   +-- MafSampleApi.http
|   |   +-- MafStructuredOutput.http
|   |   +-- Ollama.http
|   |   +-- Tools.http
|   |   \-- Workflow.http
|   +-- appsettings.Development.json
|   +-- appsettings.json
|   +-- docker.txt
|   +-- loop-stream.html
|   +-- loopstream.py
|   +-- MafRagApi.csproj
|   +-- Program.cs
|   \-- ReadMe.md
+-- MAFRagService
|   +-- Agents
|   |   +-- AnswerGeneratorAgent.cs
|   |   +-- AskResult.cs
|   |   +-- GraphReasonerAgent.cs
|   |   +-- OrchestratorAgent.cs
|   |   +-- QueryAnalyzerAgent.cs
|   |   \-- RetrieverAgent.cs
|   +-- Connectors
|   |   +-- IOllamaEmbeddingClient.cs
|   |   +-- OllamaEmbeddingClient.cs
|   |   \-- OllamaModelConnector.cs
|   +-- Controllers
|   |   +-- AskController.cs
|   |   +-- BaseApiController.cs
|   |   +-- DocsController.cs
|   |   \-- GraphController.cs
|   +-- Initializers
|   |   +-- DatabaseInitializer.cs
|   |   +-- GraphSchemaInitializer.cs
|   |   +-- SeaweedBucketInitializer.cs
|   |   \-- WeaviateSchemaInitializer.cs
|   +-- Memory
|   |   +-- NebulaGraphMemoryStore.cs
|   |   \-- NullMemoryStore.cs
|   +-- Models
|   |   +-- AskRequest.cs
|   |   +-- AskResponse.cs
|   |   +-- DocumentBlobInfo.cs
|   |   +-- DocumentMetadata.cs
|   |   +-- EntityInfo.cs
|   |   +-- GraphContext.cs
|   |   +-- QueryIntent.cs
|   |   \-- Source.cs
|   +-- Properties
|   |   \-- launchSettings.json
|   +-- Services
|   |   +-- TextChunking
|   |   |   +-- ITextChunker.cs
|   |   |   \-- TextChunker.cs
|   |   +-- Weaviate
|   |   |   +-- IWeaviateChunkWriter.cs
|   |   |   \-- WeaviateChunkWriter.cs
|   |   +-- DocumentAuditService.cs
|   |   +-- DocumentMetadataService.cs
|   |   +-- DocumentStorageService.cs
|   |   +-- EntityExtractionService.cs
|   |   +-- EventStoreService.cs
|   |   +-- IncrementalIndexer.cs
|   |   +-- IndexTaskService.cs
|   |   +-- KnowledgeGraphService.cs
|   |   +-- NebulaGraphExecutor.cs
|   |   +-- NullEntityExtractionService.cs
|   |   +-- RagService.cs
|   |   \-- VersionManager.cs
|   +-- Startup
|   |   +-- Configuration
|   |   |   +-- ConnectionStrings.cs
|   |   |   +-- DatabaseOptions.cs
|   |   |   +-- FeatureFlags.cs
|   |   |   +-- NebulaGraphAppOptions.cs
|   |   |   +-- OllamaEndpoint.cs
|   |   |   +-- OllamaOptions.cs
|   |   |   +-- OllamaOptionsValidator.cs
|   |   |   +-- Sanitizer.cs
|   |   |   +-- SeaweedFsOptions.cs
|   |   |   \-- WeaviateOptions.cs
|   |   +-- Diagnostics
|   |   |   +-- HangfireTelemetryFilter.cs
|   |   |   \-- MAFRagActivity.cs
|   |   +-- Extensions
|   |   |   +-- AiServiceCollectionExtensions.cs
|   |   |   +-- AuthServiceCollectionExtensions.cs
|   |   |   +-- BusinessServiceCollectionExtensions.cs
|   |   |   +-- HealthCheckServiceCollectionExtensions.cs
|   |   |   +-- HttpClientNames.cs
|   |   |   +-- InfrastructureServiceCollectionExtensions.cs
|   |   |   +-- MaFExtensions.cs
|   |   |   +-- ObservabilityServiceCollectionExtensions.cs
|   |   |   +-- OllamaHealthCheck.cs
|   |   |   +-- OptionsServiceCollectionExtensions.cs
|   |   |   \-- StartupInitializers.cs
|   |   \-- HealthChecks
|   |       +-- NebulaHealthCheck.cs
|   |       +-- PostgresHealthCheck.cs
|   |       +-- RedisHealthCheck.cs
|   |       \-- WeaviateHealthCheck.cs
|   +-- Stubs
|   |   +-- MAF
|   |   |   +-- AgentModelStubs.cs
|   |   |   +-- AgentStubs.cs
|   |   |   +-- MemoryStubs.cs
|   |   |   \-- ToolStubs.cs
|   |   \-- NebulaGraph
|   |       +-- ClientStubs.cs
|   |       \-- ResultSetStubs.cs
|   +-- Tools
|   |   +-- DocumentSearchTool.cs
|   |   +-- EntityExtractionTool.cs
|   |   +-- GraphQueryTool.cs
|   |   +-- HangfireDashboardAuthFilter.cs
|   |   \-- StorageTool.cs
|   +-- appsettings.Development.json
|   +-- appsettings.json
|   +-- appsettings.Production.json
|   +-- MAFRagService.csproj
|   +-- MAFRagService.http
|   +-- objects.json
|   +-- Program.cs
|   +-- ProgramAll.cs
|   +-- q1.json
|   +-- q2.json
|   +-- q3.json
|   +-- rag.json
|   +-- raw.json
|   +-- readme.md
|   \-- test-short.txt
+-- MafRagTreeGraph
|   +-- Components
|   |   +-- Layout
|   |   |   +-- MainLayout.razor
|   |   |   +-- MainLayout.razor.css
|   |   |   +-- NavMenu.razor
|   |   |   +-- NavMenu.razor.css
|   |   |   +-- ReconnectModal.razor
|   |   |   +-- ReconnectModal.razor.css
|   |   |   \-- ReconnectModal.razor.js
|   |   +-- Pages
|   |   |   +-- Counter.razor
|   |   |   +-- Error.razor
|   |   |   +-- Home.razor
|   |   |   +-- NotFound.razor
|   |   |   +-- TreeGraphPage.razor
|   |   |   \-- Weather.razor
|   |   +-- _Imports.razor
|   |   +-- App.razor
|   |   \-- Routes.razor
|   +-- Properties
|   |   \-- launchSettings.json
|   +-- wwwroot
|   |   +-- lib
|   |   |   \-- bootstrap
|   |   +-- app.css
|   |   \-- favicon.png
|   +-- appsettings.Development.json
|   +-- appsettings.json
|   +-- MafRagTreeGraph.csproj
|   \-- Program.cs
+-- MafService
|   +-- Controllers
|   |   \-- WeatherForecastController.cs
|   +-- Properties
|   |   \-- launchSettings.json
|   +-- appsettings.Development.json
|   +-- appsettings.json
|   +-- MafService.csproj
|   +-- MafService.http
|   +-- Program.cs
|   \-- WeatherForecast.cs
+-- MafStatefulApi
|   +-- Agents
|   |   \-- AgentRunner.cs
|   +-- Controllers
|   |   \-- ChatController.cs
|   +-- Endpoints
|   |   \-- ChatEndpoints.cs
|   +-- Models
|   |   +-- ChatRequest.cs
|   |   \-- ChatResponse.cs
|   +-- Properties
|   |   \-- launchSettings.json
|   +-- State
|   |   +-- IAgentSessionStore.cs
|   |   +-- InMemoryAgentSessionStore.cs
|   |   \-- RedisAgentSessionStore.cs
|   +-- appsettings.Development.json
|   +-- appsettings.json
|   +-- MafStatefulApi.csproj
|   +-- MafStatefulApi.http
|   \-- Program.cs
+-- MafStatefulApi.Client
|   +-- ApiClient.cs
|   +-- MafStatefulApi.Client.csproj
|   \-- Program.cs
+-- MafStatefulApi.Web
|   +-- Components
|   |   +-- Layout
|   |   |   +-- MainLayout.razor
|   |   |   +-- MainLayout.razor.css
|   |   |   +-- NavMenu.razor
|   |   |   +-- NavMenu.razor.css
|   |   |   +-- ReconnectModal.razor
|   |   |   +-- ReconnectModal.razor.css
|   |   |   \-- ReconnectModal.razor.js
|   |   +-- Pages
|   |   |   +-- Chat.razor
|   |   |   +-- Counter.razor
|   |   |   +-- Error.razor
|   |   |   +-- Home.razor
|   |   |   +-- NotFound.razor
|   |   |   \-- Weather.razor
|   |   +-- _Imports.razor
|   |   +-- App.razor
|   |   \-- Routes.razor
|   +-- Properties
|   |   \-- launchSettings.json
|   +-- Services
|   |   \-- ChatApiService.cs
|   +-- wwwroot
|   |   +-- lib
|   |   |   \-- bootstrap
|   |   +-- app.css
|   |   \-- favicon.png
|   +-- appsettings.Development.json
|   +-- appsettings.json
|   +-- MafStatefulApi.Web.csproj
|   \-- Program.cs
+-- MafVectorSearchApi
|   +-- Controllers
|   |   +-- EmbeddingController.cs
|   |   +-- RagController.cs
|   |   \-- RerankController.cs
|   +-- Models
|   |   +-- EmbeddingOptions.cs
|   |   \-- RerankerOptions.cs
|   +-- Properties
|   |   \-- launchSettings.json
|   +-- Services
|   |   +-- IRerankerClient.cs
|   |   +-- RagService.cs
|   |   \-- RerankerClient.cs
|   +-- appsettings.json
|   +-- MafVectorSearchApi.csproj
|   +-- MafVectorSearchApi.http
|   \-- Program.cs
+-- MAFWorkFlowApi
|   +-- Agents
|   |   +-- AgentRouter.cs
|   |   +-- AssistantAgentService.cs
|   |   +-- GraphAgentService.cs
|   |   +-- GraphTools.cs
|   |   +-- IConversationCatalog.cs
|   |   +-- LlmAgentRouter.cs
|   |   +-- MafAgentService.cs
|   |   +-- McpGraphTools.cs
|   |   +-- OllamaAgentOptions.cs
|   |   +-- OllamaWarmupService.cs
|   |   +-- RedisAgentSessionStore.cs
|   |   +-- ReplySanitizer.cs
|   |   \-- ToolCallContext.cs
|   +-- Controllers
|   |   +-- AgentsController.cs
|   |   +-- EmbeddingController.cs
|   |   +-- GraphAdminController.cs
|   |   +-- GraphAgentController.cs
|   |   +-- GraphController.cs
|   |   \-- WorkflowsController.cs
|   +-- HealthChecks
|   |   \-- OllamaModelReadyHealthCheck.cs
|   +-- Infrastructure
|   |   +-- LiteGraphEndpointResolver.cs
|   |   +-- LiteGraphExceptionMiddleware.cs
|   |   +-- LiteGraphOptions.cs
|   |   +-- LiteGraphRestClient.cs
|   |   +-- McpApiKeyMiddleware.cs
|   |   +-- NameValueCollectionConverter.cs
|   |   +-- OllamaEndpointResolver.cs
|   |   +-- ServiceCollectionExtensions.cs
|   |   \-- StableGuid.cs
|   +-- Models
|   |   +-- Graph
|   |   |   +-- GraphAdminDtos.cs
|   |   |   \-- SemanticModels.cs
|   |   +-- AgentDtos.cs
|   |   +-- AuthoringResults.cs
|   |   +-- EdgeUpsertRequest.cs
|   |   +-- EdgeUpsertResult.cs
|   |   +-- EmbeddingDtos.cs
|   |   +-- GraphAgentDtos.cs
|   |   +-- NodeUpsertRequest.cs
|   |   \-- NodeUpsertResult.cs
|   +-- Properties
|   |   \-- launchSettings.json
|   +-- Services
|   |   +-- Bm25Scorer.cs
|   |   +-- EdgeAuthoringService.cs
|   |   +-- GraphExportService.cs
|   |   +-- IntentParserService.cs
|   |   +-- NodeAuthoringService.cs
|   |   +-- RerankerService.cs
|   |   \-- SemanticSearchService.cs
|   +-- 基于LiteGraph书籍知识图谱 RAG 系统完整技术方案.md
|   +-- appsettings.Development.json
|   +-- appsettings.json
|   +-- MAFWorkFlowApi.csproj
|   +-- MAFWorkFlowApi.http
|   +-- Program.cs
|   \-- ReadMe.md
+-- nebula_fastapi
|   +-- app
|   |   +-- models
|   |   |   \-- schemas.py
|   |   +-- routers
|   |   |   +-- __init__.py
|   |   |   +-- graph.py
|   |   |   \-- health.py
|   |   +-- services
|   |   |   +-- nebula_client.py
|   |   |   \-- nebula_service.py
|   |   +-- __init__.py
|   |   +-- config.py
|   |   +-- dependencies.py
|   |   \-- main.py
|   +-- tests
|   |   \-- test_graph.py
|   +-- .env.example
|   +-- docker-compose.yml
|   +-- Dockerfile
|   +-- README.md
|   \-- requirements.txt
+-- NebulaApi.Proxy
|   +-- Controllers
|   |   +-- ConnectionController.cs
|   |   +-- EdgesController.cs
|   |   +-- JobsController.cs
|   |   +-- QueryController.cs
|   |   +-- SchemaController.cs
|   |   +-- SpacesController.cs
|   |   +-- SystemController.cs
|   |   +-- UsersController.cs
|   |   \-- VerticesController.cs
|   +-- Dtos
|   |   +-- EdgeDtos.cs
|   |   +-- JobDtos.cs
|   |   +-- QueryDtos.cs
|   |   +-- RawStmtIn.cs
|   |   +-- SchemaDtos.cs
|   |   +-- SpaceDtos.cs
|   |   +-- UserDtos.cs
|   |   \-- VertexDtos.cs
|   +-- Models
|   |   +-- ApiResult.cs
|   |   +-- NebulaFastApiOptions.cs
|   |   \-- ProxyResult.cs
|   +-- Properties
|   |   \-- launchSettings.json
|   +-- Services
|   |   \-- NebulaApiService.cs
|   +-- .gitignore
|   +-- appsettings.Development.json
|   +-- appsettings.json
|   +-- DiberySky.http
|   +-- NebulaApi.Proxy.csproj
|   \-- Program.cs
+-- NebulaGraphApiService
|   +-- Controllers
|   |   \-- WeatherForecastController.cs
|   +-- Properties
|   |   \-- launchSettings.json
|   +-- Services
|   |   +-- INebulaGraphClient.cs
|   |   +-- INebulaGraphSeedService.cs
|   |   +-- NebulaClientOptions.cs
|   |   +-- NebulaGraphGatewayClient.cs
|   |   +-- NebulaGraphGatewayOptions.cs
|   |   +-- NebulaGraphHttpClient.cs
|   |   +-- NebulaGraphNetClient.cs
|   |   +-- NebulaGraphOptions.cs
|   |   \-- NebulaGraphSeedService.cs
|   +-- appsettings.Development.json
|   +-- appsettings.json
|   +-- appsettingsGateway.json
|   +-- NebulaGraphApiService.csproj
|   +-- NebulaGraphApiService.http
|   +-- Program.cs
|   \-- WeatherForecast.cs
+-- NebulaGraphFastApiService
|   +-- app
|   |   +-- exceptions
|   |   |   +-- __init__.py
|   |   |   +-- exceptions.py
|   |   |   \-- handlers.py
|   |   +-- routers
|   |   |   +-- __init__.py
|   |   |   +-- connection.py
|   |   |   +-- edges.py
|   |   |   +-- jobs.py
|   |   |   +-- query.py
|   |   |   +-- schema.py
|   |   |   +-- spaces.py
|   |   |   +-- users.py
|   |   |   \-- vertices.py
|   |   +-- schemas
|   |   |   +-- __init__.py
|   |   |   +-- common.py
|   |   |   +-- edge.py
|   |   |   +-- job.py
|   |   |   +-- query.py
|   |   |   +-- schema.py
|   |   |   +-- space.py
|   |   |   +-- user.py
|   |   |   \-- vertex.py
|   |   +-- services
|   |   |   +-- __init__.py
|   |   |   +-- connection_service.py
|   |   |   +-- edge_service.py
|   |   |   +-- job_service.py
|   |   |   +-- query_service.py
|   |   |   +-- schema_service.py
|   |   |   +-- space_service.py
|   |   |   +-- user_service.py
|   |   |   \-- vertex_service.py
|   |   +-- utils
|   |   |   +-- __init__.py
|   |   |   +-- nebula_parser.py
|   |   |   +-- ngql.py
|   |   |   \-- response.py
|   |   +-- __init__.py
|   |   +-- auth.py
|   |   +-- config.py
|   |   +-- database.py
|   |   \-- main.py
|   +-- .env.example
|   +-- .gitignore
|   +-- requirements.txt
|   \-- run.py
+-- Notes
|   +-- 常规操作
|   |   +-- 综合操作.md
|   |   +-- Bash.md
|   |   +-- Docker.md
|   |   +-- Markdown.md
|   |   \-- user-secrets.md
|   +-- Elasticsearch
|   |   +-- analysis-ik
|   |   |   +-- config
|   |   |   +-- commons-codec-1.11.jar
|   |   |   +-- commons-logging-1.2.jar
|   |   |   +-- elasticsearch-analysis-ik-9.4.3.jar
|   |   |   +-- entitlement-policy.yaml
|   |   |   +-- httpclient-4.5.13.jar
|   |   |   +-- httpcore-4.4.13.jar
|   |   |   +-- ik-core-1.0.jar
|   |   |   +-- plugin-descriptor.properties
|   |   |   \-- plugin-security.policy
|   |   +-- 本地部署 Cross-Encoder.md
|   |   +-- 两阶段重排序.md
|   |   +-- 语义搜索方案.md
|   |   +-- 中文分词应用.md
|   |   +-- 中文语义搜索.md
|   |   +-- Dockerfile
|   |   +-- elasticsearch-analysis-ik-9.4.3.zip
|   |   +-- Plugins_analysis-ik.md
|   |   \-- test.json
|   +-- Tree
|   |   +-- DiberyTree 核心文件汇编.md
|   |   \-- Tree 完整代码.md
|   +-- 动态属性系统完整代码清单.md
|   +-- 为什么不能对一个人太好.md
|   \-- markitdown.md
+-- QuestionService
|   +-- Configs
|   |   +-- ElasticsearchConfig.cs
|   |   +-- KeycloakConfig.cs
|   |   +-- NatsPublishConfig.cs
|   |   +-- TypesenseConfig.cs
|   |   +-- WolverineToElasticsearchService.cs
|   |   \-- WolverineToTypesenseConfig.cs
|   +-- Controllers
|   |   +-- QuestionController.Answer.cs
|   |   +-- QuestionController.cs
|   |   +-- QuestionController.Semantic.cs
|   |   +-- TagsController.cs
|   |   \-- WeatherForecastController.cs
|   +-- Data
|   |   +-- Migrations
|   |   |   +-- 20260629211413_InitialCreate.cs
|   |   |   +-- 20260629211413_InitialCreate.Designer.cs
|   |   |   +-- 20260701140233_AnswerCreate.cs
|   |   |   +-- 20260701140233_AnswerCreate.Designer.cs
|   |   |   \-- QuestionDbContextModelSnapshot.cs
|   |   \-- QuestionDbContext.cs
|   +-- Models
|   |   +-- Answer.cs
|   |   +-- Question.cs
|   |   \-- Tag.cs
|   +-- Nats
|   |   +-- Consumers
|   |   |   +-- Elasticsearch
|   |   |   \-- Typesense
|   |   \-- Publishers
|   |       +-- IQuestionPublisher.cs
|   |       \-- QuestionPublisher.cs
|   +-- Properties
|   |   \-- launchSettings.json
|   +-- Services
|   |   +-- ElasticIndexInitializer.cs
|   |   +-- EmbeddingGenerator.cs
|   |   +-- IEmbeddingGenerator.cs
|   |   +-- IOllamaEmbeddingService.cs
|   |   +-- OllamaEmbeddingService.cs
|   |   +-- QuestionIndexService.cs
|   |   \-- TagService.cs
|   +-- appsettings.Development.json
|   +-- appsettings.json
|   +-- http-client.env.json
|   +-- Overflow.http
|   +-- Program.cs
|   +-- QuestionService.csproj
|   +-- QuestionService.http
|   \-- WeatherForecast.cs
+-- QuestionTypesenseService
|   +-- Controllers
|   |   \-- WeatherForecastController.cs
|   +-- Properties
|   |   \-- launchSettings.json
|   +-- appsettings.Development.json
|   +-- appsettings.json
|   +-- Program.cs
|   +-- QuestionTypesenseService.csproj
|   +-- QuestionTypesenseService.http
|   \-- WeatherForecast.cs
+-- SearchService
|   +-- Controllers
|   |   \-- TypesenseController.cs
|   +-- Data
|   |   \-- SearchInitializer.cs
|   +-- MessageHandlers
|   |   +-- QuestionCreatedHandler.cs
|   |   +-- QuestionDeletedHandler.cs
|   |   \-- QuestionUpdatedHandler.cs
|   +-- Models
|   |   \-- SearchQuestion.cs
|   +-- Properties
|   |   \-- launchSettings.json
|   +-- appsettings.Development.json
|   +-- appsettings.json
|   +-- Program.cs
|   +-- SearchService.csproj
|   \-- SearchService.http
+-- SemanticSearch.Api
|   +-- Controllers
|   |   +-- SearchController.cs
|   |   \-- WeatherForecastController.cs
|   +-- Models
|   |   +-- Document.cs
|   |   +-- SampleData.cs
|   |   \-- SearchResult.cs
|   +-- Properties
|   |   \-- launchSettings.json
|   +-- Services
|   |   +-- ElasticsearchService.cs
|   |   +-- EmbeddingService.cs
|   |   \-- OllamaClientExtensions.cs
|   +-- appsettings.Development.json
|   +-- appsettings.json
|   +-- Program.cs
|   +-- SemanticSearch.Api.csproj
|   +-- SemanticSearch.Api.http
|   \-- WeatherForecast.cs
+-- TreeGraph.Api
|   +-- Data
|   |   +-- Configurations
|   |   +-- Migrations
|   |   |   +-- 20261003070415_Initial.cs
|   |   |   +-- 20261003070415_Initial.Designer.cs
|   |   |   +-- 20261003230729_AddStringTreeNodes.cs
|   |   |   +-- 20261003230729_AddStringTreeNodes.Designer.cs
|   |   |   \-- TreeGraphDbContextModelSnapshot.cs
|   |   +-- Seeding
|   |   |   +-- EavSeeder.cs
|   |   |   \-- UnitSeedService.cs
|   |   \-- TreeGraphDbContext.cs
|   +-- NodeEavSky
|   |   +-- Controllers
|   |   |   +-- CustomTableDataController.cs
|   |   |   +-- EavController.cs
|   |   |   +-- EavEntityTypesController.cs
|   |   |   +-- EavMetadataController.cs
|   |   |   +-- InodeController.cs
|   |   |   +-- InodeQueryController.cs
|   |   |   +-- OptionItemsController.cs
|   |   |   +-- OptionSetsController.cs
|   |   |   \-- UnitsController.cs
|   |   +-- Entities
|   |   |   +-- AttributeAuditLog.cs
|   |   |   +-- AttributeDefinition.cs
|   |   |   +-- AttributeValue.cs
|   |   |   +-- CompositeTypeDefinition.cs
|   |   |   +-- CustomTableDefinition.cs
|   |   |   +-- CustomTableRow.cs
|   |   |   +-- EntityTypeDefinition.cs
|   |   |   +-- InodeEntity.cs
|   |   |   +-- InodeEntityType.cs
|   |   |   +-- OptionSet.cs
|   |   |   \-- Unit.cs
|   |   +-- Infrastructure
|   |   |   \-- DbExceptionHandler.cs
|   |   \-- Services
|   |       +-- AttributeCache.cs
|   |       +-- CompositeTypeCache.cs
|   |       +-- CompositeValueService.cs
|   |       +-- CustomTableCache.cs
|   |       +-- CustomTableQueryService.cs
|   |       +-- CustomTableReadService.cs
|   |       +-- CustomTableValidationService.cs
|   |       +-- CustomTableWriteService.cs
|   |       +-- DynamicCompositeValue.cs
|   |       +-- EavJsonConverters.cs
|   |       +-- EavQueryService.cs
|   |       +-- EavReadService.cs
|   |       +-- EavRequestBodyConverter.cs
|   |       +-- EavValidationService.cs
|   |       +-- EavWriteService.cs
|   |       +-- InodeEavFacade.cs
|   |       +-- InodeEntityService.cs
|   |       +-- NumericValue.cs
|   |       +-- OptionSetCache.cs
|   |       +-- UnitCache.cs
|   |       \-- UnitConverter.cs
|   +-- Properties
|   |   \-- launchSettings.json
|   +-- Repositories
|   +-- StringTreeSky
|   |   +-- StringTreeNodeController.cs
|   |   \-- StringTreeNodeSeeder.cs
|   +-- TreeSky
|   |   +-- EfTreeService.cs
|   |   +-- ITreeService.cs
|   |   \-- TreeControllerBase.cs
|   +-- appsettings.Development.json
|   +-- appsettings.json
|   +-- Program.cs
|   +-- TreeGraph.Api.csproj
|   +-- TreeGraph.Api.http
|   \-- TreeGraph.Api.zip
+-- TreeGraph.Api.Tests
|   +-- Fixtures
|   |   +-- EavApiFactory.cs
|   |   +-- InMemoryCaches.cs
|   |   +-- InodeTestData.cs
|   |   +-- IntegrationTestBase.cs
|   |   \-- TestData.cs
|   +-- Tests
|   |   +-- BatchDeleteTests.cs
|   |   +-- CompositeValueServiceTests.cs
|   |   +-- CrossRowUniqueTests.cs
|   |   +-- InodeConstraintTests.cs
|   |   +-- InodeEavFacadeTests.cs
|   |   +-- InodeEntityTests.cs
|   |   +-- InodeEntityTypeTests.cs
|   |   +-- InodeJsonTests.cs
|   |   +-- InodeQueryTests.cs
|   |   +-- MetadataUndeleteTests.cs
|   |   +-- OptimisticLockTests.cs
|   |   +-- OptionSetSoftDeleteTests.cs
|   |   +-- PatchEntityTests.cs
|   |   +-- QueryTests.cs
|   |   +-- TreeSkyTests.cs
|   |   \-- ValidationTests.cs
|   \-- TreeGraph.Api.Tests.csproj
+-- TreeGraph.Blazor
|   +-- Components
|   |   +-- Layout
|   |   |   +-- MainLayout.razor
|   |   |   +-- MainLayout.razor.css
|   |   |   +-- NavMenu.razor
|   |   |   +-- NavMenu.razor.css
|   |   |   +-- ReconnectModal.razor
|   |   |   +-- ReconnectModal.razor.css
|   |   |   \-- ReconnectModal.razor.js
|   |   +-- Pages
|   |   |   +-- Counter.razor
|   |   |   +-- Error.razor
|   |   |   +-- Home.razor
|   |   |   +-- NotFound.razor
|   |   |   +-- StringTreeDemo.razor
|   |   |   +-- TreeSkyDemo.razor
|   |   |   \-- Weather.razor
|   |   +-- _Imports.razor
|   |   +-- App.razor
|   |   \-- Routes.razor
|   +-- Infrastructure
|   |   \-- NonIdempotentResilience.cs
|   +-- Properties
|   |   \-- launchSettings.json
|   +-- Services
|   |   \-- DemoTree
|   |       +-- InMemoryStringTreeActionHandler.cs
|   |       +-- InMemoryStringTreeDataSource.cs
|   |       +-- InMemoryStringTreeStore.cs
|   |       \-- StringTreeClientService.cs
|   +-- wwwroot
|   |   +-- lib
|   |   |   \-- bootstrap
|   |   +-- app.css
|   |   \-- favicon.png
|   +-- appsettings.Development.json
|   +-- appsettings.json
|   +-- Program.cs
|   +-- ReadMe.md
|   \-- TreeGraph.Blazor.csproj
+-- TreeGraph.Blazor.E2E.Tests
|   +-- Base
|   |   \-- E2ETestBase.cs
|   +-- Fixtures
|   |   +-- AspireHealthCheck.cs
|   |   +-- BlazorHelpers.cs
|   |   +-- BlazorHelpers.README.md
|   |   +-- E2ESettings.cs
|   |   +-- MetadataHelpers.cs
|   |   \-- PlaywrightFixture.cs
|   +-- Tests
|   |   +-- InodeFlowTests.cs
|   |   +-- MetadataAttributesTests.cs
|   |   +-- MetadataEntityTypesTests.cs
|   |   +-- MetadataOptionSetsTests.cs
|   |   +-- MetadataUnitsTests.cs
|   |   +-- StringTreeDemoSmokeTests.cs
|   |   \-- TreeSkyDemoSmokeTests.cs
|   +-- appsettings.json
|   +-- TreeGraph.Blazor.E2E.Tests.csproj
|   \-- xunit.runner.json
+-- TreeGraph.Blazor.Shared
|   +-- Common
|   |   +-- BoolFieldSky.razor
|   |   +-- PageDialogSky.razor
|   |   \-- ProgressCircularSky.razor
|   +-- NodeEav
|   |   +-- Components
|   |   |   +-- FieldRenderers
|   |   |   +-- Shared
|   |   |   \-- DynamicForm.razor
|   |   +-- Pages
|   |   |   +-- Attributes
|   |   |   +-- CustomTable
|   |   |   +-- Entities
|   |   |   +-- Inodes
|   |   |   +-- Metadata
|   |   |   +-- Query
|   |   |   +-- Schema
|   |   |   \-- CustomTables.razor
|   |   +-- Services
|   |   |   +-- EavApiClient.cs
|   |   |   +-- EavFieldValidator.cs
|   |   |   +-- EntityTypeDisplayService.cs
|   |   |   +-- FieldValidationError.cs
|   |   |   +-- FieldValidationRules.cs
|   |   |   +-- FilterOperatorCatalog.cs
|   |   |   \-- NumericInput.cs
|   |   \-- _Imports.razor
|   +-- Trees
|   |   +-- StringTree
|   |   |   +-- Components
|   |   |   +-- Contracts
|   |   |   +-- Extensions
|   |   |   +-- Models
|   |   |   \-- Services
|   |   \-- TreeSky
|   |       +-- Attributes
|   |       +-- Contracts
|   |       +-- Dialogs
|   |       +-- Extensions
|   |       +-- Models
|   |       +-- Navigation
|   |       +-- Nodes
|   |       +-- Services
|   |       +-- TreeDialogPageSky.razor
|   |       +-- TreeHelper.cs
|   |       +-- TreeSelectDialogSky.razor
|   |       +-- TreeSky.razor
|   |       +-- TreeSky.razor.Action.cs
|   |       +-- TreeSky.razor.cs
|   |       +-- TreeSky.razor.Loading.cs
|   |       \-- TreeSky.razor.Node.cs
|   +-- wwwroot
|   +-- _Imports.razor
|   +-- README.md
|   \-- TreeGraph.Blazor.Shared.csproj
+-- TreeGraph.Blazor.Shared.Tests
|   +-- Attributes
|   |   \-- TreeRouteAttributeTests.cs
|   +-- Components
|   |   +-- StringParentSelectDialogTests.cs
|   |   +-- StringTreeSkyComponentTests.cs
|   |   \-- TreeSkyComponentTests.cs
|   +-- Extensions
|   |   +-- StringTreeServiceCollectionExtensionsTests.cs
|   |   \-- TreeSkyServiceCollectionExtensionsTests.cs
|   +-- Models
|   |   +-- StringNodeMetaTests.cs
|   |   \-- StringTreeNodeTests.cs
|   +-- Navigation
|   |   \-- TreeNavigationHistoryServiceTests.cs
|   +-- Services
|   |   +-- DefaultTreeActionHandlerTests.cs
|   |   +-- MessageServiceTests.cs
|   |   +-- NoopStringTreeActionHandlerTests.cs
|   |   +-- StringTreeDialogServiceTests.cs
|   |   \-- TreeNodeDialogServiceTests.cs
|   +-- TreeGraph.Blazor.Shared.Tests.csproj
|   +-- TreeHelperIterationTests.cs
|   \-- TreeHelperTests.cs
+-- TreeGraph.Blazor.Tests
|   +-- Components
|   |   +-- CustomTableEditorTests.cs
|   |   \-- QueryFilterBuilderTests.cs
|   +-- Services
|   |   \-- DemoTree
|   |       +-- InMemoryStringTreeActionHandlerTests.cs
|   |       +-- InMemoryStringTreeDataSourceTests.cs
|   |       \-- InMemoryStringTreeStoreTests.cs
|   +-- TestDoubles
|   |   \-- TestHttpMessageHandler.cs
|   +-- EavFieldValidatorTests.cs
|   +-- FieldValidationRulesTests.cs
|   +-- StartupTests.cs
|   \-- TreeGraph.Blazor.Tests.csproj
+-- TreeGraph.Shared
|   +-- Eav
|   |   +-- Dtos
|   |   |   +-- AttributeFilter.cs
|   |   |   +-- CustomTableDtos.cs
|   |   |   +-- EavQueryRequest.cs
|   |   |   +-- EntityDtos.cs
|   |   |   +-- EntityTypeDtos.cs
|   |   |   +-- EntityTypeSummaryDto.cs
|   |   |   +-- InodeDtos.cs
|   |   |   +-- MetadataDtos.cs
|   |   |   +-- OptionSetDtos.cs
|   |   |   +-- PagedResult.cs
|   |   |   +-- SchemaDtos.cs
|   |   |   +-- UnitDtos.cs
|   |   |   \-- ValidationModels.cs
|   |   \-- EavDataTypes.cs
|   \-- TreeGraph.Shared.csproj
+-- TreeGraphApi
|   +-- Controllers
|   |   \-- TreeController.cs
|   +-- Data
|   |   +-- Configurations
|   |   |   \-- TreeNodeEntityConfiguration.cs
|   |   +-- Seeding
|   |   |   +-- SeedNode.cs
|   |   |   +-- TreeSeedData.cs
|   |   |   \-- TreeSeeder.cs
|   |   \-- TreeDbContext.cs
|   +-- Entities
|   |   \-- TreeNodeEntity.cs
|   +-- Migrations
|   |   +-- 20260930064205_InitialCreate.cs
|   |   +-- 20260930064205_InitialCreate.Designer.cs
|   |   \-- TreeDbContextModelSnapshot.cs
|   +-- Models
|   +-- Repositories
|   |   +-- ITreeRepository.cs
|   |   \-- TreeRepository.cs
|   +-- Services
|   |   +-- ITreeService.cs
|   |   \-- TreeService.cs
|   +-- appsettings.json
|   +-- Program.cs
|   \-- TreeGraphApi.csproj
+-- WeatherApi
|   +-- Controllers
|   |   \-- WeatherForecastController.cs
|   +-- Properties
|   |   \-- launchSettings.json
|   +-- appsettings.Development.json
|   +-- appsettings.json
|   +-- Program.cs
|   +-- WeatherApi.csproj
|   +-- WeatherApi.http
|   \-- WeatherForecast.cs
+-- .gitignore
+-- APromisedLand.sln
+-- APromisedLand.sln.DotSettings
+-- aspire.config.json
+-- CONTRIBUTING.md
+-- README.md
\-- test.json
