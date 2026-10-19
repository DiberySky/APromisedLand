using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TreeGraph.Api.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    is_record_table = table.Column<bool>(type: "boolean", nullable: false),
                    has_time_column = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_table_definitions", x => x.table_definition_id);
                });

            migrationBuilder.CreateTable(
                name: "edge_definitions",
                columns: table => new
                {
                    edge_type_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false, defaultValueSql: "gen_random_uuid()::text"),
                    type_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    from_node_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    to_node_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_global = table.Column<bool>(type: "boolean", nullable: false),
                    is_directed = table.Column<bool>(type: "boolean", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    IsLine = table.Column<bool>(type: "boolean", nullable: false),
                    IsLinear = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_edge_definitions", x => x.edge_type_id);
                });

            migrationBuilder.CreateTable(
                name: "entity_type_catalog",
                columns: table => new
                {
                    entity_type_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false, defaultValueSql: "gen_random_uuid()::text"),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    owner_space_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
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
                name: "knowledge_trees",
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
                    table.PrimaryKey("PK_knowledge_trees", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "line_nodes",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false, defaultValueSql: "gen_random_uuid()::text"),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    location_geo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_line_nodes", x => x.id);
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
                name: "space_trees",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    parent_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    can_have_children = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    has_children = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_space_trees", x => x.id);
                    table.ForeignKey(
                        name: "FK_space_trees_space_trees_parent_id",
                        column: x => x.parent_id,
                        principalTable: "space_trees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "spaces",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_spaces", x => x.id);
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
                name: "edge_instances",
                columns: table => new
                {
                    edge_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false, defaultValueSql: "gen_random_uuid()::text"),
                    edge_type_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    from_node_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    to_node_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    weight = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    enforce_single_incoming = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    display_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_edge_instances", x => x.edge_id);
                    table.ForeignKey(
                        name: "FK_edge_instances_edge_definitions_edge_type_id",
                        column: x => x.edge_type_id,
                        principalTable: "edge_definitions",
                        principalColumn: "edge_type_id",
                        onDelete: ReferentialAction.Restrict);
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
                        onDelete: ReferentialAction.Restrict);
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
                    IsRecordMode = table.Column<bool>(type: "boolean", nullable: false),
                    ref_option_set_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    unit_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
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
                        name: "FK_attribute_catalog_unit_trees_unit_id",
                        column: x => x.unit_id,
                        principalTable: "unit_trees",
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
                    unit_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
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
                        onDelete: ReferentialAction.Restrict);
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
                        name: "FK_composite_field_definitions_unit_trees_unit_id",
                        column: x => x.unit_id,
                        principalTable: "unit_trees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
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
                    default_value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    unit_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_table_columns", x => x.column_id);
                    table.CheckConstraint("ck_custom_table_column_numeric_unit", "data_type IN ('int', 'decimal') OR unit_id IS NULL");
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
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_custom_table_columns_unit_trees_unit_id",
                        column: x => x.unit_id,
                        principalTable: "unit_trees",
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
                    unit_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true)
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
                        name: "FK_attribute_values_unit_trees_unit_id",
                        column: x => x.unit_id,
                        principalTable: "unit_trees",
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
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_custom_table_rows_custom_table_definitions_table_definition~",
                        column: x => x.table_definition_id,
                        principalTable: "custom_table_definitions",
                        principalColumn: "table_definition_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "location_attribute_values",
                columns: table => new
                {
                    value_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false, defaultValueSql: "gen_random_uuid()::text"),
                    entity_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    attribute_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    coordinate_system = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "gcj02"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_location_attribute_values", x => x.value_id);
                    table.ForeignKey(
                        name: "FK_location_attribute_values_attribute_catalog_attribute_id",
                        column: x => x.attribute_id,
                        principalTable: "attribute_catalog",
                        principalColumn: "attribute_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "attribute_catalog",
                columns: new[] { "attribute_id", "allowed_values", "attribute_name", "created_at", "data_type", "default_value", "display_name", "display_order", "entity_type", "is_deleted", "IsRecordMode", "is_required", "is_searchable", "is_sortable", "ref_composite_type_id", "ref_option_set_id", "ref_table_definition_id", "unit_id", "updated_at", "validation_rule", "version" },
                values: new object[,]
                {
                    { "a0000001-0000-4000-8000-000000000001", null, "voltageLevel", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "string", null, "电压等级", 0, "ConnectedTo", false, false, false, true, false, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000002-0000-4000-8000-000000000002", null, "connectionMode", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "string", null, "连接方式", 1, "ConnectedTo", false, false, false, true, false, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000003-0000-4000-8000-000000000003", null, "supplyDirection", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "string", null, "供电方向", 0, "SuppliedBy", false, false, false, true, false, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000004-0000-4000-8000-000000000004", null, "isPrimarySupply", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "bool", null, "是否主供", 1, "SuppliedBy", false, false, false, true, true, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000005-0000-4000-8000-000000000005", null, "cableSegmentCount", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "int", null, "电缆段数", 0, "LowVoltageAccess", false, false, false, true, true, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000006-0000-4000-8000-000000000006", null, "cableLength", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "decimal", null, "电缆长度", 1, "LowVoltageAccess", false, false, false, true, true, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000007-0000-4000-8000-000000000007", null, "voltageLevel", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "string", null, "电压等级", 0, "TransmissionLine", false, false, false, true, false, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000008-0000-4000-8000-000000000008", null, "commissionDate", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "date", null, "投运日期", 1, "TransmissionLine", false, false, false, true, true, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000009-0000-4000-8000-000000000009", null, "ratedCurrent", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "decimal", null, "额定载流量", 0, "Feeder", false, false, false, true, true, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000010-0000-4000-8000-000000000010", null, "isSingleSource", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "bool", null, "单电源", 1, "Feeder", false, false, false, true, true, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000011-0000-4000-8000-000000000011", null, "belongType", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "string", null, "归属类型", 0, "BelongsTo", false, false, false, true, false, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000012-0000-4000-8000-000000000012", null, "effectiveDate", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "date", null, "生效日期", 1, "BelongsTo", false, false, false, true, true, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000013-0000-4000-8000-000000000013", null, "assignType", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "string", null, "归属类型", 0, "AssignedTo", false, false, false, true, false, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000014-0000-4000-8000-000000000014", null, "installYear", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "int", null, "安装年份", 0, "InstalledIn", false, false, false, true, true, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000015-0000-4000-8000-000000000015", null, "installedCapacity", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "decimal", null, "装机容量", 1, "InstalledIn", false, false, false, true, true, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000016-0000-4000-8000-000000000016", null, "distance", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "decimal", null, "距离", 0, "AdjacentTo", false, false, false, true, true, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000017-0000-4000-8000-000000000017", null, "shareBoundary", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "bool", null, "共享边界", 1, "AdjacentTo", false, false, false, true, true, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000018-0000-4000-8000-000000000018", null, "position", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "location", null, "位置", 0, "LocatedAt", false, false, false, true, false, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000019-0000-4000-8000-000000000019", null, "protectionType", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "string", null, "保护类型", 0, "Protects", false, false, false, true, false, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000020-0000-4000-8000-000000000020", null, "settingValue", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "string", null, "保护定值", 1, "Protects", false, false, false, true, false, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000021-0000-4000-8000-000000000021", null, "coordinationGap", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "decimal", null, "配合级差", 0, "BacksUp", false, false, false, true, true, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000022-0000-4000-8000-000000000022", null, "timeSetting", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "decimal", null, "时间定值", 1, "BacksUp", false, false, false, true, true, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000023-0000-4000-8000-000000000023", null, "ruleCode", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "string", null, "规则编号", 0, "PrerequisiteFor", false, false, false, true, false, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000024-0000-4000-8000-000000000024", null, "precondition", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "string", null, "前置条件", 1, "PrerequisiteFor", false, false, false, true, false, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000025-0000-4000-8000-000000000025", null, "dependencyType", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "string", null, "依赖类型", 0, "DependsOn", false, false, false, true, false, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000026-0000-4000-8000-000000000026", null, "impactLevel", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "string", null, "影响等级", 0, "Affects", false, false, false, true, false, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000027-0000-4000-8000-000000000027", null, "estimatedRecoveryMinutes", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "int", null, "预计恢复时长", 1, "Affects", false, false, false, true, true, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000028-0000-4000-8000-000000000028", null, "eventTime", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "datetime", null, "事件时刻", 0, "TrippedBy", false, false, false, true, true, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000029-0000-4000-8000-000000000029", null, "faultType", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "string", null, "故障类型", 1, "TrippedBy", false, false, false, true, false, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000030-0000-4000-8000-000000000030", null, "room", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "string", null, "房间", 1, "LocatedAt", false, false, false, true, false, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000031-0000-4000-8000-000000000031", null, "settingUnit", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "string", null, "定值单位", 2, "Protects", false, false, false, true, false, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000032-0000-4000-8000-000000000032", null, "clearance_m", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "decimal", null, "垂直净距（米）", 1, "LineNode", false, false, true, true, true, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000033-0000-4000-8000-000000000033", null, "crossing_angle", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "decimal", null, "交叉角（度）", 2, "LineNode", false, false, false, true, true, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000034-0000-4000-8000-000000000034", null, "crossing_type", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "single_choice", null, "跨越物类型", 3, "LineNode", false, false, true, true, false, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000035-0000-4000-8000-000000000035", null, "voltage_below", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "string", null, "被跨线路电压等级", 4, "LineNode", false, false, false, true, false, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000036-0000-4000-8000-000000000036", null, "location_geo", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "location", null, "经纬度", 5, "LineNode", false, false, false, false, false, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "a0000037-0000-4000-8000-000000000037", null, "measured_at", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "datetime", null, "最近测量时间", 6, "LineNode", false, false, false, true, true, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 }
                });

            migrationBuilder.InsertData(
                table: "category_trees",
                columns: new[] { "id", "can_have_children", "description", "is_archived", "name", "parent_id", "sort_order" },
                values: new object[] { "d0000000-0000-4000-8000-000000000001", true, "根分类", false, "项目类型", null, 0 });

            migrationBuilder.InsertData(
                table: "edge_definitions",
                columns: new[] { "edge_type_id", "created_at", "description", "display_name", "from_node_type", "is_active", "is_directed", "is_global", "IsLine", "IsLinear", "to_node_type", "type_name", "updated_at" },
                values: new object[,]
                {
                    { "e0000001-0000-4000-8000-000000000001", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "设备之间的电气连通（母线↔断路器、隔离开关↔线路）；物理连接对称，按无向处理。", "电气连接", "SpaceTree", true, false, false, false, false, "SpaceTree", "ConnectedTo", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "e0000002-0000-4000-8000-000000000002", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "下游设备指向供电上游，沿供电方向逐级上溯；线路结构支持完整供电路径查询与成环校验。", "供电来源", "SpaceTree", true, true, false, true, false, "SpaceTree", "SuppliedBy", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "e0000003-0000-4000-8000-000000000003", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "低压电缆组接入变压器，与电气连接配合还原「建筑—低压柜—变压器」链路。", "低压接入", "SpaceTree", true, true, false, false, false, "SpaceTree", "LowVoltageAccess", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "e0000004-0000-4000-8000-000000000004", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "线路段序列，属线路结构；允许 T 接分支（不限制入度）。", "输电线路", "SpaceTree", true, true, false, true, false, "SpaceTree", "TransmissionLine", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "e0000005-0000-4000-8000-000000000005", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "单电源馈线，线路结构且严格线性（入度≤1、无环）。", "馈线", "SpaceTree", true, true, false, true, true, "SpaceTree", "Feeder", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "e0000006-0000-4000-8000-000000000006", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "设备在管理上归属台区/馈线等逻辑单元；与电气连接、物理位置分开建模。", "逻辑归属", "SpaceTree", true, true, false, false, false, "SpaceTree", "BelongsTo", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "e0000007-0000-4000-8000-000000000007", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "设备归属运维单位，与逻辑归属分型，避免同一对点混用一条边。", "归属单位", "SpaceTree", true, true, false, false, false, "UnitTree", "AssignedTo", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "e0000008-0000-4000-8000-000000000008", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "分布式光伏、储能等设备安装在建筑/站点上。", "安装承载", "SpaceTree", true, true, false, false, false, "SpaceTree", "InstalledIn", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "e0000009-0000-4000-8000-000000000009", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "空间实体相邻（共享边界/距离），用于台区划分与抢修路径分析；对称按无向处理。", "空间邻接", "SpaceTree", true, false, false, false, false, "SpaceTree", "AdjacentTo", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "e0000010-0000-4000-8000-000000000010", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "设备物理位置（站房/楼层/房间），与逻辑归属分离。", "位置所在", "SpaceTree", true, true, false, false, false, "SpaceTree", "LocatedAt", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "e0000011-0000-4000-8000-000000000011", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "保护装置指向被保护设备，用于故障时定位应动作的保护。", "保护范围", "SpaceTree", true, true, false, false, false, "SpaceTree", "Protects", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "e0000012-0000-4000-8000-000000000012", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "上下级保护配合（配合级差与时间定值），支持定值校核与越级跳闸分析。", "保护配合", "SpaceTree", true, true, false, false, false, "SpaceTree", "BacksUp", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "e0000013-0000-4000-8000-000000000013", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "操作票与五防规则的前置约束，例如线路带电时禁止合接地刀闸。", "操作前置", "SpaceTree", true, true, false, false, false, "SpaceTree", "PrerequisiteFor", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "e0000014-0000-4000-8000-000000000014", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "二次设备（保护、测控、通信）依赖一次设备或电源，用于故障溯源。", "二次依赖", "SpaceTree", true, true, false, false, false, "SpaceTree", "DependsOn", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "e0000015-0000-4000-8000-000000000015", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "故障事件节点指向受影响的设备/用户，表达失电或降级范围。", "影响范围", "SpaceTree", true, true, false, false, false, "SpaceTree", "Affects", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "e0000016-0000-4000-8000-000000000016", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "跳闸事件指向触发它的保护动作或根因故障，用于事后复盘。", "事件起因", "SpaceTree", true, true, false, false, false, "SpaceTree", "TrippedBy", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "e0000017-0000-4000-8000-000000000017", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "线路区段 → 交叉跨越点；空间相交但电气隔离，不参与供电拓扑遍历。", "跨越通过", null, true, true, true, false, false, "LineNode", "PassesThrough", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.InsertData(
                table: "knowledge_trees",
                columns: new[] { "id", "can_have_children", "description", "is_archived", "name", "parent_id", "sort_order" },
                values: new object[] { "beef0000-0000-4000-8000-000000000001", true, "根知识库", false, "知识库", null, 0 });

            migrationBuilder.InsertData(
                table: "spaces",
                columns: new[] { "id", "description", "name", "type" },
                values: new object[,]
                {
                    { "7a1e3f5d-9b2c-4a8e-1f0d-3c5b7e9a1c2d", "武汉供电段", "武汉供电段", "Dynamic" },
                    { "8b2f4a6e-0c3d-5b9f-2a1e-4d6c8f0b2d3e", "类型", "类型", "Type" },
                    { "9c3a5b7f-1d4e-6c0a-3b2f-5e7d9a1c3e4f", "单位", "单位", "Unit" },
                    { "ad5e6c8f-2b3d-4a7e-9c0f-1d2b3e4f5a6b", "知识库", "知识库", "Knowledge" }
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
                name: "IX_custom_table_columns_unit_id",
                table: "custom_table_columns",
                column: "unit_id");

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
                name: "uq_edge_definition_type_name",
                table: "edge_definitions",
                column: "type_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ei_from",
                table: "edge_instances",
                columns: new[] { "edge_type_id", "from_node_id" });

            migrationBuilder.CreateIndex(
                name: "ix_ei_from_node",
                table: "edge_instances",
                column: "from_node_id");

            migrationBuilder.CreateIndex(
                name: "ix_ei_to",
                table: "edge_instances",
                columns: new[] { "edge_type_id", "to_node_id" });

            migrationBuilder.CreateIndex(
                name: "ix_ei_to_node",
                table: "edge_instances",
                column: "to_node_id");

            migrationBuilder.CreateIndex(
                name: "uq_edge_instance",
                table: "edge_instances",
                columns: new[] { "edge_type_id", "from_node_id", "to_node_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_ei_single_incoming",
                table: "edge_instances",
                columns: new[] { "to_node_id", "edge_type_id" },
                unique: true,
                filter: "enforce_single_incoming = true");

            migrationBuilder.CreateIndex(
                name: "ix_entity_type_owner_space",
                table: "entity_type_catalog",
                column: "owner_space_id");

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
                name: "ix_knowledge_trees_parent",
                table: "knowledge_trees",
                column: "parent_id");

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
                name: "IX_location_attribute_values_attribute_id",
                table: "location_attribute_values",
                column: "attribute_id");

            migrationBuilder.CreateIndex(
                name: "uq_lav_entity_attr",
                table: "location_attribute_values",
                columns: new[] { "entity_id", "entity_type", "attribute_id" },
                unique: true);

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
                name: "ix_space_trees_parent",
                table: "space_trees",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_unit_trees_parent",
                table: "unit_trees",
                column: "parent_id");

            // attribute_audit_log 为 ExcludeFromMigrations 分区表，EF 不自动生成，此处手写补回
            migrationBuilder.Sql("""
                CREATE TABLE attribute_audit_log (
                    audit_id text NOT NULL DEFAULT gen_random_uuid()::text,
                    changed_at timestamp with time zone NOT NULL,
                    entity_id text NOT NULL,
                    entity_type text NOT NULL,
                    attribute_id text NOT NULL,
                    attribute_name text NOT NULL,
                    old_value text,
                    new_value text,
                    change_type text NOT NULL,
                    changed_by text NOT NULL,
                    correlation_id text,
                    client_ip text,
                    CONSTRAINT pk_attribute_audit_log PRIMARY KEY (audit_id, changed_at)
                ) PARTITION BY RANGE (changed_at);
                """);

            migrationBuilder.Sql("""
                CREATE TABLE attribute_audit_log_default
                    PARTITION OF attribute_audit_log DEFAULT;
                """);

            migrationBuilder.Sql("""
                CREATE INDEX ix_audit_entity ON attribute_audit_log
                    (entity_type, entity_id, changed_at DESC);
                """);

            migrationBuilder.Sql("""
                CREATE INDEX ix_audit_time ON attribute_audit_log (changed_at);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS attribute_audit_log CASCADE;");

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
                name: "edge_instances");

            migrationBuilder.DropTable(
                name: "entity_type_catalog");

            migrationBuilder.DropTable(
                name: "inode_entity");

            migrationBuilder.DropTable(
                name: "inode_entitytype");

            migrationBuilder.DropTable(
                name: "knowledge_trees");

            migrationBuilder.DropTable(
                name: "line_nodes");

            migrationBuilder.DropTable(
                name: "location_attribute_values");

            migrationBuilder.DropTable(
                name: "option_items");

            migrationBuilder.DropTable(
                name: "space_trees");

            migrationBuilder.DropTable(
                name: "spaces");

            migrationBuilder.DropTable(
                name: "edge_definitions");

            migrationBuilder.DropTable(
                name: "attribute_catalog");

            migrationBuilder.DropTable(
                name: "composite_type_definitions");

            migrationBuilder.DropTable(
                name: "custom_table_definitions");

            migrationBuilder.DropTable(
                name: "option_sets");

            migrationBuilder.DropTable(
                name: "unit_trees");
        }
    }
}
