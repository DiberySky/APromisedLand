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
                    table.ForeignKey(
                        name: "FK_CategoryTrees_CategoryTrees_ParentId",
                        column: x => x.ParentId,
                        principalTable: "CategoryTrees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "CategoryTrees",
                columns: new[] { "Id", "Description", "IsActive", "IsArchived", "Name", "ParentId", "SortOrder" },
                values: new object[,]
                {
                    { "55705350-7071-43A4-AFAF-2F30B3CE2718", "根分类示例", true, false, "Sample Root", null, 0 },
                    { "27EE32B0-0F30-4331-AA85-61457B7A0912", "子分类 3", true, false, "Sample 3", "55705350-7071-43A4-AFAF-2F30B3CE2718", 3 },
                    { "39EA6315-0A74-40F6-A096-8E15CCC98579", "子分类 1", true, false, "Sample 1", "55705350-7071-43A4-AFAF-2F30B3CE2718", 1 },
                    { "C8969ED0-C018-4FDC-AE55-C363BD95C853", "子分类 2", true, false, "Sample 2", "55705350-7071-43A4-AFAF-2F30B3CE2718", 2 },
                    { "1B16336D-FB7F-42AA-AFD2-F78388883336", "子分类 3.3", true, false, "Sample 3.3", "27EE32B0-0F30-4331-AA85-61457B7A0912", 2 },
                    { "5C30BACA-3C11-4677-8123-8EC2BE729667", "子分类 3.2", true, false, "Sample 3.2", "27EE32B0-0F30-4331-AA85-61457B7A0912", 1 },
                    { "8E971C0E-B99A-4931-AFD6-46E44D6ECE5A", "子分类 3.1", true, false, "Sample 3.1", "27EE32B0-0F30-4331-AA85-61457B7A0912", 0 },
                    { "975599CC-B967-4AD2-B4B8-9E00D889FB4D", "子分类 2.2 [Archived]", false, true, "Sample 2.2", "C8969ED0-C018-4FDC-AE55-C363BD95C853", 1 },
                    { "C1EBEE10-97F6-44C8-9852-2F574515BF51", "子分类 1.1", true, false, "Sample 1.1", "39EA6315-0A74-40F6-A096-8E15CCC98579", 0 },
                    { "ED0990CF-5BB7-4C36-A3BB-3AF606AD1974", "子分类 2.1", true, false, "Sample 2.1", "C8969ED0-C018-4FDC-AE55-C363BD95C853", 0 },
                    { "35F02829-6490-467E-9D3E-C2EBF0EAA2B4", "子分类 3.3.1", true, false, "Sample 3.3.1", "1B16336D-FB7F-42AA-AFD2-F78388883336", 0 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CategoryTrees_ParentId",
                table: "CategoryTrees",
                column: "ParentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CategoryTrees");
        }
    }
}
