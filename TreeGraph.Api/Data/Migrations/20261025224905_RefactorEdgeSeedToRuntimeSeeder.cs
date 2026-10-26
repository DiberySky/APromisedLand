using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreeGraph.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefactorEdgeSeedToRuntimeSeeder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "edge_type_props",
                keyColumns: new[] { "edge_type_name", "prop_name" },
                keyValues: new object[] { "line_segment", "name" });

            migrationBuilder.DeleteData(
                table: "edge_types",
                keyColumn: "name",
                keyValue: "line_segment");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "edge_types",
                columns: new[] { "name", "comment", "created_at", "display_name", "ttl_col", "ttl_duration_seconds", "updated_at" },
                values: new object[] { "line_segment", "线路结构段边（内置：LineNode → LineNode 链边）", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.InsertData(
                table: "edge_type_props",
                columns: new[] { "edge_type_name", "prop_name", "comment", "data_type", "default_value", "display_name", "display_order", "nullable" },
                values: new object[] { "line_segment", "name", "段边展示名", "STRING", null, null, 0, true });
        }
    }
}
