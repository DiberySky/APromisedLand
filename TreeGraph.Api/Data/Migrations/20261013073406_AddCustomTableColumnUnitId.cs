using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreeGraph.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomTableColumnUnitId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "unit_id",
                table: "custom_table_columns",
                type: "character varying(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_custom_table_columns_unit_id",
                table: "custom_table_columns",
                column: "unit_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_custom_table_column_numeric_unit",
                table: "custom_table_columns",
                sql: "data_type IN ('int', 'decimal') OR unit_id IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_custom_table_columns_unit_trees_unit_id",
                table: "custom_table_columns",
                column: "unit_id",
                principalTable: "unit_trees",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_custom_table_columns_unit_trees_unit_id",
                table: "custom_table_columns");

            migrationBuilder.DropIndex(
                name: "IX_custom_table_columns_unit_id",
                table: "custom_table_columns");

            migrationBuilder.DropCheckConstraint(
                name: "ck_custom_table_column_numeric_unit",
                table: "custom_table_columns");

            migrationBuilder.DropColumn(
                name: "unit_id",
                table: "custom_table_columns");
        }
    }
}
