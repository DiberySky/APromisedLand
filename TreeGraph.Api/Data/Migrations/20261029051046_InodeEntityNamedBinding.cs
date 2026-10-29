using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreeGraph.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InodeEntityNamedBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_inode_entity",
                table: "inode_entity");

            migrationBuilder.DropIndex(
                name: "ix_inode_entity_entity",
                table: "inode_entity");

            migrationBuilder.DropIndex(
                name: "ix_inode_entity_type_entity",
                table: "inode_entity");

            migrationBuilder.AddColumn<string>(
                name: "binding_id",
                table: "inode_entity",
                type: "character varying(36)",
                maxLength: 36,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "inode_entity",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            // 存量行回填：binding_id 用随机 GUID；name 用「类型-序号」保证同 (inode,type) 下唯一
            migrationBuilder.Sql(
                "UPDATE inode_entity SET binding_id = gen_random_uuid()::text " +
                "WHERE binding_id IS NULL OR binding_id = '';");

            migrationBuilder.Sql(
                "WITH ranked AS (" +
                "    SELECT ctid, row_number() OVER (" +
                "        PARTITION BY inode_id, entity_type ORDER BY attached_at, entity_id" +
                "    ) AS rn" +
                "    FROM inode_entity" +
                ") " +
                "UPDATE inode_entity t SET name = t.entity_type || '-' || ranked.rn " +
                "FROM ranked WHERE t.ctid = ranked.ctid;");

            migrationBuilder.AddPrimaryKey(
                name: "PK_inode_entity",
                table: "inode_entity",
                column: "binding_id");

            migrationBuilder.CreateIndex(
                name: "ux_inode_entity_name",
                table: "inode_entity",
                columns: new[] { "inode_id", "entity_type", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_inode_entity_type_entity",
                table: "inode_entity",
                columns: new[] { "entity_type", "entity_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_inode_entity",
                table: "inode_entity");

            migrationBuilder.DropIndex(
                name: "ux_inode_entity_name",
                table: "inode_entity");

            migrationBuilder.DropIndex(
                name: "ux_inode_entity_type_entity",
                table: "inode_entity");

            migrationBuilder.DropColumn(
                name: "binding_id",
                table: "inode_entity");

            migrationBuilder.DropColumn(
                name: "name",
                table: "inode_entity");

            migrationBuilder.AddPrimaryKey(
                name: "PK_inode_entity",
                table: "inode_entity",
                columns: new[] { "inode_id", "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_inode_entity_entity",
                table: "inode_entity",
                column: "entity_id");

            migrationBuilder.CreateIndex(
                name: "ix_inode_entity_type_entity",
                table: "inode_entity",
                columns: new[] { "entity_type", "entity_id" });
        }
    }
}
