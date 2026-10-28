using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreeGraph.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEavEntityType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_lav_entity",
                table: "location_attribute_values");

            migrationBuilder.DropIndex(
                name: "ix_lav_entity_updated",
                table: "location_attribute_values");

            migrationBuilder.DropIndex(
                name: "uq_lav_entity_attr",
                table: "location_attribute_values");

            migrationBuilder.DropIndex(
                name: "ix_av_entity",
                table: "attribute_values");

            migrationBuilder.DropIndex(
                name: "ix_av_entity_updated",
                table: "attribute_values");

            migrationBuilder.DropIndex(
                name: "uq_av_entity_attr",
                table: "attribute_values");

            migrationBuilder.DropIndex(
                name: "ix_attr_catalog_entity",
                table: "attribute_catalog");

            migrationBuilder.DropIndex(
                name: "uq_attr_catalog",
                table: "attribute_catalog");

            migrationBuilder.DropIndex(
                name: "ix_audit_entity",
                table: "attribute_audit_log");

            migrationBuilder.DropColumn(
                name: "entity_type",
                table: "location_attribute_values");

            migrationBuilder.DropColumn(
                name: "entity_type",
                table: "attribute_values");

            migrationBuilder.DropColumn(
                name: "entity_type",
                table: "attribute_catalog");

            migrationBuilder.DropColumn(
                name: "entity_type",
                table: "attribute_audit_log");

            migrationBuilder.CreateIndex(
                name: "ix_lav_entity",
                table: "location_attribute_values",
                column: "entity_id");

            migrationBuilder.CreateIndex(
                name: "ix_lav_entity_updated",
                table: "location_attribute_values",
                columns: new[] { "entity_id", "updated_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "uq_lav_entity_attr",
                table: "location_attribute_values",
                columns: new[] { "entity_id", "attribute_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_av_entity",
                table: "attribute_values",
                column: "entity_id");

            migrationBuilder.CreateIndex(
                name: "ix_av_entity_updated",
                table: "attribute_values",
                columns: new[] { "entity_id", "updated_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "uq_av_entity_attr",
                table: "attribute_values",
                columns: new[] { "entity_id", "attribute_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_attr_catalog",
                table: "attribute_catalog",
                column: "attribute_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_entity",
                table: "attribute_audit_log",
                columns: new[] { "entity_id", "changed_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_lav_entity",
                table: "location_attribute_values");

            migrationBuilder.DropIndex(
                name: "ix_lav_entity_updated",
                table: "location_attribute_values");

            migrationBuilder.DropIndex(
                name: "uq_lav_entity_attr",
                table: "location_attribute_values");

            migrationBuilder.DropIndex(
                name: "ix_av_entity",
                table: "attribute_values");

            migrationBuilder.DropIndex(
                name: "ix_av_entity_updated",
                table: "attribute_values");

            migrationBuilder.DropIndex(
                name: "uq_av_entity_attr",
                table: "attribute_values");

            migrationBuilder.DropIndex(
                name: "uq_attr_catalog",
                table: "attribute_catalog");

            migrationBuilder.DropIndex(
                name: "ix_audit_entity",
                table: "attribute_audit_log");

            migrationBuilder.AddColumn<string>(
                name: "entity_type",
                table: "location_attribute_values",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "entity_type",
                table: "attribute_values",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "entity_type",
                table: "attribute_catalog",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "entity_type",
                table: "attribute_audit_log",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_lav_entity",
                table: "location_attribute_values",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_lav_entity_updated",
                table: "location_attribute_values",
                columns: new[] { "entity_type", "entity_id", "updated_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "uq_lav_entity_attr",
                table: "location_attribute_values",
                columns: new[] { "entity_id", "entity_type", "attribute_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_av_entity",
                table: "attribute_values",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_av_entity_updated",
                table: "attribute_values",
                columns: new[] { "entity_type", "entity_id", "updated_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "uq_av_entity_attr",
                table: "attribute_values",
                columns: new[] { "entity_id", "entity_type", "attribute_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_attr_catalog_entity",
                table: "attribute_catalog",
                column: "entity_type");

            migrationBuilder.CreateIndex(
                name: "uq_attr_catalog",
                table: "attribute_catalog",
                columns: new[] { "entity_type", "attribute_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_entity",
                table: "attribute_audit_log",
                columns: new[] { "entity_type", "entity_id", "changed_at" },
                descending: new[] { false, false, true });
        }
    }
}
