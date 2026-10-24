using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TreeGraph.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RebuildEdgeModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_edge_instances_edge_definitions_edge_type_id",
                table: "edge_instances");

            migrationBuilder.DropForeignKey(
                name: "FK_line_structure_segments_edge_instances_edge_id",
                table: "line_structure_segments");

            migrationBuilder.DropForeignKey(
                name: "FK_line_structures_edge_definitions_edge_type_id",
                table: "line_structures");

            // R4：清空 edge 全部既有数据与种子（不做兼容、不回填）。
            // 先清 EAV/挂载数据，再删边行，最后注销 nodes 中的 EdgeInstance 身份行。
            migrationBuilder.Sql("""
                DELETE FROM inode_entitytype
                 WHERE inode_id IN (SELECT edge_id FROM edge_instances);

                DELETE FROM inode_entity
                 WHERE inode_id IN (SELECT edge_id FROM edge_instances)
                    OR entity_type IN (SELECT type_name FROM edge_definitions);

                DELETE FROM attribute_values
                 WHERE entity_id IN (SELECT edge_id FROM edge_instances)
                    OR entity_type IN (SELECT type_name FROM edge_definitions);

                DELETE FROM location_attribute_values
                 WHERE entity_id IN (SELECT edge_id FROM edge_instances)
                    OR entity_type IN (SELECT type_name FROM edge_definitions);

                DELETE FROM attribute_catalog
                 WHERE entity_type IN (SELECT type_name FROM edge_definitions);

                DELETE FROM entity_type_catalog
                 WHERE entity_type IN (SELECT type_name FROM edge_definitions);

                DELETE FROM edge_instances;

                DELETE FROM nodes WHERE host_type = 'EdgeInstance';
                """);

            migrationBuilder.DropTable(
                name: "edge_definitions");

            migrationBuilder.DropIndex(
                name: "ix_line_structures_edge_type",
                table: "line_structures");

            migrationBuilder.DropIndex(
                name: "ix_ei_from",
                table: "edge_instances");

            migrationBuilder.DropIndex(
                name: "ix_ei_to",
                table: "edge_instances");

            migrationBuilder.DropIndex(
                name: "uq_edge_instance",
                table: "edge_instances");

            migrationBuilder.DropIndex(
                name: "uq_ei_single_incoming",
                table: "edge_instances");

            migrationBuilder.DropColumn(
                name: "edge_type_id",
                table: "line_structures");

            migrationBuilder.DropColumn(
                name: "display_name",
                table: "edge_instances");

            migrationBuilder.DropColumn(
                name: "edge_type_id",
                table: "edge_instances");

            migrationBuilder.DropColumn(
                name: "enforce_single_incoming",
                table: "edge_instances");

            migrationBuilder.DropColumn(
                name: "weight",
                table: "edge_instances");

            migrationBuilder.AddColumn<string>(
                name: "display_name",
                table: "line_structure_segments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "weight",
                table: "line_structure_segments",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_directed",
                table: "edge_instances",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "type_name",
                table: "edge_instances",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "uq_edge_instance",
                table: "edge_instances",
                columns: new[] { "type_name", "from_node_id", "to_node_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_ei_from_node",
                table: "edge_instances",
                column: "from_node_id",
                principalTable: "nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_ei_to_node",
                table: "edge_instances",
                column: "to_node_id",
                principalTable: "nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 本迁移是不可逆的破坏性重构（edge_definitions 已删、边数据已清空）。
            // 仅尽力恢复旧表结构，不恢复任何数据；回滚后需手工重建类型与边。
            migrationBuilder.DropForeignKey(
                name: "fk_ei_from_node",
                table: "edge_instances");

            migrationBuilder.DropForeignKey(
                name: "fk_ei_to_node",
                table: "edge_instances");

            migrationBuilder.DropIndex(
                name: "uq_edge_instance",
                table: "edge_instances");

            migrationBuilder.DropColumn(
                name: "display_name",
                table: "line_structure_segments");

            migrationBuilder.DropColumn(
                name: "weight",
                table: "line_structure_segments");

            migrationBuilder.DropColumn(
                name: "is_directed",
                table: "edge_instances");

            migrationBuilder.DropColumn(
                name: "type_name",
                table: "edge_instances");

            migrationBuilder.AddColumn<string>(
                name: "edge_type_id",
                table: "line_structures",
                type: "character varying(36)",
                maxLength: 36,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "display_name",
                table: "edge_instances",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "edge_type_id",
                table: "edge_instances",
                type: "character varying(36)",
                maxLength: 36,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "enforce_single_incoming",
                table: "edge_instances",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "weight",
                table: "edge_instances",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "edge_definitions",
                columns: table => new
                {
                    edge_type_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false, defaultValueSql: "gen_random_uuid()::text"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    from_node_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_directed = table.Column<bool>(type: "boolean", nullable: false),
                    is_global = table.Column<bool>(type: "boolean", nullable: false),
                    IsLine = table.Column<bool>(type: "boolean", nullable: false),
                    IsLinear = table.Column<bool>(type: "boolean", nullable: false),
                    to_node_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    type_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_edge_definitions", x => x.edge_type_id);
                });

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
                    { "e0000017-0000-4000-8000-000000000017", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "线路区段 → 交叉跨越点；空间相交但电气隔离，不参与供电拓扑遍历。", "跨越通过", null, true, true, true, false, false, "LineNode", "PassesThrough", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "e0000018-0000-4000-8000-000000000018", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "两个跨越点（LineNode）之间的线路区段；无向对称，属线路结构，可串联成完整路径，允许 T 接分支。", "线路段", "LineNode", true, false, false, true, false, "LineNode", "LineSegment", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.CreateIndex(
                name: "ix_line_structures_edge_type",
                table: "line_structures",
                column: "edge_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_ei_from",
                table: "edge_instances",
                columns: new[] { "edge_type_id", "from_node_id" });

            migrationBuilder.CreateIndex(
                name: "ix_ei_to",
                table: "edge_instances",
                columns: new[] { "edge_type_id", "to_node_id" });

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
                name: "uq_edge_definition_type_name",
                table: "edge_definitions",
                column: "type_name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_edge_instances_edge_definitions_edge_type_id",
                table: "edge_instances",
                column: "edge_type_id",
                principalTable: "edge_definitions",
                principalColumn: "edge_type_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_line_structure_segments_edge_instances_edge_id",
                table: "line_structure_segments",
                column: "edge_id",
                principalTable: "edge_instances",
                principalColumn: "edge_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_line_structures_edge_definitions_edge_type_id",
                table: "line_structures",
                column: "edge_type_id",
                principalTable: "edge_definitions",
                principalColumn: "edge_type_id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
