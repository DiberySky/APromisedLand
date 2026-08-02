using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

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
            migrationBuilder.CreateSequence(
                name: "AttributeValueBaseSequence");

            migrationBuilder.CreateTable(
                name: "AttributeTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
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
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    ParentId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryTrees", x => x.Id);
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
                name: "AttributeDefinitions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AttributeTypeId = table.Column<int>(type: "integer", nullable: false),
                    Lines = table.Column<int>(type: "integer", nullable: true),
                    Precision = table.Column<int>(type: "integer", nullable: true),
                    Scale = table.Column<int>(type: "integer", nullable: true),
                    UnitOfMeasureId = table.Column<string>(type: "character varying(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttributeDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttributeDefinitions_AttributeTypes_AttributeTypeId",
                        column: x => x.AttributeTypeId,
                        principalTable: "AttributeTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttributeDefinitions_UnitsOfMeasure_UnitOfMeasureId",
                        column: x => x.UnitOfMeasureId,
                        principalTable: "UnitsOfMeasure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DateAttributeValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false, defaultValueSql: "nextval('\"AttributeValueBaseSequence\"')"),
                    NodeId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AttributeDefinitionId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Value = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DateAttributeValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DateAttributeValues_AttributeDefinitions_AttributeDefinitio~",
                        column: x => x.AttributeDefinitionId,
                        principalTable: "AttributeDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DateTimeAttributeValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false, defaultValueSql: "nextval('\"AttributeValueBaseSequence\"')"),
                    NodeId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AttributeDefinitionId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Value = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DateTimeAttributeValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DateTimeAttributeValues_AttributeDefinitions_AttributeDefin~",
                        column: x => x.AttributeDefinitionId,
                        principalTable: "AttributeDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DecimalAttributeValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false, defaultValueSql: "nextval('\"AttributeValueBaseSequence\"')"),
                    NodeId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AttributeDefinitionId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Value = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DecimalAttributeValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DecimalAttributeValues_AttributeDefinitions_AttributeDefini~",
                        column: x => x.AttributeDefinitionId,
                        principalTable: "AttributeDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FileAttributeValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false, defaultValueSql: "nextval('\"AttributeValueBaseSequence\"')"),
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
                    table.ForeignKey(
                        name: "FK_FileAttributeValues_AttributeDefinitions_AttributeDefinitio~",
                        column: x => x.AttributeDefinitionId,
                        principalTable: "AttributeDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IntegerAttributeValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false, defaultValueSql: "nextval('\"AttributeValueBaseSequence\"')"),
                    NodeId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AttributeDefinitionId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Value = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegerAttributeValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntegerAttributeValues_AttributeDefinitions_AttributeDefini~",
                        column: x => x.AttributeDefinitionId,
                        principalTable: "AttributeDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LocationAttributeValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false, defaultValueSql: "nextval('\"AttributeValueBaseSequence\"')"),
                    NodeId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AttributeDefinitionId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationAttributeValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LocationAttributeValues_AttributeDefinitions_AttributeDefin~",
                        column: x => x.AttributeDefinitionId,
                        principalTable: "AttributeDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TextAttributeValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false, defaultValueSql: "nextval('\"AttributeValueBaseSequence\"')"),
                    NodeId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AttributeDefinitionId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TextAttributeValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TextAttributeValues_AttributeDefinitions_AttributeDefinitio~",
                        column: x => x.AttributeDefinitionId,
                        principalTable: "AttributeDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TimeAttributeValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false, defaultValueSql: "nextval('\"AttributeValueBaseSequence\"')"),
                    NodeId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AttributeDefinitionId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Value = table.Column<TimeSpan>(type: "interval", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeAttributeValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimeAttributeValues_AttributeDefinitions_AttributeDefinitio~",
                        column: x => x.AttributeDefinitionId,
                        principalTable: "AttributeDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "AttributeTypes",
                columns: new[] { "Id", "Description", "Name", "SystemType" },
                values: new object[,]
                {
                    { 1, "用于存储短文本、备注、名称等单行或多行文字信息", "文本", "文本" },
                    { 2, "用于存储整数值，如数量、计数、等级等", "整数", "整数" },
                    { 3, "用于存储带小数的数值，如价格、重量等", "小数", "小数" },
                    { 4, "用于存储日期（不含时间）", "日期", "日期" },
                    { 5, "用于存储时间（不含日期）", "时间", "时间" },
                    { 6, "用于存储精确的日期和时间", "日期时间", "日期时间" },
                    { 7, "用于上传和存储文件，记录文件路径、名称等", "文件", "文件" },
                    { 8, "用于存储地理位置信息（经度、纬度）", "定位", "定位" }
                });

            migrationBuilder.InsertData(
                table: "CategoryTrees",
                columns: new[] { "Id", "Description", "IsActive", "IsArchived", "Name", "ParentId", "SortOrder" },
                values: new object[,]
                {
                    { "1B16336D-FB7F-42AA-AFD2-F78388883336", "子分类 3.3", true, false, "Sample 3.3", "27EE32B0-0F30-4331-AA85-61457B7A0912", 2 },
                    { "27EE32B0-0F30-4331-AA85-61457B7A0912", "子分类 3", true, false, "Sample 3", "55705350-7071-43A4-AFAF-2F30B3CE2718", 3 },
                    { "35F02829-6490-467E-9D3E-C2EBF0EAA2B4", "子分类 3.3.1", true, false, "Sample 3.3.1", "1B16336D-FB7F-42AA-AFD2-F78388883336", 0 },
                    { "39EA6315-0A74-40F6-A096-8E15CCC98579", "子分类 1", true, false, "Sample 1", "55705350-7071-43A4-AFAF-2F30B3CE2718", 1 },
                    { "55705350-7071-43A4-AFAF-2F30B3CE2718", "根分类示例", true, false, "Sample Root", null, 0 },
                    { "5C30BACA-3C11-4677-8123-8EC2BE729667", "子分类 3.2", true, false, "Sample 3.2", "27EE32B0-0F30-4331-AA85-61457B7A0912", 1 },
                    { "8E971C0E-B99A-4931-AFD6-46E44D6ECE5A", "子分类 3.1", true, false, "Sample 3.1", "27EE32B0-0F30-4331-AA85-61457B7A0912", 0 },
                    { "975599CC-B967-4AD2-B4B8-9E00D889FB4D", "子分类 2.2 [Archived]", false, true, "Sample 2.2", "C8969ED0-C018-4FDC-AE55-C363BD95C853", 1 },
                    { "C1EBEE10-97F6-44C8-9852-2F574515BF51", "子分类 1.1", true, false, "Sample 1.1", "39EA6315-0A74-40F6-A096-8E15CCC98579", 0 },
                    { "C8969ED0-C018-4FDC-AE55-C363BD95C853", "子分类 2", true, false, "Sample 2", "55705350-7071-43A4-AFAF-2F30B3CE2718", 2 },
                    { "ED0990CF-5BB7-4C36-A3BB-3AF606AD1974", "子分类 2.1", true, false, "Sample 2.1", "C8969ED0-C018-4FDC-AE55-C363BD95C853", 0 }
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_AttributeDefinitions_AttributeTypeId",
                table: "AttributeDefinitions",
                column: "AttributeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AttributeDefinitions_UnitOfMeasureId",
                table: "AttributeDefinitions",
                column: "UnitOfMeasureId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryTrees_ParentId",
                table: "CategoryTrees",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_DateAttributeValues_AttributeDefinitionId",
                table: "DateAttributeValues",
                column: "AttributeDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_DateTimeAttributeValues_AttributeDefinitionId",
                table: "DateTimeAttributeValues",
                column: "AttributeDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_DecimalAttributeValues_AttributeDefinitionId",
                table: "DecimalAttributeValues",
                column: "AttributeDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_FileAttributeValues_AttributeDefinitionId",
                table: "FileAttributeValues",
                column: "AttributeDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegerAttributeValues_AttributeDefinitionId",
                table: "IntegerAttributeValues",
                column: "AttributeDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_LocationAttributeValues_AttributeDefinitionId",
                table: "LocationAttributeValues",
                column: "AttributeDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_TextAttributeValues_AttributeDefinitionId",
                table: "TextAttributeValues",
                column: "AttributeDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_TimeAttributeValues_AttributeDefinitionId",
                table: "TimeAttributeValues",
                column: "AttributeDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitsOfMeasure_Name",
                table: "UnitsOfMeasure",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                name: "AttributeDefinitions");

            migrationBuilder.DropTable(
                name: "AttributeTypes");

            migrationBuilder.DropTable(
                name: "UnitsOfMeasure");

            migrationBuilder.DropSequence(
                name: "AttributeValueBaseSequence");
        }
    }
}
