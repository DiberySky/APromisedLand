using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TreeGraph.Api.Data.Migrations
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
                name: "category_trees",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    can_have_children = table.Column<bool>(type: "boolean", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    parent_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_category_trees", x => x.id);
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
                name: "string_tree_sky_nodes",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false, defaultValueSql: "gen_random_uuid()::text"),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    parent_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    entity_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false, defaultValue: "StringTreeNode"),
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
                name: "unit_trees",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    abbreviation = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    parent_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    can_have_children = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    has_children = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unit_trees", x => x.id);
                    table.ForeignKey(
                        name: "FK_unit_trees_unit_trees_parent_id",
                        column: x => x.parent_id,
                        principalTable: "unit_trees",
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

            migrationBuilder.InsertData(
                table: "category_trees",
                columns: new[] { "id", "can_have_children", "description", "is_archived", "name", "parent_id", "sort_order" },
                values: new object[,]
                {
                    { "1B16336D-FB7F-42AA-AFD2-F78388883336", false, "子分类 3.3", false, "Sample 3.3", "27EE32B0-0F30-4331-AA85-61457B7A0912", 2 },
                    { "27EE32B0-0F30-4331-AA85-61457B7A0912", false, "子分类 3", false, "Sample 3", "55705350-7071-43A4-AFAF-2F30B3CE2718", 3 },
                    { "35F02829-6490-467E-9D3E-C2EBF0EAA2B4", false, "子分类 3.3.1", false, "Sample 3.3.1", "1B16336D-FB7F-42AA-AFD2-F78388883336", 0 },
                    { "39EA6315-0A74-40F6-A096-8E15CCC98579", false, "子分类 1", false, "Sample 1", "55705350-7071-43A4-AFAF-2F30B3CE2718", 1 },
                    { "55705350-7071-43A4-AFAF-2F30B3CE2718", false, "根分类示例", false, "Sample Root", null, 0 },
                    { "5C30BACA-3C11-4677-8123-8EC2BE729667", false, "子分类 3.2", false, "Sample 3.2", "27EE32B0-0F30-4331-AA85-61457B7A0912", 1 },
                    { "8E971C0E-B99A-4931-AFD6-46E44D6ECE5A", false, "子分类 3.1", false, "Sample 3.1", "27EE32B0-0F30-4331-AA85-61457B7A0912", 0 },
                    { "975599CC-B967-4AD2-B4B8-9E00D889FB4D", false, "子分类 2.2 [Archived]", true, "Sample 2.2", "C8969ED0-C018-4FDC-AE55-C363BD95C853", 1 },
                    { "C1EBEE10-97F6-44C8-9852-2F574515BF51", false, "子分类 1.1", false, "Sample 1.1", "39EA6315-0A74-40F6-A096-8E15CCC98579", 0 },
                    { "C8969ED0-C018-4FDC-AE55-C363BD95C853", false, "子分类 2", false, "Sample 2", "55705350-7071-43A4-AFAF-2F30B3CE2718", 2 },
                    { "ED0990CF-5BB7-4C36-A3BB-3AF606AD1974", false, "子分类 2.1", false, "Sample 2.1", "C8969ED0-C018-4FDC-AE55-C363BD95C853", 0 }
                });

            migrationBuilder.InsertData(
                table: "unit_trees",
                columns: new[] { "id", "abbreviation", "can_have_children", "description", "has_children", "name", "parent_id", "sort_order" },
                values: new object[,]
                {
                    { "9AB5700C-68F2-43F3-9D7E-805E7D5C539B", "", true, "", true, "计量单位", null, 0 },
                    { "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", "", true, "长度计量单位", true, "长度", "9AB5700C-68F2-43F3-9D7E-805E7D5C539B", 1 },
                    { "a3b4c5d6-e7f8-4a9b-0c1d-2e3f4a5b6c7d", "", true, "频率计量单位", true, "频率", "9AB5700C-68F2-43F3-9D7E-805E7D5C539B", 13 },
                    { "a7b8c9d0-e1f2-4a3b-4c5d-6e7f8a9b0c1d", "", true, "功率计量单位", true, "功率", "9AB5700C-68F2-43F3-9D7E-805E7D5C539B", 7 },
                    { "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e", "", true, "质量计量单位", true, "质量", "9AB5700C-68F2-43F3-9D7E-805E7D5C539B", 2 },
                    { "b4c5d6e7-f8a9-4b0c-1d2e-3f4a5b6c7d8e", "", true, "角度计量单位", true, "角度", "9AB5700C-68F2-43F3-9D7E-805E7D5C539B", 14 },
                    { "b8c9d0e1-f2a3-4b4c-5d6e-7f8a9b0c1d2e", "", true, "面积计量单位", true, "面积", "9AB5700C-68F2-43F3-9D7E-805E7D5C539B", 8 },
                    { "c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f", "", true, "时间计量单位", true, "时间", "9AB5700C-68F2-43F3-9D7E-805E7D5C539B", 3 },
                    { "c5d6e7f8-a9b0-4c1d-2e3f-4a5b6c7d8e9f", "", true, "货币计量单位", true, "货币", "9AB5700C-68F2-43F3-9D7E-805E7D5C539B", 0 },
                    { "c9d0e1f2-a3b4-4c5d-6e7f-8a9b0c1d2e3f", "", true, "体积计量单位", true, "体积", "9AB5700C-68F2-43F3-9D7E-805E7D5C539B", 9 },
                    { "d0e1f2a3-b4c5-4d6e-7f8a-9b0c1d2e3f4a", "", true, "速度计量单位", true, "速度", "9AB5700C-68F2-43F3-9D7E-805E7D5C539B", 10 },
                    { "d4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a", "", true, "温度计量单位", true, "温度", "9AB5700C-68F2-43F3-9D7E-805E7D5C539B", 4 },
                    { "e1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a5b", "", true, "压力计量单位", true, "压力", "9AB5700C-68F2-43F3-9D7E-805E7D5C539B", 11 },
                    { "e5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8a9b", "", true, "电流计量单位", true, "电流", "9AB5700C-68F2-43F3-9D7E-805E7D5C539B", 5 },
                    { "f2a3b4c5-d6e7-4f8a-9b0c-1d2e3f4a5b6c", "", true, "能量计量单位", true, "能量", "9AB5700C-68F2-43F3-9D7E-805E7D5C539B", 12 },
                    { "f6a7b8c9-d0e1-4f2a-3b4c-5d6e7f8a9b0c", "", true, "电压计量单位", true, "电压", "9AB5700C-68F2-43F3-9D7E-805E7D5C539B", 6 },
                    { "0d7ebe17-93ae-4e4c-92f4-063a124cd181", "km²", false, "", false, "平方公里", "b8c9d0e1-f2a3-4b4c-5d6e-7f8a9b0c1d2e", 54 },
                    { "1a80ed3b-1b36-4d8b-b80b-3070dbc7979d", "kHz", false, "", false, "千赫", "a3b4c5d6-e7f8-4a9b-0c1d-2e3f4a5b6c7d", 71 },
                    { "221d3c45-911f-4e94-9c3e-c13e6f2bcc76", "Pa", false, "", false, "帕斯卡", "e1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a5b", 63 },
                    { "279a6b18-6d01-4437-b95b-0480ca7adc98", "kJ", false, "", false, "千焦", "f2a3b4c5-d6e7-4f8a-9b0c-1d2e3f4a5b6c", 68 },
                    { "2d21c35a-4251-479e-b814-060b2fc84445", "m³", false, "", false, "立方米", "c9d0e1f2-a3b4-4c5d-6e7f-8a9b0c1d2e3f", 57 },
                    { "3d9088a3-7283-4f8f-b995-b193a57a6c2a", "rad", false, "", false, "弧度", "b4c5d6e7-f8a9-4b0c-1d2e-3f4a5b6c7d8e", 74 },
                    { "405ae7a3-8a13-479d-bc1a-6f9d3c15e521", "mph", false, "", false, "英里/小时", "d0e1f2a3-b4c5-4d6e-7f8a-9b0c1d2e3f4a", 62 },
                    { "41572712-95dd-4caf-b316-e1b924bc57c3", "MHz", false, "", false, "兆赫", "a3b4c5d6-e7f8-4a9b-0c1d-2e3f4a5b6c7d", 72 },
                    { "4cbec89d-3f52-4db3-9ab0-faeeb841ffbf", "m/s", false, "", false, "米/秒", "d0e1f2a3-b4c5-4d6e-7f8a-9b0c1d2e3f4a", 60 },
                    { "5db494d7-2a9c-4ae0-86e0-d4bb0dfc7b81", "J", false, "", false, "焦耳", "f2a3b4c5-d6e7-4f8a-9b0c-1d2e3f4a5b6c", 67 },
                    { "5e880060-9410-40d7-bcb4-545ccd0c1bb6", "Hz", false, "", false, "赫兹", "a3b4c5d6-e7f8-4a9b-0c1d-2e3f4a5b6c7d", 70 },
                    { "64e918fb-ee9d-45c7-b35a-2a55f5a5fe62", "km/h", false, "", false, "千米/小时", "d0e1f2a3-b4c5-4d6e-7f8a-9b0c1d2e3f4a", 61 },
                    { "780e7a01-350d-45ec-b963-b36a996de614", "mL", false, "", false, "毫升", "c9d0e1f2-a3b4-4c5d-6e7f-8a9b0c1d2e3f", 59 },
                    { "7f0af6a9-ba1a-469c-b967-e32afe43cad2", "L", false, "", false, "升", "c9d0e1f2-a3b4-4c5d-6e7f-8a9b0c1d2e3f", 58 },
                    { "883a9940-84ec-4daa-8448-609461b984ea", "kPa", false, "", false, "千帕", "e1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a5b", 64 },
                    { "8981bd9a-bd99-4f4c-b8af-038975b799be", "bar", false, "", false, "巴", "e1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a5b", 66 },
                    { "a00be966-be2e-484e-92b6-9706494ac775", "hp", false, "", false, "马力", "a7b8c9d0-e1f2-4a3b-4c5d-6e7f8a9b0c1d", 52 },
                    { "a0e1f2a3-b4c5-4d6e-7f8a-9b0c1d2e3f4a", "mg", false, "", false, "毫克", "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e", 32 },
                    { "a2e3f4a5-b6c7-4d8e-9f0a-1b2c3d4e5f6a", "mA", false, "", false, "毫安", "e5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8a9b", 44 },
                    { "a4c312d3-023e-4d4e-b5a7-fb7fcbd55c56", "ha", false, "", false, "公顷", "b8c9d0e1-f2a3-4b4c-5d6e-7f8a9b0c1d2e", 55 },
                    { "a4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a", "mi", false, "", false, "英里", "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", 26 },
                    { "a5b6c7d8-e9f0-4a1b-2c3d-4e5f6a7b8c9d", "HKD", false, "港元", false, "港元", "c5d6e7f8-a9b0-4c1d-2e3f-4a5b6c7d8e9f", 20 },
                    { "a6e7f8a9-b0c1-4d2e-3f4a-5b6c7d8e9f0a", "h", false, "", false, "小时", "c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f", 38 },
                    { "a8e9f0a1-b2c3-4d4e-5f6a-7b8c9d0e1f2a", "kW", false, "", false, "千瓦", "a7b8c9d0-e1f2-4a3b-4c5d-6e7f8a9b0c1d", 50 },
                    { "b1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a5b", "t", false, "", false, "吨", "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e", 33 },
                    { "b3c4d5e6-f7a8-4b9c-0d1e-2f3a4b5c6d7e", "JPY", false, "日元", false, "日元", "c5d6e7f8-a9b0-4c1d-2e3f-4a5b6c7d8e9f", 19 },
                    { "b3f4a5b6-c7d8-4e9f-0a1b-2c3d4e5f6a7b", "µA", false, "", false, "微安", "e5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8a9b", 45 },
                    { "b5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8a9b", "yd", false, "", false, "码", "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", 27 },
                    { "b6c7d8e9-f0a1-4b2c-3d4e-5f6a7b8c9d0e", "AUD", false, "澳元", false, "澳元", "c5d6e7f8-a9b0-4c1d-2e3f-4a5b6c7d8e9f", 21 },
                    { "b7f8a9b0-c1d2-4e3f-4a5b-6c7d8e9f0a1b", "d", false, "", false, "天", "c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f", 39 },
                    { "b9f0a1b2-c3d4-4e5f-6a7b-8c9d0e1f2a3b", "MW", false, "", false, "兆瓦", "a7b8c9d0-e1f2-4a3b-4c5d-6e7f8a9b0c1d", 51 },
                    { "c0a1b2c3-d4e5-4f6a-7b8c-9d0e1f2a3b4c", "m", false, "", false, "米", "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", 22 },
                    { "c2a3b4c5-d6e7-4f8a-9b0c-1d2e3f4a5b6c", "lb", false, "", false, "磅", "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e", 34 },
                    { "c4a5b6c7-d8e9-4f0a-1b2c-3d4e5f6a7b8c", "V", false, "", false, "伏特", "f6a7b8c9-d0e1-4f2a-3b4c-5d6e7f8a9b0c", 46 },
                    { "c4d5e6f7-a8b9-4c0d-1e2f-3a4b5c6d7e8f", "GBP", false, "英镑", false, "英镑", "c5d6e7f8-a9b0-4c1d-2e3f-4a5b6c7d8e9f", 18 },
                    { "c6a7b8c9-d0e1-4f2a-3b4c-5d6e7f8a9b0c", "ft", false, "", false, "英尺", "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", 28 },
                    { "c8a9b0c1-d2e3-4f4a-5b6c-7d8e9f0a1b2c", "°C", false, "", false, "摄氏度", "d4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a", 40 },
                    { "d1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", "km", false, "", false, "千米", "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", 23 },
                    { "d3b4c5d6-e7f8-4a9b-0c1d-2e3f4a5b6c7d", "oz", false, "", false, "盎司", "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e", 35 },
                    { "d5306eb8-324f-4088-a867-6fbc7141fd59", "MPa", false, "", false, "兆帕", "e1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a5b", 65 },
                    { "d5b6c7d8-e9f0-4a1b-2c3d-4e5f6a7b8c9d", "kV", false, "", false, "千伏", "f6a7b8c9-d0e1-4f2a-3b4c-5d6e7f8a9b0c", 47 },
                    { "d5c6d7e8-f9a0-4b1c-2d3e-4f5a6b7c8d9e", "EUR", false, "欧元", false, "欧元", "c5d6e7f8-a9b0-4c1d-2e3f-4a5b6c7d8e9f", 17 },
                    { "d7b8c9d0-e1f2-4a3b-4c5d-6e7f8a9b0c1d", "in", false, "", false, "英寸", "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", 29 },
                    { "d9b0c1d2-e3f4-4a5b-6c7d-8e9f0a1b2c3d", "°F", false, "", false, "华氏度", "d4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a", 41 },
                    { "e0c1d2e3-f4a5-4b6c-7d8e-9f0a1b2c3d4e", "K", false, "", false, "开尔文", "d4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a", 42 },
                    { "e2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e", "cm", false, "", false, "厘米", "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", 24 },
                    { "e3c1f025-3b2b-461a-a5a1-015cb4e3fe38", "kWh", false, "", false, "千瓦时", "f2a3b4c5-d6e7-4f8a-9b0c-1d2e3f4a5b6c", 69 },
                    { "e4c5d6e7-f8a9-4b0c-1d2e-3f4a5b6c7d8e", "s", false, "", false, "秒", "c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f", 36 },
                    { "e6b7c8d9-f0a1-4b2c-3d4e-5f6a7b8c9d0e", "USD", false, "美元", false, "美元", "c5d6e7f8-a9b0-4c1d-2e3f-4a5b6c7d8e9f", 16 },
                    { "e6c7d8e9-f0a1-4b2c-3d4e-5f6a7b8c9d0e", "mV", false, "", false, "毫伏", "f6a7b8c9-d0e1-4f2a-3b4c-5d6e7f8a9b0c", 48 },
                    { "e88b04db-40ac-4bb7-b420-1f3b37180673", "m²", false, "", false, "平方米", "b8c9d0e1-f2a3-4b4c-5d6e-7f8a9b0c1d2e", 53 },
                    { "e8c9d0e1-f2a3-4b4c-5d6e-7f8a9b0c1d2e", "kg", false, "", false, "千克", "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e", 30 },
                    { "ed1b66d2-454b-453b-9d43-12605dffa456", "°", false, "", false, "度", "b4c5d6e7-f8a9-4b0c-1d2e-3f4a5b6c7d8e", 73 },
                    { "f1d2e3f4-a5b6-4c7d-8e9f-0a1b2c3d4e5f", "A", false, "", false, "安培", "e5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8a9b", 43 },
                    { "f3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f", "mm", false, "", false, "毫米", "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", 25 },
                    { "f5d6e7f8-a9b0-4c1d-2e3f-4a5b6c7d8e9f", "min", false, "", false, "分钟", "c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f", 37 },
                    { "f7a8b9c0-d1e2-4f3a-4b5c-6d7e8f9a0b1c", "CNY", false, "人民币", false, "元", "c5d6e7f8-a9b0-4c1d-2e3f-4a5b6c7d8e9f", 15 },
                    { "f7d8e9f0-a1b2-4c3d-4e5f-6a7b8c9d0e1f", "W", false, "", false, "瓦特", "a7b8c9d0-e1f2-4a3b-4c5d-6e7f8a9b0c1d", 49 },
                    { "f9d0e1f2-a3b4-4c5d-6e7f-8a9b0c1d2e3f", "g", false, "", false, "克", "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e", 31 },
                    { "fefa26a5-d608-411c-b637-469a886e558c", "亩", false, "", false, "亩", "b8c9d0e1-f2a3-4b4c-5d6e-7f8a9b0c1d2e", 56 }
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
                name: "ix_category_trees_parent",
                table: "category_trees",
                column: "parent_id");

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
                name: "ix_string_tree_sky_nodes_entity_type",
                table: "string_tree_sky_nodes",
                column: "entity_type");

            migrationBuilder.CreateIndex(
                name: "ix_string_tree_sky_nodes_parent",
                table: "string_tree_sky_nodes",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_string_tree_sky_nodes_parent_sort",
                table: "string_tree_sky_nodes",
                columns: new[] { "parent_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_unit_trees_parent",
                table: "unit_trees",
                column: "parent_id");

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
                name: "category_trees");

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
                name: "string_tree_sky_nodes");

            migrationBuilder.DropTable(
                name: "unit_trees");

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
