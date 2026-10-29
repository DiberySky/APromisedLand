using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreeGraph.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InodeEntityDisplayOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "display_order",
                table: "inode_entity",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // 存量行回填：同一 iNode 内按创建时间序号 1..N
            migrationBuilder.Sql(
                "WITH ranked AS (" +
                "    SELECT ctid, row_number() OVER (" +
                "        PARTITION BY inode_id ORDER BY attached_at, entity_id" +
                "    ) AS rn" +
                "    FROM inode_entity" +
                ") " +
                "UPDATE inode_entity t SET display_order = ranked.rn " +
                "FROM ranked WHERE t.ctid = ranked.ctid;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "display_order",
                table: "inode_entity");
        }
    }
}
