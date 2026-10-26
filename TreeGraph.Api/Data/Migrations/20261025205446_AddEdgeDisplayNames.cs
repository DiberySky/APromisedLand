using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreeGraph.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEdgeDisplayNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "display_name",
                table: "edge_types",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "display_name",
                table: "edge_type_props",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "edge_type_props",
                keyColumns: new[] { "edge_type_name", "prop_name" },
                keyValues: new object[] { "line_segment", "name" },
                column: "display_name",
                value: null);

            migrationBuilder.UpdateData(
                table: "edge_types",
                keyColumn: "name",
                keyValue: "line_segment",
                column: "display_name",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "display_name",
                table: "edge_types");

            migrationBuilder.DropColumn(
                name: "display_name",
                table: "edge_type_props");
        }
    }
}
