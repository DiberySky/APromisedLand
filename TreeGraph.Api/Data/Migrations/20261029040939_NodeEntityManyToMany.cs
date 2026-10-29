using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreeGraph.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class NodeEntityManyToMany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_inode_entity",
                table: "inode_entity");

            migrationBuilder.DropIndex(
                name: "uq_inode_entity_global",
                table: "inode_entity");

            migrationBuilder.AddPrimaryKey(
                name: "PK_inode_entity",
                table: "inode_entity",
                columns: new[] { "inode_id", "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_inode_entity_type_entity",
                table: "inode_entity",
                columns: new[] { "entity_type", "entity_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_inode_entity",
                table: "inode_entity");

            migrationBuilder.DropIndex(
                name: "ix_inode_entity_type_entity",
                table: "inode_entity");

            migrationBuilder.AddPrimaryKey(
                name: "PK_inode_entity",
                table: "inode_entity",
                columns: new[] { "inode_id", "entity_type" });

            migrationBuilder.CreateIndex(
                name: "uq_inode_entity_global",
                table: "inode_entity",
                columns: new[] { "entity_type", "entity_id" },
                unique: true);
        }
    }
}
