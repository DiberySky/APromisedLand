using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreeGraph.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEavTreeRoots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "eav_tree_roots",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    attribute_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    binding_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eav_tree_roots", x => x.id);
                    table.ForeignKey(
                        name: "FK_eav_tree_roots_inode_entity_binding_id",
                        column: x => x.binding_id,
                        principalTable: "inode_entity",
                        principalColumn: "binding_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_etr_binding",
                table: "eav_tree_roots",
                column: "binding_id");

            migrationBuilder.CreateIndex(
                name: "uq_etr_entity_attr",
                table: "eav_tree_roots",
                columns: new[] { "entity_id", "attribute_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "eav_tree_roots");
        }
    }
}
