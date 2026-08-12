using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace APromisedLand.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttributeTypes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    SystemType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttributeTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CategoryTrees",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CanHaveChildren = table.Column<bool>(type: "boolean", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    ParentId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryTrees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DateAttributeValues",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    NodeId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AttributeDefinitionId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Value = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DateAttributeValues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DateTimeAttributeValues",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    NodeId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AttributeDefinitionId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Value = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DateTimeAttributeValues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DecimalAttributeValues",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    NodeId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AttributeDefinitionId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Value = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DecimalAttributeValues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FileAttributeValues",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    NodeId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AttributeDefinitionId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Value = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    FileName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Size = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileAttributeValues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegerAttributeValues",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    NodeId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AttributeDefinitionId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Value = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegerAttributeValues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LocationAttributeValues",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    NodeId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AttributeDefinitionId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationAttributeValues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TextAttributeValues",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    NodeId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AttributeDefinitionId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TextAttributeValues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimeAttributeValues",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    NodeId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AttributeDefinitionId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Value = table.Column<TimeSpan>(type: "interval", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeAttributeValues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnitsOfMeasure",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Symbol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitsOfMeasure", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnitTrees",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Abbreviation = table.Column<string>(type: "text", nullable: false),
                    ParentId = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CanHaveChildren = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    HasChildren = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitTrees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitTrees_UnitTrees_ParentId",
                        column: x => x.ParentId,
                        principalTable: "UnitTrees",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AttributeDefinitions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AttributeTypeId = table.Column<string>(type: "character varying(36)", nullable: false),
                    Lines = table.Column<int>(type: "integer", nullable: true),
                    MaxLength = table.Column<int>(type: "integer", nullable: true),
                    Precision = table.Column<int>(type: "integer", nullable: true),
                    Scale = table.Column<int>(type: "integer", nullable: true),
                    UnitId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttributeDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttributeDefinitions_AttributeTypes_AttributeTypeId",
                        column: x => x.AttributeTypeId,
                        principalTable: "AttributeTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttributeDefinitions_UnitTrees_UnitId",
                        column: x => x.UnitId,
                        principalTable: "UnitTrees",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "AttributeTypes",
                columns: new[] { "Id", "Description", "Name", "SystemType" },
                values: new object[,]
                {
                    { "3f8f6d3a-9c2b-4d8f-9a6e-5b7c8d9e0f1a", "用于存储短文本、备注、名称等单行或多行文字信息", "文本", "文本" },
                    { "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", "用于存储整数值，如数量、计数、等级等", "整数", "整数" },
                    { "a7b8c9d0-e1f2-4a3b-4c5d-6e7f8a9b0c1d", "用于存储地理位置信息（经度、纬度）", "定位", "定位" },
                    { "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e", "用于存储带小数的数值，如价格、重量等", "小数", "小数" },
                    { "c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f", "用于存储日期（不含时间）", "日期", "日期" },
                    { "d4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a", "用于存储时间（不含日期）", "时间", "时间" },
                    { "e5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8a9b", "用于存储精确的日期和时间", "日期时间", "日期时间" },
                    { "f6a7b8c9-d0e1-4f2a-3b4c-5d6e7f8a9b0c", "用于上传和存储文件，记录文件路径、名称等", "文件", "文件" }
                });

            migrationBuilder.InsertData(
                table: "CategoryTrees",
                columns: new[] { "Id", "CanHaveChildren", "Description", "IsArchived", "Name", "ParentId", "SortOrder" },
                values: new object[,]
                {
                    { "1B16336D-FB7F-42AA-AFD2-F78388883336", false, "子分类 3.3", false, "Sample 3.3", "27EE32B0-0F30-4331-AA85-61457B7A0912", 2 },
                    { "27EE32B0-0F30-4331-AA85-61457B7A0912", false, "子分类 3", false, "Sample 3", "55705350-7071-43A4-AFAF-2F30B3CE2718", 3 },
                    { "35F02829-6490-467E-9D3E-C2EBF0EAA2B4", false, "子分类 3.3.1", false, "Sample 3.3.1", "1B16336D-FB7F-42AA-AFD2-F78388883336", 0 },
                    { "39EA6315-0A74-40F6-A096-8E15CCC98579", false, "子分类 1", false, "Sample 1", "55705350-7071-43A4-AFAF-2F30B3CE2718", 1 },
                    { "55705350-7071-43A4-AFAF-2F30B3CE2718", false, "根分类示例", false, "Sample Root", null, 0 },
                    { "5C30BACA-3C11-4677-8123-8EC2BE729667", false, "子分类 3.2", false, "Sample 3.2", "27EE32B0-0F30-4331-AA85-61457B7A0912", 1 },
                    { "8E971C0E-B99A-4931-AFD6-46E44D6ECE5A", false, "子分类 3.1", false, "Sample 3.1", "27EE32B0-0F30-4331-AA85-61457B7A0912", 0 },
                    { "975599CC-B967-4AD2-B4B8-9E00D889FB4D", false, "子分类 2.2 [Archived]", true, "Sample 2.2", "C8969ED0-C018-4FDC-AE55-C363BD95C853", 1 },
                    { "C1EBEE10-97F6-44C8-9852-2F574515BF51", false, "子分类 1.1", false, "Sample 1.1", "39EA6315-0A74-40F6-A096-8E15CCC98579", 0 },
                    { "C8969ED0-C018-4FDC-AE55-C363BD95C853", false, "子分类 2", false, "Sample 2", "55705350-7071-43A4-AFAF-2F30B3CE2718", 2 },
                    { "ED0990CF-5BB7-4C36-A3BB-3AF606AD1974", false, "子分类 2.1", false, "Sample 2.1", "C8969ED0-C018-4FDC-AE55-C363BD95C853", 0 }
                });

            migrationBuilder.InsertData(
                table: "UnitTrees",
                columns: new[] { "Id", "Abbreviation", "CanHaveChildren", "Description", "HasChildren", "Name", "ParentId", "SortOrder" },
                values: new object[] { "9AB5700C-68F2-43F3-9D7E-805E7D5C539B", "", true, "", true, "计量单位", null, 0 });

            migrationBuilder.InsertData(
                table: "UnitsOfMeasure",
                columns: new[] { "Id", "Description", "IsActive", "Name", "Symbol" },
                values: new object[,]
                {
                    { "0b5d7f9e-6a4e-4f2c-9d0e-5a6b7c8d9e00", "立方公尺", true, "立方米", "m³" },
                    { "0e1d6c3b-7f8a-42c9-9d4e-1f2a3b4c5d92", "热力学温标", true, "开尔文", "K" },
                    { "1b5e7f9a-4a6b-4c8d-8e9f-9a1c3e5b7d55", "无功功率单位", true, "乏", "var" },
                    { "1c6e8a0d-5f3d-4f1b-8c9d-6b7c8d9e0f99", "平方公尺", true, "平方米", "m²" },
                    { "2a7d9f1b-4e2c-4e0a-9b8c-7c8d9e0f1a88", null, true, "卷", "roll" },
                    { "2c4d6e8f-3f5a-4b7c-9d8e-0b2d4f6a8c44", "视在功率单位", true, "伏安", "VA" },
                    { "3b8e0d2c-3f1b-4d9f-8a7b-8d9e0f1a2b77", null, true, "套", "set" },
                    { "3d3c5d7e-2e4f-4a6b-8c7d-1c3e5a7b9d33", "电阻单位", true, "欧姆", "Ω" },
                    { "4c9f1e3b-2d0a-4c8e-9f6a-9e0f1a2b3c66", null, true, "双", "pair" },
                    { "4e2b4c6d-1d3e-4f5a-9b6c-2d4f6a8b0c22", "千赫兹", true, "千赫", "kHz" },
                    { "5d0e2f4a-1c9f-4b9c-8e5a-0f1a2b3c4d55", "听", true, "罐", "can" },
                    { "5f1a3b5c-0c2d-4e4f-8a5b-3e5f7a9b1c11", "频率单位", true, "赫兹", "Hz" },
                    { "60b0e2f4-9b1c-4d3e-9f4a-4f6a8b0c2d00", "能量单位", true, "焦耳", "J" },
                    { "6e1f3a5b-0d8e-4c8a-9d4b-1a2b3c4d5e44", null, true, "瓶", "bottle" },
                    { "70f2a4b6-9e0c-4d7a-8f3b-2a3b4c5d6e33", null, true, "包", "bag" },
                    { "71c9d1e3-8a0b-4c2d-8e3f-5e5f7a9b1c99", "千度，电能单位", true, "兆瓦时", "MWh" },
                    { "7d9b6f2e-1a3c-48f7-9b8d-5e6a1c2d3f40", "公斤", true, "千克", "kg" },
                    { "81a3c5e7-8d1f-4b6c-8e2a-3b4c5d6e7f22", null, true, "箱", "box" },
                    { "82d8c0d2-7f9a-4b1c-9d2e-6d4f6a8b0c88", "度，电能单位", true, "千瓦时", "kWh" },
                    { "8f2e6a1d-3b4c-45f7-9e1a-2c3d4e5f6a70", "摄氏温标", true, "摄氏度", "°C" },
                    { "92b4d6f8-7c0e-4a5b-9d1a-4c5d6e7f8a11", "件、只", true, "个", "pcs" },
                    { "93e7b9c1-6e8f-4a0b-8c1d-7c3e5a7b9d77", "兆瓦特", true, "兆瓦", "MW" },
                    { "9c4a7b2e-5d6e-41a8-bf3c-0d1e2f3a4b81", "华氏温标", true, "华氏度", "°F" },
                    { "a3c5e7f9-6b1d-4f4a-8c2b-5d6e7f8a9b00", "公里", true, "千米", "km" },
                    { "a3c8e0f5-6b4d-4a1e-9f2c-8d7b1e5a6f30", null, true, "克", "g" },
                    { "a4f6a8b0-5d7e-4f9a-9b0c-8b2d4f6a8c66", "千瓦特", true, "千瓦", "kW" },
                    { "b2d4f6a8-c0e2-4f1b-8a3d-1c5e7b9d2f44", null, true, "毫克", "mg" },
                    { "b4d6f8a0-5e1b-4d3c-9a2b-6c7d8e9f0a99", null, true, "毫米", "mm" },
                    { "b5e5f7a9-4c6d-4e8f-8a9b-9a1c3e5b7d55", "功率单位", true, "瓦特", "W" },
                    { "c1e3a5f7-d9b1-4e8c-9d2a-5b7c0e1f3a62", "公吨", true, "吨", "t" },
                    { "c5e7b9d1-4f0a-4c2e-8a3b-7b8d9e0f1c88", "公分", true, "厘米", "cm" },
                    { "c6d4e6f8-3b5c-4d7e-9f8a-0b2d4f6a8c44", "毫安培", true, "毫安", "mA" },
                    { "d6f8a0b2-3c4e-4a6d-8b1c-9d0e1f2a3b77", "公尺", true, "米", "m" },
                    { "d7c3d5e7-2a4b-4c6d-8e7f-1c3e5a7b9d33", "电流单位", true, "安培", "A" },
                    { "e7d9b1f3-2c4a-4e6b-8d5c-0f1a2b3c4d66", null, true, "毫升", "mL" },
                    { "e8b2c4d6-1f3a-4b5c-9d6e-2d4f6a8b0c22", "千伏特", true, "千伏", "kV" },
                    { "f8a2c4e6-1b3d-4f5c-8a9b-7d0e1f2a3b55", "公升", true, "升", "L" },
                    { "f9a1b3c5-7d0e-4a2f-8b4d-3c5e7f9a1b11", "电压单位", true, "伏特", "V" }
                });

            migrationBuilder.InsertData(
                table: "AttributeDefinitions",
                columns: new[] { "Id", "AttributeTypeId", "Lines", "MaxLength", "Name", "Precision", "Scale", "UnitId" },
                values: new object[,]
                {
                    { "a2b3c4d5-e6f7-4a8b-9c0d-1e2f3a4b5c6d", "d4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a", null, null, "开始时间", null, null, null },
                    { "a6b7c8d9-e0f1-4a2b-3c4d-5e6f7a8b9c0d", "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e", null, null, "价格", 18, 2, null },
                    { "a8b9c0d1-e2f3-4a4b-5c6d-7e8f9a0b1c2d", "a7b8c9d0-e1f2-4a3b-4c5d-6e7f8a9b0c1d", null, null, "位置坐标", null, null, null },
                    { "b1c2d3e4-f5a6-4b7c-8d9e-0f1a2b3c4d5e", "3f8f6d3a-9c2b-4d8f-9a6e-5b7c8d9e0f1a", 1, 50, "名称", null, null, null },
                    { "b3c4d5e6-f7a8-4b9c-0d1e-2f3a4b5c6d7e", "d4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a", null, null, "结束时间", null, null, null },
                    { "c2d3e4f5-a6b7-4c8d-9e0f-1a2b3c4d5e6f", "3f8f6d3a-9c2b-4d8f-9a6e-5b7c8d9e0f1a", 5, 500, "描述", null, null, null },
                    { "c4d5e6f7-a8b9-4c0d-1e2f-3a4b5c6d7e8f", "e5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8a9b", null, null, "创建时间", null, null, null },
                    { "d3e4f5a6-b7c8-4d9e-0f1a-2b3c4d5e6f7a", "3f8f6d3a-9c2b-4d8f-9a6e-5b7c8d9e0f1a", 10, 1000, "备注", null, null, null },
                    { "d5e6f7a8-b9c0-4d1e-2f3a-4b5c6d7e8f9a", "e5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8a9b", null, null, "更新时间", null, null, null },
                    { "d9e0f1a2-b3c4-4d5e-6f7a-8b9c0d1e2f3a", "c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f", null, null, "生产日期", null, null, null },
                    { "e0f1a2b3-c4d5-4e6f-7a8b-9c0d1e2f3a4b", "c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f", null, null, "试验日期", null, null, null },
                    { "e6f7a8b9-c0d1-4e2f-3a4b-5c6d7e8f9a0b", "f6a7b8c9-d0e1-4f2a-3b4c-5d6e7f8a9b0c", null, null, "附件", null, null, null },
                    { "f1a2b3c4-d5e6-4f7a-8b9c-0d1e2f3a4b5c", "c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f", null, null, "出厂日期", null, null, null },
                    { "f5a6b7c8-d9e0-4f1a-2b3c-4d5e6f7a8b9c", "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", null, null, "等级", null, null, null },
                    { "f7a8b9c0-d1e2-4f3a-4b5c-6d7e8f9a0b1c", "f6a7b8c9-d0e1-4f2a-3b4c-5d6e7f8a9b0c", null, null, "图片", null, null, null }
                });

            migrationBuilder.InsertData(
                table: "UnitTrees",
                columns: new[] { "Id", "Abbreviation", "CanHaveChildren", "Description", "HasChildren", "Name", "ParentId", "SortOrder" },
                values: new object[,]
                {
                    { "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", "", true, "长度计量单位", true, "长度", "9AB5700C-68F2-43F3-9D7E-805E7D5C539B", 1 },
                    { "a3b4c5d6-e7f8-4a9b-0c1d-2e3f4a5b6c7d", "", true, "频率计量单位", true, "频率", "9AB5700C-68F2-43F3-9D7E-805E7D5C539B", 13 },
                    { "a7b8c9d0-e1f2-4a3b-4c5d-6e7f8a9b0c1d", "", true, "功率计量单位", true, "功率", "9AB5700C-68F2-43F3-9D7E-805E7D5C539B", 7 },
                    { "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e", "", true, "质量计量单位", true, "质量", "9AB5700C-68F2-43F3-9D7E-805E7D5C539B", 2 },
                    { "b4c5d6e7-f8a9-4b0c-1d2e-3f4a5b6c7d8e", "", true, "角度计量单位", true, "角度", "9AB5700C-68F2-43F3-9D7E-805E7D5C539B", 14 },
                    { "b8c9d0e1-f2a3-4b4c-5d6e-7f8a9b0c1d2e", "", true, "面积计量单位", true, "面积", "9AB5700C-68F2-43F3-9D7E-805E7D5C539B", 8 },
                    { "c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f", "", true, "时间计量单位", true, "时间", "9AB5700C-68F2-43F3-9D7E-805E7D5C539B", 3 },
                    { "c5d6e7f8-a9b0-4c1d-2e3f-4a5b6c7d8e9f", "", true, "货币计量单位", true, "货币", "9AB5700C-68F2-43F3-9D7E-805E7D5C539B", 0 },
                    { "c9d0e1f2-a3b4-4c5d-6e7f-8a9b0c1d2e3f", "", true, "体积计量单位", true, "体积", "9AB5700C-68F2-43F3-9D7E-805E7D5C539B", 9 },
                    { "d0e1f2a3-b4c5-4d6e-7f8a-9b0c1d2e3f4a", "", true, "速度计量单位", true, "速度", "9AB5700C-68F2-43F3-9D7E-805E7D5C539B", 10 },
                    { "d4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a", "", true, "温度计量单位", true, "温度", "9AB5700C-68F2-43F3-9D7E-805E7D5C539B", 4 },
                    { "e1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a5b", "", true, "压力计量单位", true, "压力", "9AB5700C-68F2-43F3-9D7E-805E7D5C539B", 11 },
                    { "e5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8a9b", "", true, "电流计量单位", true, "电流", "9AB5700C-68F2-43F3-9D7E-805E7D5C539B", 5 },
                    { "f2a3b4c5-d6e7-4f8a-9b0c-1d2e3f4a5b6c", "", true, "能量计量单位", true, "能量", "9AB5700C-68F2-43F3-9D7E-805E7D5C539B", 12 },
                    { "f6a7b8c9-d0e1-4f2a-3b4c-5d6e7f8a9b0c", "", true, "电压计量单位", true, "电压", "9AB5700C-68F2-43F3-9D7E-805E7D5C539B", 6 },
                    { "0d7ebe17-93ae-4e4c-92f4-063a124cd181", "km²", false, "", false, "平方公里", "b8c9d0e1-f2a3-4b4c-5d6e-7f8a9b0c1d2e", 54 },
                    { "1a80ed3b-1b36-4d8b-b80b-3070dbc7979d", "kHz", false, "", false, "千赫", "a3b4c5d6-e7f8-4a9b-0c1d-2e3f4a5b6c7d", 71 },
                    { "221d3c45-911f-4e94-9c3e-c13e6f2bcc76", "Pa", false, "", false, "帕斯卡", "e1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a5b", 63 },
                    { "279a6b18-6d01-4437-b95b-0480ca7adc98", "kJ", false, "", false, "千焦", "f2a3b4c5-d6e7-4f8a-9b0c-1d2e3f4a5b6c", 68 },
                    { "2d21c35a-4251-479e-b814-060b2fc84445", "m³", false, "", false, "立方米", "c9d0e1f2-a3b4-4c5d-6e7f-8a9b0c1d2e3f", 57 },
                    { "3d9088a3-7283-4f8f-b995-b193a57a6c2a", "rad", false, "", false, "弧度", "b4c5d6e7-f8a9-4b0c-1d2e-3f4a5b6c7d8e", 74 },
                    { "405ae7a3-8a13-479d-bc1a-6f9d3c15e521", "mph", false, "", false, "英里/小时", "d0e1f2a3-b4c5-4d6e-7f8a-9b0c1d2e3f4a", 62 },
                    { "41572712-95dd-4caf-b316-e1b924bc57c3", "MHz", false, "", false, "兆赫", "a3b4c5d6-e7f8-4a9b-0c1d-2e3f4a5b6c7d", 72 },
                    { "4cbec89d-3f52-4db3-9ab0-faeeb841ffbf", "m/s", false, "", false, "米/秒", "d0e1f2a3-b4c5-4d6e-7f8a-9b0c1d2e3f4a", 60 },
                    { "5db494d7-2a9c-4ae0-86e0-d4bb0dfc7b81", "J", false, "", false, "焦耳", "f2a3b4c5-d6e7-4f8a-9b0c-1d2e3f4a5b6c", 67 },
                    { "5e880060-9410-40d7-bcb4-545ccd0c1bb6", "Hz", false, "", false, "赫兹", "a3b4c5d6-e7f8-4a9b-0c1d-2e3f4a5b6c7d", 70 },
                    { "64e918fb-ee9d-45c7-b35a-2a55f5a5fe62", "km/h", false, "", false, "千米/小时", "d0e1f2a3-b4c5-4d6e-7f8a-9b0c1d2e3f4a", 61 },
                    { "780e7a01-350d-45ec-b963-b36a996de614", "mL", false, "", false, "毫升", "c9d0e1f2-a3b4-4c5d-6e7f-8a9b0c1d2e3f", 59 },
                    { "7f0af6a9-ba1a-469c-b967-e32afe43cad2", "L", false, "", false, "升", "c9d0e1f2-a3b4-4c5d-6e7f-8a9b0c1d2e3f", 58 },
                    { "883a9940-84ec-4daa-8448-609461b984ea", "kPa", false, "", false, "千帕", "e1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a5b", 64 },
                    { "8981bd9a-bd99-4f4c-b8af-038975b799be", "bar", false, "", false, "巴", "e1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a5b", 66 },
                    { "a00be966-be2e-484e-92b6-9706494ac775", "hp", false, "", false, "马力", "a7b8c9d0-e1f2-4a3b-4c5d-6e7f8a9b0c1d", 52 },
                    { "a0e1f2a3-b4c5-4d6e-7f8a-9b0c1d2e3f4a", "mg", false, "", false, "毫克", "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e", 32 },
                    { "a2e3f4a5-b6c7-4d8e-9f0a-1b2c3d4e5f6a", "mA", false, "", false, "毫安", "e5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8a9b", 44 },
                    { "a4c312d3-023e-4d4e-b5a7-fb7fcbd55c56", "ha", false, "", false, "公顷", "b8c9d0e1-f2a3-4b4c-5d6e-7f8a9b0c1d2e", 55 },
                    { "a4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a", "mi", false, "", false, "英里", "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", 26 },
                    { "a5b6c7d8-e9f0-4a1b-2c3d-4e5f6a7b8c9d", "HKD", false, "港元", false, "港元", "c5d6e7f8-a9b0-4c1d-2e3f-4a5b6c7d8e9f", 20 },
                    { "a6e7f8a9-b0c1-4d2e-3f4a-5b6c7d8e9f0a", "h", false, "", false, "小时", "c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f", 38 },
                    { "a8e9f0a1-b2c3-4d4e-5f6a-7b8c9d0e1f2a", "kW", false, "", false, "千瓦", "a7b8c9d0-e1f2-4a3b-4c5d-6e7f8a9b0c1d", 50 },
                    { "b1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a5b", "t", false, "", false, "吨", "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e", 33 },
                    { "b3c4d5e6-f7a8-4b9c-0d1e-2f3a4b5c6d7e", "JPY", false, "日元", false, "日元", "c5d6e7f8-a9b0-4c1d-2e3f-4a5b6c7d8e9f", 19 },
                    { "b3f4a5b6-c7d8-4e9f-0a1b-2c3d4e5f6a7b", "µA", false, "", false, "微安", "e5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8a9b", 45 },
                    { "b5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8a9b", "yd", false, "", false, "码", "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", 27 },
                    { "b6c7d8e9-f0a1-4b2c-3d4e-5f6a7b8c9d0e", "AUD", false, "澳元", false, "澳元", "c5d6e7f8-a9b0-4c1d-2e3f-4a5b6c7d8e9f", 21 },
                    { "b7f8a9b0-c1d2-4e3f-4a5b-6c7d8e9f0a1b", "d", false, "", false, "天", "c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f", 39 },
                    { "b9f0a1b2-c3d4-4e5f-6a7b-8c9d0e1f2a3b", "MW", false, "", false, "兆瓦", "a7b8c9d0-e1f2-4a3b-4c5d-6e7f8a9b0c1d", 51 },
                    { "c0a1b2c3-d4e5-4f6a-7b8c-9d0e1f2a3b4c", "m", false, "", false, "米", "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", 22 },
                    { "c2a3b4c5-d6e7-4f8a-9b0c-1d2e3f4a5b6c", "lb", false, "", false, "磅", "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e", 34 },
                    { "c4a5b6c7-d8e9-4f0a-1b2c-3d4e5f6a7b8c", "V", false, "", false, "伏特", "f6a7b8c9-d0e1-4f2a-3b4c-5d6e7f8a9b0c", 46 },
                    { "c4d5e6f7-a8b9-4c0d-1e2f-3a4b5c6d7e8f", "GBP", false, "英镑", false, "英镑", "c5d6e7f8-a9b0-4c1d-2e3f-4a5b6c7d8e9f", 18 },
                    { "c6a7b8c9-d0e1-4f2a-3b4c-5d6e7f8a9b0c", "ft", false, "", false, "英尺", "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", 28 },
                    { "c8a9b0c1-d2e3-4f4a-5b6c-7d8e9f0a1b2c", "°C", false, "", false, "摄氏度", "d4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a", 40 },
                    { "d1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", "km", false, "", false, "千米", "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", 23 },
                    { "d3b4c5d6-e7f8-4a9b-0c1d-2e3f4a5b6c7d", "oz", false, "", false, "盎司", "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e", 35 },
                    { "d5306eb8-324f-4088-a867-6fbc7141fd59", "MPa", false, "", false, "兆帕", "e1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a5b", 65 },
                    { "d5b6c7d8-e9f0-4a1b-2c3d-4e5f6a7b8c9d", "kV", false, "", false, "千伏", "f6a7b8c9-d0e1-4f2a-3b4c-5d6e7f8a9b0c", 47 },
                    { "d5c6d7e8-f9a0-4b1c-2d3e-4f5a6b7c8d9e", "EUR", false, "欧元", false, "欧元", "c5d6e7f8-a9b0-4c1d-2e3f-4a5b6c7d8e9f", 17 },
                    { "d7b8c9d0-e1f2-4a3b-4c5d-6e7f8a9b0c1d", "in", false, "", false, "英寸", "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", 29 },
                    { "d9b0c1d2-e3f4-4a5b-6c7d-8e9f0a1b2c3d", "°F", false, "", false, "华氏度", "d4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a", 41 },
                    { "e0c1d2e3-f4a5-4b6c-7d8e-9f0a1b2c3d4e", "K", false, "", false, "开尔文", "d4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a", 42 },
                    { "e2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e", "cm", false, "", false, "厘米", "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", 24 },
                    { "e3c1f025-3b2b-461a-a5a1-015cb4e3fe38", "kWh", false, "", false, "千瓦时", "f2a3b4c5-d6e7-4f8a-9b0c-1d2e3f4a5b6c", 69 },
                    { "e4c5d6e7-f8a9-4b0c-1d2e-3f4a5b6c7d8e", "s", false, "", false, "秒", "c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f", 36 },
                    { "e6b7c8d9-f0a1-4b2c-3d4e-5f6a7b8c9d0e", "USD", false, "美元", false, "美元", "c5d6e7f8-a9b0-4c1d-2e3f-4a5b6c7d8e9f", 16 },
                    { "e6c7d8e9-f0a1-4b2c-3d4e-5f6a7b8c9d0e", "mV", false, "", false, "毫伏", "f6a7b8c9-d0e1-4f2a-3b4c-5d6e7f8a9b0c", 48 },
                    { "e88b04db-40ac-4bb7-b420-1f3b37180673", "m²", false, "", false, "平方米", "b8c9d0e1-f2a3-4b4c-5d6e-7f8a9b0c1d2e", 53 },
                    { "e8c9d0e1-f2a3-4b4c-5d6e-7f8a9b0c1d2e", "kg", false, "", false, "千克", "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e", 30 },
                    { "ed1b66d2-454b-453b-9d43-12605dffa456", "°", false, "", false, "度", "b4c5d6e7-f8a9-4b0c-1d2e-3f4a5b6c7d8e", 73 },
                    { "f1d2e3f4-a5b6-4c7d-8e9f-0a1b2c3d4e5f", "A", false, "", false, "安培", "e5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8a9b", 43 },
                    { "f3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f", "mm", false, "", false, "毫米", "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", 25 },
                    { "f5d6e7f8-a9b0-4c1d-2e3f-4a5b6c7d8e9f", "min", false, "", false, "分钟", "c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f", 37 },
                    { "f7a8b9c0-d1e2-4f3a-4b5c-6d7e8f9a0b1c", "CNY", false, "人民币", false, "元", "c5d6e7f8-a9b0-4c1d-2e3f-4a5b6c7d8e9f", 15 },
                    { "f7d8e9f0-a1b2-4c3d-4e5f-6a7b8c9d0e1f", "W", false, "", false, "瓦特", "a7b8c9d0-e1f2-4a3b-4c5d-6e7f8a9b0c1d", 49 },
                    { "f9d0e1f2-a3b4-4c5d-6e7f-8a9b0c1d2e3f", "g", false, "", false, "克", "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e", 31 },
                    { "fefa26a5-d608-411c-b637-469a886e558c", "亩", false, "", false, "亩", "b8c9d0e1-f2a3-4b4c-5d6e-7f8a9b0c1d2e", 56 }
                });

            migrationBuilder.InsertData(
                table: "AttributeDefinitions",
                columns: new[] { "Id", "AttributeTypeId", "Lines", "MaxLength", "Name", "Precision", "Scale", "UnitId" },
                values: new object[,]
                {
                    { "b7c8d9e0-f1a2-4b3c-4d5e-6f7a8b9c0d1e", "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e", null, null, "重量", 10, 3, "e8c9d0e1-f2a3-4b4c-5d6e-7f8a9b0c1d2e" },
                    { "c8d9e0f1-a2b3-4c4d-5e6f-7a8b9c0d1e2f", "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e", null, null, "长度", 8, 2, "e2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e" },
                    { "e4f5a6b7-c8d9-4e0f-1a2b-3c4d5e6f7a8b", "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", null, null, "数量", null, null, "c0a1b2c3-d4e5-4f6a-7b8c-9d0e1f2a3b4c" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttributeDefinitions_AttributeTypeId",
                table: "AttributeDefinitions",
                column: "AttributeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AttributeDefinitions_UnitId",
                table: "AttributeDefinitions",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryTrees_ParentId",
                table: "CategoryTrees",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitsOfMeasure_Name",
                table: "UnitsOfMeasure",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnitTrees_ParentId",
                table: "UnitTrees",
                column: "ParentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttributeDefinitions");

            migrationBuilder.DropTable(
                name: "CategoryTrees");

            migrationBuilder.DropTable(
                name: "DateAttributeValues");

            migrationBuilder.DropTable(
                name: "DateTimeAttributeValues");

            migrationBuilder.DropTable(
                name: "DecimalAttributeValues");

            migrationBuilder.DropTable(
                name: "FileAttributeValues");

            migrationBuilder.DropTable(
                name: "IntegerAttributeValues");

            migrationBuilder.DropTable(
                name: "LocationAttributeValues");

            migrationBuilder.DropTable(
                name: "TextAttributeValues");

            migrationBuilder.DropTable(
                name: "TimeAttributeValues");

            migrationBuilder.DropTable(
                name: "UnitsOfMeasure");

            migrationBuilder.DropTable(
                name: "AttributeTypes");

            migrationBuilder.DropTable(
                name: "UnitTrees");
        }
    }
}
