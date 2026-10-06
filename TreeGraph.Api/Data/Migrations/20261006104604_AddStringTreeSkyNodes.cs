using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TreeGraph.Api.NodeEavSky.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStringTreeSkyNodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "string_tree_sky_nodes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    parent_id = table.Column<int>(type: "integer", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_string_tree_sky_nodes", x => x.id);
                    table.ForeignKey(
                        name: "FK_string_tree_sky_nodes_string_tree_sky_nodes_parent_id",
                        column: x => x.parent_id,
                        principalTable: "string_tree_sky_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_string_tree_sky_nodes_parent",
                table: "string_tree_sky_nodes",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_string_tree_sky_nodes_parent_sort",
                table: "string_tree_sky_nodes",
                columns: new[] { "parent_id", "sort_order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "string_tree_sky_nodes");
        }
    }
}
