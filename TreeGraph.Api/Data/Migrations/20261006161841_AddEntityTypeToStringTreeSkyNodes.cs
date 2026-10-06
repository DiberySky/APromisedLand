using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreeGraph.Api.NodeEavSky.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEntityTypeToStringTreeSkyNodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "entity_type",
                table: "string_tree_sky_nodes",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "StringTreeNode");

            migrationBuilder.CreateIndex(
                name: "ix_string_tree_sky_nodes_entity_type",
                table: "string_tree_sky_nodes",
                column: "entity_type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_string_tree_sky_nodes_entity_type",
                table: "string_tree_sky_nodes");

            migrationBuilder.DropColumn(
                name: "entity_type",
                table: "string_tree_sky_nodes");
        }
    }
}
