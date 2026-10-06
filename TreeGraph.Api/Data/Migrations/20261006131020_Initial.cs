using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreeGraph.Api.NodeEavSky.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "attribute_audit_log",
                columns: table => new
                {
                    audit_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false, defaultValueSql: "gen_random_uuid()::text"),
                    entity_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    attribute_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    attribute_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    old_value = table.Column<string>(type: "text", nullable: true),
                    new_value = table.Column<string>(type: "text", nullable: true),
                    change_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    changed_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    client_ip = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attribute_audit_log", x => x.audit_id);
                });

            migrationBuilder.CreateTable(
                name: "composite_type_definitions",
                columns: table => new
                {
                    composite_type_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false, defaultValueSql: "gen_random_uuid()::text"),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    type_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_composite_type_definitions", x => x.composite_type_id);
                });

            migrationBuilder.CreateTable(
                name: "custom_table_definitions",
                columns: table => new
                {
                    table_definition_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false, defaultValueSql: "gen_random_uuid()::text"),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    table_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_table_definitions", x => x.table_definition_id);
                });

            migrationBuilder.CreateTable(
                name: "entity_type_catalog",
                columns: table => new
                {
                    entity_type_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false, defaultValueSql: "gen_random_uuid()::text"),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entity_type_catalog", x => x.entity_type_id);
                });

            migrationBuilder.CreateTable(
                name: "inode_entity",
                columns: table => new
                {
                    inode_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    attached_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inode_entity", x => new { x.inode_id, x.entity_type });
                });

            migrationBuilder.CreateTable(
                name: "inode_entitytype",
                columns: table => new
                {
                    inode_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    attached_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inode_entitytype", x => new { x.inode_id, x.entity_type });
                });

            migrationBuilder.CreateTable(
                name: "option_sets",
                columns: table => new
                {
                    option_set_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false, defaultValueSql: "gen_random_uuid()::text"),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    set_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_option_sets", x => x.option_set_id);
                });

            migrationBuilder.CreateTable(
                name: "string_tree_nodes",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    can_have_children = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    parent_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_string_tree_nodes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "string_tree_sky_nodes",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false, defaultValueSql: "gen_random_uuid()::text"),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    parent_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_string_tree_sky_nodes", x => x.id);
                    table.ForeignKey(
                        name: "FK_string_tree_sky_nodes_string_tree_sky_nodes_parent_id",
                        column: x => x.parent_id,
                        principalTable: "string_tree_sky_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "units",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    symbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    to_base_factor = table.Column<decimal>(type: "numeric(38,15)", precision: 38, scale: 15, nullable: false),
                    is_base_unit = table.Column<bool>(type: "boolean", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_units", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "custom_table_columns",
                columns: table => new
                {
                    column_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false, defaultValueSql: "gen_random_uuid()::text"),
                    table_definition_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    column_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    data_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ref_composite_type_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    is_searchable = table.Column<bool>(type: "boolean", nullable: false),
                    is_sortable = table.Column<bool>(type: "boolean", nullable: false),
                    is_unique = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    validation_rule = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    allowed_values = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    default_value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_table_columns", x => x.column_id);
                    table.ForeignKey(
                        name: "FK_custom_table_columns_composite_type_definitions_ref_composi~",
                        column: x => x.ref_composite_type_id,
                        principalTable: "composite_type_definitions",
                        principalColumn: "composite_type_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_custom_table_columns_custom_table_definitions_table_definit~",
                        column: x => x.table_definition_id,
                        principalTable: "custom_table_definitions",
                        principalColumn: "table_definition_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "option_items",
                columns: table => new
                {
                    option_item_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false, defaultValueSql: "gen_random_uuid()::text"),
                    option_set_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_option_items", x => x.option_item_id);
                    table.ForeignKey(
                        name: "FK_option_items_option_sets_option_set_id",
                        column: x => x.option_set_id,
                        principalTable: "option_sets",
                        principalColumn: "option_set_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "attribute_catalog",
                columns: table => new
                {
                    attribute_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false, defaultValueSql: "gen_random_uuid()::text"),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    attribute_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    data_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    is_searchable = table.Column<bool>(type: "boolean", nullable: false),
                    is_sortable = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    allowed_values = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    validation_rule = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    default_value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ref_composite_type_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    ref_table_definition_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    ref_option_set_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attribute_catalog", x => x.attribute_id);
                    table.ForeignKey(
                        name: "FK_attribute_catalog_composite_type_definitions_ref_composite_~",
                        column: x => x.ref_composite_type_id,
                        principalTable: "composite_type_definitions",
                        principalColumn: "composite_type_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_attribute_catalog_custom_table_definitions_ref_table_defini~",
                        column: x => x.ref_table_definition_id,
                        principalTable: "custom_table_definitions",
                        principalColumn: "table_definition_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_attribute_catalog_option_sets_ref_option_set_id",
                        column: x => x.ref_option_set_id,
                        principalTable: "option_sets",
                        principalColumn: "option_set_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_attribute_catalog_units_unit_id",
                        column: x => x.unit_id,
                        principalTable: "units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "composite_field_definitions",
                columns: table => new
                {
                    field_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false, defaultValueSql: "gen_random_uuid()::text"),
                    composite_type_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    field_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    data_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ref_composite_type_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ref_option_set_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    is_array = table.Column<bool>(type: "boolean", nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    is_searchable = table.Column<bool>(type: "boolean", nullable: false),
                    is_sortable = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    validation_rule = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    allowed_values = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    default_value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_composite_field_definitions", x => x.field_id);
                    table.CheckConstraint("ck_composite_field_decimal_unit", "data_type = 'decimal' OR unit_id IS NULL");
                    table.CheckConstraint("ck_composite_field_single_choice_optionset", "data_type = 'single_choice' OR ref_option_set_id IS NULL");
                    table.ForeignKey(
                        name: "FK_composite_field_definitions_composite_type_definitions_comp~",
                        column: x => x.composite_type_id,
                        principalTable: "composite_type_definitions",
                        principalColumn: "composite_type_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_composite_field_definitions_composite_type_definitions_ref_~",
                        column: x => x.ref_composite_type_id,
                        principalTable: "composite_type_definitions",
                        principalColumn: "composite_type_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_composite_field_definitions_option_sets_ref_option_set_id",
                        column: x => x.ref_option_set_id,
                        principalTable: "option_sets",
                        principalColumn: "option_set_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_composite_field_definitions_units_unit_id",
                        column: x => x.unit_id,
                        principalTable: "units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attribute_values",
                columns: table => new
                {
                    value_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false, defaultValueSql: "gen_random_uuid()::text"),
                    entity_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    attribute_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    value_string = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    value_int = table.Column<long>(type: "bigint", nullable: true),
                    value_decimal = table.Column<decimal>(type: "numeric(38,15)", precision: 38, scale: 15, nullable: true),
                    value_bool = table.Column<bool>(type: "boolean", nullable: true),
                    value_datetime = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    value_dateonly = table.Column<DateOnly>(type: "date", nullable: true),
                    value_time = table.Column<TimeOnly>(type: "time(0) without time zone", nullable: true),
                    value_file_meta = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    value_jsonb = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attribute_values", x => x.value_id);
                    table.ForeignKey(
                        name: "FK_attribute_values_attribute_catalog_attribute_id",
                        column: x => x.attribute_id,
                        principalTable: "attribute_catalog",
                        principalColumn: "attribute_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_attribute_values_units_unit_id",
                        column: x => x.unit_id,
                        principalTable: "units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "custom_table_rows",
                columns: table => new
                {
                    row_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false, defaultValueSql: "gen_random_uuid()::text"),
                    table_definition_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    attribute_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    parent_entity_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    parent_entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    row_data = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    row_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_table_rows", x => x.row_id);
                    table.ForeignKey(
                        name: "FK_custom_table_rows_attribute_catalog_attribute_id",
                        column: x => x.attribute_id,
                        principalTable: "attribute_catalog",
                        principalColumn: "attribute_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_custom_table_rows_custom_table_definitions_table_definition~",
                        column: x => x.table_definition_id,
                        principalTable: "custom_table_definitions",
                        principalColumn: "table_definition_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entity",
                table: "attribute_audit_log",
                columns: new[] { "entity_type", "entity_id", "changed_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_audit_time",
                table: "attribute_audit_log",
                column: "changed_at");

            migrationBuilder.CreateIndex(
                name: "ix_attr_catalog_entity",
                table: "attribute_catalog",
                column: "entity_type",
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_attribute_catalog_ref_composite_type_id",
                table: "attribute_catalog",
                column: "ref_composite_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_attribute_catalog_ref_option_set_id",
                table: "attribute_catalog",
                column: "ref_option_set_id");

            migrationBuilder.CreateIndex(
                name: "IX_attribute_catalog_ref_table_definition_id",
                table: "attribute_catalog",
                column: "ref_table_definition_id");

            migrationBuilder.CreateIndex(
                name: "IX_attribute_catalog_unit_id",
                table: "attribute_catalog",
                column: "unit_id");

            migrationBuilder.CreateIndex(
                name: "uq_attr_catalog",
                table: "attribute_catalog",
                columns: new[] { "entity_type", "attribute_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_attribute_values_unit_id",
                table: "attribute_values",
                column: "unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_av_attr_bool",
                table: "attribute_values",
                columns: new[] { "attribute_id", "value_bool" },
                filter: "value_bool IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_av_attr_dateonly",
                table: "attribute_values",
                columns: new[] { "attribute_id", "value_dateonly" },
                filter: "value_dateonly IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_av_attr_datetime",
                table: "attribute_values",
                columns: new[] { "attribute_id", "value_datetime" },
                filter: "value_datetime IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_av_attr_decimal",
                table: "attribute_values",
                columns: new[] { "attribute_id", "value_decimal" },
                filter: "value_decimal IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_av_attr_int",
                table: "attribute_values",
                columns: new[] { "attribute_id", "value_int" },
                filter: "value_int IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_av_attr_string",
                table: "attribute_values",
                columns: new[] { "attribute_id", "value_string" },
                filter: "value_string IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_av_attr_time",
                table: "attribute_values",
                columns: new[] { "attribute_id", "value_time" },
                filter: "value_time IS NOT NULL");

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
                name: "ix_av_file_meta",
                table: "attribute_values",
                column: "value_file_meta",
                filter: "value_file_meta IS NOT NULL")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_av_jsonb",
                table: "attribute_values",
                column: "value_jsonb",
                filter: "value_jsonb IS NOT NULL")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "uq_av_entity_attr",
                table: "attribute_values",
                columns: new[] { "entity_id", "entity_type", "attribute_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_composite_field_definitions_ref_composite_type_id",
                table: "composite_field_definitions",
                column: "ref_composite_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_composite_field_definitions_ref_option_set_id",
                table: "composite_field_definitions",
                column: "ref_option_set_id");

            migrationBuilder.CreateIndex(
                name: "IX_composite_field_definitions_unit_id",
                table: "composite_field_definitions",
                column: "unit_id");

            migrationBuilder.CreateIndex(
                name: "uq_composite_field",
                table: "composite_field_definitions",
                columns: new[] { "composite_type_id", "field_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_composite_type",
                table: "composite_type_definitions",
                columns: new[] { "entity_type", "type_name", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_custom_table_columns_ref_composite_type_id",
                table: "custom_table_columns",
                column: "ref_composite_type_id");

            migrationBuilder.CreateIndex(
                name: "uq_custom_table_column",
                table: "custom_table_columns",
                columns: new[] { "table_definition_id", "column_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_custom_table",
                table: "custom_table_definitions",
                columns: new[] { "entity_type", "table_name", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ctr_order",
                table: "custom_table_rows",
                columns: new[] { "parent_entity_id", "attribute_id", "row_order" });

            migrationBuilder.CreateIndex(
                name: "ix_ctr_parent",
                table: "custom_table_rows",
                columns: new[] { "parent_entity_type", "parent_entity_id", "attribute_id" });

            migrationBuilder.CreateIndex(
                name: "ix_ctr_rowdata",
                table: "custom_table_rows",
                column: "row_data")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_ctr_table",
                table: "custom_table_rows",
                column: "table_definition_id");

            migrationBuilder.CreateIndex(
                name: "IX_custom_table_rows_attribute_id",
                table: "custom_table_rows",
                column: "attribute_id");

            migrationBuilder.CreateIndex(
                name: "uq_entity_type",
                table: "entity_type_catalog",
                column: "entity_type",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_inode_entity_entity",
                table: "inode_entity",
                column: "entity_id");

            migrationBuilder.CreateIndex(
                name: "ix_inode_entity_inode",
                table: "inode_entity",
                column: "inode_id");

            migrationBuilder.CreateIndex(
                name: "uq_inode_entity_global",
                table: "inode_entity",
                columns: new[] { "entity_type", "entity_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inode_et_inode",
                table: "inode_entitytype",
                column: "inode_id");

            migrationBuilder.CreateIndex(
                name: "ix_inode_et_type",
                table: "inode_entitytype",
                column: "entity_type");

            migrationBuilder.CreateIndex(
                name: "ix_option_items_set",
                table: "option_items",
                column: "option_set_id");

            migrationBuilder.CreateIndex(
                name: "uq_option_item_value",
                table: "option_items",
                columns: new[] { "option_set_id", "value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_option_set",
                table: "option_sets",
                columns: new[] { "entity_type", "set_name" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_string_tree_nodes_parent_id",
                table: "string_tree_nodes",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_string_tree_sky_nodes_parent",
                table: "string_tree_sky_nodes",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_string_tree_sky_nodes_parent_sort",
                table: "string_tree_sky_nodes",
                columns: new[] { "parent_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_unit_category_base",
                table: "units",
                column: "category",
                unique: true,
                filter: "is_base_unit = true");

            migrationBuilder.CreateIndex(
                name: "uq_unit_category_name",
                table: "units",
                columns: new[] { "category", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attribute_audit_log");

            migrationBuilder.DropTable(
                name: "attribute_values");

            migrationBuilder.DropTable(
                name: "composite_field_definitions");

            migrationBuilder.DropTable(
                name: "custom_table_columns");

            migrationBuilder.DropTable(
                name: "custom_table_rows");

            migrationBuilder.DropTable(
                name: "entity_type_catalog");

            migrationBuilder.DropTable(
                name: "inode_entity");

            migrationBuilder.DropTable(
                name: "inode_entitytype");

            migrationBuilder.DropTable(
                name: "option_items");

            migrationBuilder.DropTable(
                name: "string_tree_nodes");

            migrationBuilder.DropTable(
                name: "string_tree_sky_nodes");

            migrationBuilder.DropTable(
                name: "attribute_catalog");

            migrationBuilder.DropTable(
                name: "composite_type_definitions");

            migrationBuilder.DropTable(
                name: "custom_table_definitions");

            migrationBuilder.DropTable(
                name: "option_sets");

            migrationBuilder.DropTable(
                name: "units");
        }
    }
}
