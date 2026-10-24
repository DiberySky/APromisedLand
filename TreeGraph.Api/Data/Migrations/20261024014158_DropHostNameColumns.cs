using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TreeGraph.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropHostNameColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "nodes",
                columns: new[] { "id", "created_at", "description", "host_type", "name", "updated_at" },
                values: new object[,]
                {
                    { "0d7ebe17-93ae-4e4c-92f4-063a124cd181", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "平方公里", null },
                    { "1a80ed3b-1b36-4d8b-b80b-3070dbc7979d", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "千赫", null },
                    { "221d3c45-911f-4e94-9c3e-c13e6f2bcc76", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "帕斯卡", null },
                    { "279a6b18-6d01-4437-b95b-0480ca7adc98", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "千焦", null },
                    { "2d21c35a-4251-479e-b814-060b2fc84445", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "立方米", null },
                    { "3d9088a3-7283-4f8f-b995-b193a57a6c2a", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "弧度", null },
                    { "405ae7a3-8a13-479d-bc1a-6f9d3c15e521", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "英里/小时", null },
                    { "41572712-95dd-4caf-b316-e1b924bc57c3", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "兆赫", null },
                    { "4cbec89d-3f52-4db3-9ab0-faeeb841ffbf", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "米/秒", null },
                    { "5db494d7-2a9c-4ae0-86e0-d4bb0dfc7b81", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "焦耳", null },
                    { "5e880060-9410-40d7-bcb4-545ccd0c1bb6", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "赫兹", null },
                    { "64e918fb-ee9d-45c7-b35a-2a55f5a5fe62", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "千米/小时", null },
                    { "780e7a01-350d-45ec-b963-b36a996de614", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "毫升", null },
                    { "7f0af6a9-ba1a-469c-b967-e32afe43cad2", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "升", null },
                    { "883a9940-84ec-4daa-8448-609461b984ea", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "千帕", null },
                    { "8981bd9a-bd99-4f4c-b8af-038975b799be", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "巴", null },
                    { "9AB5700C-68F2-43F3-9D7E-805E7D5C539B", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "计量单位", null },
                    { "a00be966-be2e-484e-92b6-9706494ac775", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "马力", null },
                    { "a0e1f2a3-b4c5-4d6e-7f8a-9b0c1d2e3f4a", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "毫克", null },
                    { "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "长度", null },
                    { "a2e3f4a5-b6c7-4d8e-9f0a-1b2c3d4e5f6a", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "毫安", null },
                    { "a3b4c5d6-e7f8-4a9b-0c1d-2e3f4a5b6c7d", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "频率", null },
                    { "a4c312d3-023e-4d4e-b5a7-fb7fcbd55c56", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "公顷", null },
                    { "a4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "英里", null },
                    { "a5b6c7d8-e9f0-4a1b-2c3d-4e5f6a7b8c9d", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "港元", null },
                    { "a6e7f8a9-b0c1-4d2e-3f4a-5b6c7d8e9f0a", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "小时", null },
                    { "a7b8c9d0-e1f2-4a3b-4c5d-6e7f8a9b0c1d", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "功率", null },
                    { "a8e9f0a1-b2c3-4d4e-5f6a-7b8c9d0e1f2a", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "千瓦", null },
                    { "b1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a5b", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "吨", null },
                    { "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "质量", null },
                    { "b3c4d5e6-f7a8-4b9c-0d1e-2f3a4b5c6d7e", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "日元", null },
                    { "b3f4a5b6-c7d8-4e9f-0a1b-2c3d4e5f6a7b", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "微安", null },
                    { "b4c5d6e7-f8a9-4b0c-1d2e-3f4a5b6c7d8e", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "角度", null },
                    { "b5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8a9b", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "码", null },
                    { "b6c7d8e9-f0a1-4b2c-3d4e-5f6a7b8c9d0e", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "澳元", null },
                    { "b7f8a9b0-c1d2-4e3f-4a5b-6c7d8e9f0a1b", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "天", null },
                    { "b8c9d0e1-f2a3-4b4c-5d6e-7f8a9b0c1d2e", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "面积", null },
                    { "b9f0a1b2-c3d4-4e5f-6a7b-8c9d0e1f2a3b", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "兆瓦", null },
                    { "beef0000-0000-4000-8000-000000000001", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "KnowledgeTree", "知识库", null },
                    { "c0a1b2c3-d4e5-4f6a-7b8c-9d0e1f2a3b4c", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "米", null },
                    { "c2a3b4c5-d6e7-4f8a-9b0c-1d2e3f4a5b6c", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "磅", null },
                    { "c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "时间", null },
                    { "c4a5b6c7-d8e9-4f0a-1b2c-3d4e5f6a7b8c", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "伏特", null },
                    { "c4d5e6f7-a8b9-4c0d-1e2f-3a4b5c6d7e8f", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "英镑", null },
                    { "c5d6e7f8-a9b0-4c1d-2e3f-4a5b6c7d8e9f", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "货币", null },
                    { "c6a7b8c9-d0e1-4f2a-3b4c-5d6e7f8a9b0c", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "英尺", null },
                    { "c8a9b0c1-d2e3-4f4a-5b6c-7d8e9f0a1b2c", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "摄氏度", null },
                    { "c9d0e1f2-a3b4-4c5d-6e7f-8a9b0c1d2e3f", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "体积", null },
                    { "d0000000-0000-4000-8000-000000000001", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "CategoryTree", "项目类型", null },
                    { "d0e1f2a3-b4c5-4d6e-7f8a-9b0c1d2e3f4a", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "速度", null },
                    { "d1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "千米", null },
                    { "d3b4c5d6-e7f8-4a9b-0c1d-2e3f4a5b6c7d", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "盎司", null },
                    { "d4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "温度", null },
                    { "d5306eb8-324f-4088-a867-6fbc7141fd59", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "兆帕", null },
                    { "d5b6c7d8-e9f0-4a1b-2c3d-4e5f6a7b8c9d", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "千伏", null },
                    { "d5c6d7e8-f9a0-4b1c-2d3e-4f5a6b7c8d9e", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "欧元", null },
                    { "d7b8c9d0-e1f2-4a3b-4c5d-6e7f8a9b0c1d", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "英寸", null },
                    { "d9b0c1d2-e3f4-4a5b-6c7d-8e9f0a1b2c3d", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "华氏度", null },
                    { "e0c1d2e3-f4a5-4b6c-7d8e-9f0a1b2c3d4e", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "开尔文", null },
                    { "e1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a5b", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "压力", null },
                    { "e2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "厘米", null },
                    { "e3c1f025-3b2b-461a-a5a1-015cb4e3fe38", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "千瓦时", null },
                    { "e4c5d6e7-f8a9-4b0c-1d2e-3f4a5b6c7d8e", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "秒", null },
                    { "e5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8a9b", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "电流", null },
                    { "e6b7c8d9-f0a1-4b2c-3d4e-5f6a7b8c9d0e", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "美元", null },
                    { "e6c7d8e9-f0a1-4b2c-3d4e-5f6a7b8c9d0e", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "毫伏", null },
                    { "e88b04db-40ac-4bb7-b420-1f3b37180673", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "平方米", null },
                    { "e8c9d0e1-f2a3-4b4c-5d6e-7f8a9b0c1d2e", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "千克", null },
                    { "ed1b66d2-454b-453b-9d43-12605dffa456", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "度", null },
                    { "f1d2e3f4-a5b6-4c7d-8e9f-0a1b2c3d4e5f", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "安培", null },
                    { "f2a3b4c5-d6e7-4f8a-9b0c-1d2e3f4a5b6c", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "能量", null },
                    { "f3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "毫米", null },
                    { "f5d6e7f8-a9b0-4c1d-2e3f-4a5b6c7d8e9f", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "分钟", null },
                    { "f6a7b8c9-d0e1-4f2a-3b4c-5d6e7f8a9b0c", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "电压", null },
                    { "f7a8b9c0-d1e2-4f3a-4b5c-6d7e8f9a0b1c", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "元", null },
                    { "f7d8e9f0-a1b2-4c3d-4e5f-6a7b8c9d0e1f", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "瓦特", null },
                    { "f9d0e1f2-a3b4-4c5d-6e7f-8a9b0c1d2e3f", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "克", null },
                    { "fefa26a5-d608-411c-b637-469a886e558c", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnitTree", "亩", null }
                });

            // R2 Name 收编：把五张宿主表现有 name 回填到 nodes 注册表（SpaceTree/LineNode 无 EF 种子，必须靠此回填）。
            // ON CONFLICT (id) DO NOTHING：跳过已由上方 InsertData 预置的种子行。
            migrationBuilder.Sql(
                "INSERT INTO nodes (id, host_type, name, created_at) " +
                "SELECT id, 'UnitTree', COALESCE(name, ''), now() FROM unit_trees " +
                "ON CONFLICT (id) DO NOTHING;");
            migrationBuilder.Sql(
                "INSERT INTO nodes (id, host_type, name, created_at) " +
                "SELECT id, 'SpaceTree', COALESCE(name, ''), now() FROM space_trees " +
                "ON CONFLICT (id) DO NOTHING;");
            migrationBuilder.Sql(
                "INSERT INTO nodes (id, host_type, name, created_at) " +
                "SELECT id, 'CategoryTree', COALESCE(name, ''), now() FROM category_trees " +
                "ON CONFLICT (id) DO NOTHING;");
            migrationBuilder.Sql(
                "INSERT INTO nodes (id, host_type, name, created_at) " +
                "SELECT id, 'KnowledgeTree', COALESCE(name, ''), now() FROM knowledge_trees " +
                "ON CONFLICT (id) DO NOTHING;");
            migrationBuilder.Sql(
                "INSERT INTO nodes (id, host_type, name, created_at) " +
                "SELECT id, 'LineNode', COALESCE(name, ''), now() FROM line_nodes " +
                "ON CONFLICT (id) DO NOTHING;");

            migrationBuilder.DropColumn(
                name: "name",
                table: "unit_trees");

            migrationBuilder.DropColumn(
                name: "name",
                table: "space_trees");

            migrationBuilder.DropColumn(
                name: "name",
                table: "line_nodes");

            migrationBuilder.DropColumn(
                name: "name",
                table: "knowledge_trees");

            migrationBuilder.DropColumn(
                name: "name",
                table: "category_trees");

            migrationBuilder.AddForeignKey(
                name: "FK_category_trees_nodes_id",
                table: "category_trees",
                column: "id",
                principalTable: "nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_knowledge_trees_nodes_id",
                table: "knowledge_trees",
                column: "id",
                principalTable: "nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_line_nodes_nodes_id",
                table: "line_nodes",
                column: "id",
                principalTable: "nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_space_trees_nodes_id",
                table: "space_trees",
                column: "id",
                principalTable: "nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_unit_trees_nodes_id",
                table: "unit_trees",
                column: "id",
                principalTable: "nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_category_trees_nodes_id",
                table: "category_trees");

            migrationBuilder.DropForeignKey(
                name: "FK_knowledge_trees_nodes_id",
                table: "knowledge_trees");

            migrationBuilder.DropForeignKey(
                name: "FK_line_nodes_nodes_id",
                table: "line_nodes");

            migrationBuilder.DropForeignKey(
                name: "FK_space_trees_nodes_id",
                table: "space_trees");

            migrationBuilder.DropForeignKey(
                name: "FK_unit_trees_nodes_id",
                table: "unit_trees");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "0d7ebe17-93ae-4e4c-92f4-063a124cd181");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "1a80ed3b-1b36-4d8b-b80b-3070dbc7979d");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "221d3c45-911f-4e94-9c3e-c13e6f2bcc76");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "279a6b18-6d01-4437-b95b-0480ca7adc98");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "2d21c35a-4251-479e-b814-060b2fc84445");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "3d9088a3-7283-4f8f-b995-b193a57a6c2a");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "405ae7a3-8a13-479d-bc1a-6f9d3c15e521");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "41572712-95dd-4caf-b316-e1b924bc57c3");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "4cbec89d-3f52-4db3-9ab0-faeeb841ffbf");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "5db494d7-2a9c-4ae0-86e0-d4bb0dfc7b81");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "5e880060-9410-40d7-bcb4-545ccd0c1bb6");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "64e918fb-ee9d-45c7-b35a-2a55f5a5fe62");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "780e7a01-350d-45ec-b963-b36a996de614");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "7f0af6a9-ba1a-469c-b967-e32afe43cad2");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "883a9940-84ec-4daa-8448-609461b984ea");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "8981bd9a-bd99-4f4c-b8af-038975b799be");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "9AB5700C-68F2-43F3-9D7E-805E7D5C539B");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "a00be966-be2e-484e-92b6-9706494ac775");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "a0e1f2a3-b4c5-4d6e-7f8a-9b0c1d2e3f4a");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "a2e3f4a5-b6c7-4d8e-9f0a-1b2c3d4e5f6a");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "a3b4c5d6-e7f8-4a9b-0c1d-2e3f4a5b6c7d");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "a4c312d3-023e-4d4e-b5a7-fb7fcbd55c56");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "a4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "a5b6c7d8-e9f0-4a1b-2c3d-4e5f6a7b8c9d");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "a6e7f8a9-b0c1-4d2e-3f4a-5b6c7d8e9f0a");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "a7b8c9d0-e1f2-4a3b-4c5d-6e7f8a9b0c1d");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "a8e9f0a1-b2c3-4d4e-5f6a-7b8c9d0e1f2a");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "b1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a5b");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "b3c4d5e6-f7a8-4b9c-0d1e-2f3a4b5c6d7e");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "b3f4a5b6-c7d8-4e9f-0a1b-2c3d4e5f6a7b");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "b4c5d6e7-f8a9-4b0c-1d2e-3f4a5b6c7d8e");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "b5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8a9b");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "b6c7d8e9-f0a1-4b2c-3d4e-5f6a7b8c9d0e");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "b7f8a9b0-c1d2-4e3f-4a5b-6c7d8e9f0a1b");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "b8c9d0e1-f2a3-4b4c-5d6e-7f8a9b0c1d2e");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "b9f0a1b2-c3d4-4e5f-6a7b-8c9d0e1f2a3b");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "beef0000-0000-4000-8000-000000000001");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "c0a1b2c3-d4e5-4f6a-7b8c-9d0e1f2a3b4c");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "c2a3b4c5-d6e7-4f8a-9b0c-1d2e3f4a5b6c");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "c4a5b6c7-d8e9-4f0a-1b2c-3d4e5f6a7b8c");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "c4d5e6f7-a8b9-4c0d-1e2f-3a4b5c6d7e8f");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "c5d6e7f8-a9b0-4c1d-2e3f-4a5b6c7d8e9f");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "c6a7b8c9-d0e1-4f2a-3b4c-5d6e7f8a9b0c");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "c8a9b0c1-d2e3-4f4a-5b6c-7d8e9f0a1b2c");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "c9d0e1f2-a3b4-4c5d-6e7f-8a9b0c1d2e3f");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "d0000000-0000-4000-8000-000000000001");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "d0e1f2a3-b4c5-4d6e-7f8a-9b0c1d2e3f4a");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "d1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "d3b4c5d6-e7f8-4a9b-0c1d-2e3f4a5b6c7d");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "d4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "d5306eb8-324f-4088-a867-6fbc7141fd59");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "d5b6c7d8-e9f0-4a1b-2c3d-4e5f6a7b8c9d");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "d5c6d7e8-f9a0-4b1c-2d3e-4f5a6b7c8d9e");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "d7b8c9d0-e1f2-4a3b-4c5d-6e7f8a9b0c1d");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "d9b0c1d2-e3f4-4a5b-6c7d-8e9f0a1b2c3d");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "e0c1d2e3-f4a5-4b6c-7d8e-9f0a1b2c3d4e");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "e1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a5b");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "e2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "e3c1f025-3b2b-461a-a5a1-015cb4e3fe38");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "e4c5d6e7-f8a9-4b0c-1d2e-3f4a5b6c7d8e");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "e5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8a9b");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "e6b7c8d9-f0a1-4b2c-3d4e-5f6a7b8c9d0e");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "e6c7d8e9-f0a1-4b2c-3d4e-5f6a7b8c9d0e");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "e88b04db-40ac-4bb7-b420-1f3b37180673");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "e8c9d0e1-f2a3-4b4c-5d6e-7f8a9b0c1d2e");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "ed1b66d2-454b-453b-9d43-12605dffa456");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "f1d2e3f4-a5b6-4c7d-8e9f-0a1b2c3d4e5f");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "f2a3b4c5-d6e7-4f8a-9b0c-1d2e3f4a5b6c");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "f3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "f5d6e7f8-a9b0-4c1d-2e3f-4a5b6c7d8e9f");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "f6a7b8c9-d0e1-4f2a-3b4c-5d6e7f8a9b0c");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "f7a8b9c0-d1e2-4f3a-4b5c-6d7e8f9a0b1c");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "f7d8e9f0-a1b2-4c3d-4e5f-6a7b8c9d0e1f");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "f9d0e1f2-a3b4-4c5d-6e7f-8a9b0c1d2e3f");

            migrationBuilder.DeleteData(
                table: "nodes",
                keyColumn: "id",
                keyValue: "fefa26a5-d608-411c-b637-469a886e558c");

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "unit_trees",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "space_trees",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "line_nodes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "knowledge_trees",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "category_trees",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "category_trees",
                keyColumn: "id",
                keyValue: "d0000000-0000-4000-8000-000000000001",
                column: "name",
                value: "项目类型");

            migrationBuilder.UpdateData(
                table: "knowledge_trees",
                keyColumn: "id",
                keyValue: "beef0000-0000-4000-8000-000000000001",
                column: "name",
                value: "知识库");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "0d7ebe17-93ae-4e4c-92f4-063a124cd181",
                column: "name",
                value: "平方公里");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "1a80ed3b-1b36-4d8b-b80b-3070dbc7979d",
                column: "name",
                value: "千赫");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "221d3c45-911f-4e94-9c3e-c13e6f2bcc76",
                column: "name",
                value: "帕斯卡");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "279a6b18-6d01-4437-b95b-0480ca7adc98",
                column: "name",
                value: "千焦");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "2d21c35a-4251-479e-b814-060b2fc84445",
                column: "name",
                value: "立方米");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "3d9088a3-7283-4f8f-b995-b193a57a6c2a",
                column: "name",
                value: "弧度");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "405ae7a3-8a13-479d-bc1a-6f9d3c15e521",
                column: "name",
                value: "英里/小时");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "41572712-95dd-4caf-b316-e1b924bc57c3",
                column: "name",
                value: "兆赫");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "4cbec89d-3f52-4db3-9ab0-faeeb841ffbf",
                column: "name",
                value: "米/秒");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "5db494d7-2a9c-4ae0-86e0-d4bb0dfc7b81",
                column: "name",
                value: "焦耳");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "5e880060-9410-40d7-bcb4-545ccd0c1bb6",
                column: "name",
                value: "赫兹");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "64e918fb-ee9d-45c7-b35a-2a55f5a5fe62",
                column: "name",
                value: "千米/小时");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "780e7a01-350d-45ec-b963-b36a996de614",
                column: "name",
                value: "毫升");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "7f0af6a9-ba1a-469c-b967-e32afe43cad2",
                column: "name",
                value: "升");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "883a9940-84ec-4daa-8448-609461b984ea",
                column: "name",
                value: "千帕");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "8981bd9a-bd99-4f4c-b8af-038975b799be",
                column: "name",
                value: "巴");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "9AB5700C-68F2-43F3-9D7E-805E7D5C539B",
                column: "name",
                value: "计量单位");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "a00be966-be2e-484e-92b6-9706494ac775",
                column: "name",
                value: "马力");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "a0e1f2a3-b4c5-4d6e-7f8a-9b0c1d2e3f4a",
                column: "name",
                value: "毫克");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d",
                column: "name",
                value: "长度");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "a2e3f4a5-b6c7-4d8e-9f0a-1b2c3d4e5f6a",
                column: "name",
                value: "毫安");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "a3b4c5d6-e7f8-4a9b-0c1d-2e3f4a5b6c7d",
                column: "name",
                value: "频率");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "a4c312d3-023e-4d4e-b5a7-fb7fcbd55c56",
                column: "name",
                value: "公顷");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "a4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a",
                column: "name",
                value: "英里");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "a5b6c7d8-e9f0-4a1b-2c3d-4e5f6a7b8c9d",
                column: "name",
                value: "港元");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "a6e7f8a9-b0c1-4d2e-3f4a-5b6c7d8e9f0a",
                column: "name",
                value: "小时");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "a7b8c9d0-e1f2-4a3b-4c5d-6e7f8a9b0c1d",
                column: "name",
                value: "功率");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "a8e9f0a1-b2c3-4d4e-5f6a-7b8c9d0e1f2a",
                column: "name",
                value: "千瓦");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "b1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a5b",
                column: "name",
                value: "吨");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e",
                column: "name",
                value: "质量");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "b3c4d5e6-f7a8-4b9c-0d1e-2f3a4b5c6d7e",
                column: "name",
                value: "日元");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "b3f4a5b6-c7d8-4e9f-0a1b-2c3d4e5f6a7b",
                column: "name",
                value: "微安");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "b4c5d6e7-f8a9-4b0c-1d2e-3f4a5b6c7d8e",
                column: "name",
                value: "角度");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "b5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8a9b",
                column: "name",
                value: "码");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "b6c7d8e9-f0a1-4b2c-3d4e-5f6a7b8c9d0e",
                column: "name",
                value: "澳元");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "b7f8a9b0-c1d2-4e3f-4a5b-6c7d8e9f0a1b",
                column: "name",
                value: "天");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "b8c9d0e1-f2a3-4b4c-5d6e-7f8a9b0c1d2e",
                column: "name",
                value: "面积");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "b9f0a1b2-c3d4-4e5f-6a7b-8c9d0e1f2a3b",
                column: "name",
                value: "兆瓦");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "c0a1b2c3-d4e5-4f6a-7b8c-9d0e1f2a3b4c",
                column: "name",
                value: "米");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "c2a3b4c5-d6e7-4f8a-9b0c-1d2e3f4a5b6c",
                column: "name",
                value: "磅");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f",
                column: "name",
                value: "时间");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "c4a5b6c7-d8e9-4f0a-1b2c-3d4e5f6a7b8c",
                column: "name",
                value: "伏特");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "c4d5e6f7-a8b9-4c0d-1e2f-3a4b5c6d7e8f",
                column: "name",
                value: "英镑");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "c5d6e7f8-a9b0-4c1d-2e3f-4a5b6c7d8e9f",
                column: "name",
                value: "货币");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "c6a7b8c9-d0e1-4f2a-3b4c-5d6e7f8a9b0c",
                column: "name",
                value: "英尺");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "c8a9b0c1-d2e3-4f4a-5b6c-7d8e9f0a1b2c",
                column: "name",
                value: "摄氏度");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "c9d0e1f2-a3b4-4c5d-6e7f-8a9b0c1d2e3f",
                column: "name",
                value: "体积");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "d0e1f2a3-b4c5-4d6e-7f8a-9b0c1d2e3f4a",
                column: "name",
                value: "速度");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "d1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d",
                column: "name",
                value: "千米");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "d3b4c5d6-e7f8-4a9b-0c1d-2e3f4a5b6c7d",
                column: "name",
                value: "盎司");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "d4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a",
                column: "name",
                value: "温度");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "d5306eb8-324f-4088-a867-6fbc7141fd59",
                column: "name",
                value: "兆帕");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "d5b6c7d8-e9f0-4a1b-2c3d-4e5f6a7b8c9d",
                column: "name",
                value: "千伏");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "d5c6d7e8-f9a0-4b1c-2d3e-4f5a6b7c8d9e",
                column: "name",
                value: "欧元");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "d7b8c9d0-e1f2-4a3b-4c5d-6e7f8a9b0c1d",
                column: "name",
                value: "英寸");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "d9b0c1d2-e3f4-4a5b-6c7d-8e9f0a1b2c3d",
                column: "name",
                value: "华氏度");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "e0c1d2e3-f4a5-4b6c-7d8e-9f0a1b2c3d4e",
                column: "name",
                value: "开尔文");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "e1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a5b",
                column: "name",
                value: "压力");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "e2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e",
                column: "name",
                value: "厘米");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "e3c1f025-3b2b-461a-a5a1-015cb4e3fe38",
                column: "name",
                value: "千瓦时");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "e4c5d6e7-f8a9-4b0c-1d2e-3f4a5b6c7d8e",
                column: "name",
                value: "秒");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "e5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8a9b",
                column: "name",
                value: "电流");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "e6b7c8d9-f0a1-4b2c-3d4e-5f6a7b8c9d0e",
                column: "name",
                value: "美元");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "e6c7d8e9-f0a1-4b2c-3d4e-5f6a7b8c9d0e",
                column: "name",
                value: "毫伏");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "e88b04db-40ac-4bb7-b420-1f3b37180673",
                column: "name",
                value: "平方米");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "e8c9d0e1-f2a3-4b4c-5d6e-7f8a9b0c1d2e",
                column: "name",
                value: "千克");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "ed1b66d2-454b-453b-9d43-12605dffa456",
                column: "name",
                value: "度");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "f1d2e3f4-a5b6-4c7d-8e9f-0a1b2c3d4e5f",
                column: "name",
                value: "安培");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "f2a3b4c5-d6e7-4f8a-9b0c-1d2e3f4a5b6c",
                column: "name",
                value: "能量");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "f3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f",
                column: "name",
                value: "毫米");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "f5d6e7f8-a9b0-4c1d-2e3f-4a5b6c7d8e9f",
                column: "name",
                value: "分钟");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "f6a7b8c9-d0e1-4f2a-3b4c-5d6e7f8a9b0c",
                column: "name",
                value: "电压");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "f7a8b9c0-d1e2-4f3a-4b5c-6d7e8f9a0b1c",
                column: "name",
                value: "元");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "f7d8e9f0-a1b2-4c3d-4e5f-6a7b8c9d0e1f",
                column: "name",
                value: "瓦特");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "f9d0e1f2-a3b4-4c5d-6e7f-8a9b0c1d2e3f",
                column: "name",
                value: "克");

            migrationBuilder.UpdateData(
                table: "unit_trees",
                keyColumn: "id",
                keyValue: "fefa26a5-d608-411c-b637-469a886e558c",
                column: "name",
                value: "亩");

            // R2 回滚：从 nodes 回填宿主 name 列（UpdateData 仅覆盖种子行，其余行需回填）。
            migrationBuilder.Sql(
                "UPDATE unit_trees SET name = COALESCE((SELECT name FROM nodes WHERE nodes.id = unit_trees.id), '') WHERE name = '';");
            migrationBuilder.Sql(
                "UPDATE space_trees SET name = COALESCE((SELECT name FROM nodes WHERE nodes.id = space_trees.id), '') WHERE name = '';");
            migrationBuilder.Sql(
                "UPDATE category_trees SET name = COALESCE((SELECT name FROM nodes WHERE nodes.id = category_trees.id), '') WHERE name = '';");
            migrationBuilder.Sql(
                "UPDATE knowledge_trees SET name = COALESCE((SELECT name FROM nodes WHERE nodes.id = knowledge_trees.id), '') WHERE name = '';");
            migrationBuilder.Sql(
                "UPDATE line_nodes SET name = COALESCE((SELECT name FROM nodes WHERE nodes.id = line_nodes.id), '') WHERE name = '';");
        }
    }
}
