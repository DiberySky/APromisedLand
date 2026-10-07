using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreeGraph.Api.NodeEavSky.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyStringTreeNodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "string_tree_nodes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "string_tree_nodes",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    can_have_children = table.Column<bool>(type: "boolean", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    parent_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_string_tree_nodes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_string_tree_nodes_parent_id",
                table: "string_tree_nodes",
                column: "parent_id");
        }
    }
}
