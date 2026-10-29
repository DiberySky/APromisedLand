using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreeGraph.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class EdgeTypeDisplayOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "display_order",
                table: "edge_types",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "display_order",
                table: "edge_types");
        }
    }
}
