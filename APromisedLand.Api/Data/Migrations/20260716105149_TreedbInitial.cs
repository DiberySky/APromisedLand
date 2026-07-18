using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APromisedLand.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class TreedbInitial : Migration
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
                    ParentId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    HasChildren = table.Column<bool>(type: "boolean", nullable: false)
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
