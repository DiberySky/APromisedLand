using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreeGraph.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedSpaceTreeRoot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "space_trees",
                columns: new[] { "id", "abbreviation", "can_have_children", "description", "has_children", "name", "parent_id", "sort_order" },
                values: new object[] { "EBBAD64B-2119-47EB-95C3-01D3DD796774", "", true, "", false, "空间树", null, 0 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "space_trees",
                keyColumn: "id",
                keyValue: "EBBAD64B-2119-47EB-95C3-01D3DD796774");
        }
    }
}
