using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreeGraph.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEdgeDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "edge_definitions",
                columns: table => new
                {
                    edge_type_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false, defaultValueSql: "gen_random_uuid()::text"),
                    type_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    from_node_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    to_node_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_global = table.Column<bool>(type: "boolean", nullable: false),
                    is_directed = table.Column<bool>(type: "boolean", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_edge_definitions", x => x.edge_type_id);
                });

            migrationBuilder.CreateTable(
                name: "edge_instances",
                columns: table => new
                {
                    edge_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false, defaultValueSql: "gen_random_uuid()::text"),
                    edge_type_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    from_node_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    to_node_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    weight = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_edge_instances", x => x.edge_id);
                    table.ForeignKey(
                        name: "FK_edge_instances_edge_definitions_edge_type_id",
                        column: x => x.edge_type_id,
                        principalTable: "edge_definitions",
                        principalColumn: "edge_type_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "uq_edge_definition_type_name",
                table: "edge_definitions",
                column: "type_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ei_from",
                table: "edge_instances",
                columns: new[] { "edge_type_id", "from_node_id" });

            migrationBuilder.CreateIndex(
                name: "ix_ei_from_node",
                table: "edge_instances",
                column: "from_node_id");

            migrationBuilder.CreateIndex(
                name: "ix_ei_to",
                table: "edge_instances",
                columns: new[] { "edge_type_id", "to_node_id" });

            migrationBuilder.CreateIndex(
                name: "ix_ei_to_node",
                table: "edge_instances",
                column: "to_node_id");

            migrationBuilder.CreateIndex(
                name: "uq_edge_instance",
                table: "edge_instances",
                columns: new[] { "edge_type_id", "from_node_id", "to_node_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "edge_instances");

            migrationBuilder.DropTable(
                name: "edge_definitions");
        }
    }
}
