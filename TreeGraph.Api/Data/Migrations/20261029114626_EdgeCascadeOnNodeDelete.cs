using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreeGraph.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class EdgeCascadeOnNodeDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_edges_dst_node",
                table: "edges");

            migrationBuilder.DropForeignKey(
                name: "fk_edges_src_node",
                table: "edges");

            migrationBuilder.AddForeignKey(
                name: "fk_edges_dst_node",
                table: "edges",
                column: "dst_vid",
                principalTable: "nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_edges_src_node",
                table: "edges",
                column: "src_vid",
                principalTable: "nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_edges_dst_node",
                table: "edges");

            migrationBuilder.DropForeignKey(
                name: "fk_edges_src_node",
                table: "edges");

            migrationBuilder.AddForeignKey(
                name: "fk_edges_dst_node",
                table: "edges",
                column: "dst_vid",
                principalTable: "nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_edges_src_node",
                table: "edges",
                column: "src_vid",
                principalTable: "nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
