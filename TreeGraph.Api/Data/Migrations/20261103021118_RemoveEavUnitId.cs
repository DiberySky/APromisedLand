using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreeGraph.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEavUnitId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_attribute_catalog_unit_trees_unit_id",
                table: "attribute_catalog");

            migrationBuilder.DropForeignKey(
                name: "FK_attribute_values_unit_trees_unit_id",
                table: "attribute_values");

            migrationBuilder.DropForeignKey(
                name: "FK_composite_field_definitions_unit_trees_unit_id",
                table: "composite_field_definitions");

            migrationBuilder.DropForeignKey(
                name: "FK_custom_table_columns_unit_trees_unit_id",
                table: "custom_table_columns");

            migrationBuilder.DropIndex(
                name: "IX_custom_table_columns_unit_id",
                table: "custom_table_columns");

            migrationBuilder.DropCheckConstraint(
                name: "ck_custom_table_column_numeric_unit",
                table: "custom_table_columns");

            migrationBuilder.DropIndex(
                name: "IX_composite_field_definitions_unit_id",
                table: "composite_field_definitions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_composite_field_decimal_unit",
                table: "composite_field_definitions");

            migrationBuilder.DropIndex(
                name: "IX_attribute_values_unit_id",
                table: "attribute_values");

            migrationBuilder.DropIndex(
                name: "IX_attribute_catalog_unit_id",
                table: "attribute_catalog");

            migrationBuilder.DropColumn(
                name: "unit_id",
                table: "custom_table_columns");

            migrationBuilder.DropColumn(
                name: "unit_id",
                table: "composite_field_definitions");

            migrationBuilder.DropColumn(
                name: "unit_id",
                table: "attribute_values");

            migrationBuilder.DropColumn(
                name: "unit_id",
                table: "attribute_catalog");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "unit_id",
                table: "custom_table_columns",
                type: "character varying(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "unit_id",
                table: "composite_field_definitions",
                type: "character varying(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "unit_id",
                table: "attribute_values",
                type: "character varying(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "unit_id",
                table: "attribute_catalog",
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

            migrationBuilder.CreateIndex(
                name: "IX_composite_field_definitions_unit_id",
                table: "composite_field_definitions",
                column: "unit_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_composite_field_decimal_unit",
                table: "composite_field_definitions",
                sql: "data_type IN ('int', 'decimal') OR unit_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_attribute_values_unit_id",
                table: "attribute_values",
                column: "unit_id");

            migrationBuilder.CreateIndex(
                name: "IX_attribute_catalog_unit_id",
                table: "attribute_catalog",
                column: "unit_id");

            migrationBuilder.AddForeignKey(
                name: "FK_attribute_catalog_unit_trees_unit_id",
                table: "attribute_catalog",
                column: "unit_id",
                principalTable: "unit_trees",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_attribute_values_unit_trees_unit_id",
                table: "attribute_values",
                column: "unit_id",
                principalTable: "unit_trees",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_composite_field_definitions_unit_trees_unit_id",
                table: "composite_field_definitions",
                column: "unit_id",
                principalTable: "unit_trees",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_custom_table_columns_unit_trees_unit_id",
                table: "custom_table_columns",
                column: "unit_id",
                principalTable: "unit_trees",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
