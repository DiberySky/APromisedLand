using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreeGraph.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RegisterEdgesInNodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // R3：存量边先回填进 nodes 注册表（身份 = edge_id，host_type = "EdgeInstance"，name = null）。
            // name 为派生物（from名 → to名 / EAV DisplayName），不落库。
            migrationBuilder.Sql(
                "INSERT INTO nodes (id, host_type, name, created_at) " +
                "SELECT edge_id, 'EdgeInstance', NULL, created_at FROM edge_instances " +
                "ON CONFLICT (id) DO NOTHING;");

            migrationBuilder.AddForeignKey(
                name: "FK_edge_instances_nodes_edge_id",
                table: "edge_instances",
                column: "edge_id",
                principalTable: "nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_edge_instances_nodes_edge_id",
                table: "edge_instances");

            // R3 回滚：注销由边注册产生的 nodes 身份行
            migrationBuilder.Sql(
                "DELETE FROM nodes WHERE host_type = 'EdgeInstance' " +
                "AND id IN (SELECT edge_id FROM edge_instances);");
        }
    }
}
