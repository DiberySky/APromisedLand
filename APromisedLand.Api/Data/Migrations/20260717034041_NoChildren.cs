using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APromisedLand.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class NoChildren : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasChildren",
                table: "CategoryTrees");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasChildren",
                table: "CategoryTrees",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "CategoryTrees",
                keyColumn: "Id",
                keyValue: "1B16336D-FB7F-42AA-AFD2-F78388883336",
                column: "HasChildren",
                value: false);

            migrationBuilder.UpdateData(
                table: "CategoryTrees",
                keyColumn: "Id",
                keyValue: "27EE32B0-0F30-4331-AA85-61457B7A0912",
                column: "HasChildren",
                value: false);

            migrationBuilder.UpdateData(
                table: "CategoryTrees",
                keyColumn: "Id",
                keyValue: "35F02829-6490-467E-9D3E-C2EBF0EAA2B4",
                column: "HasChildren",
                value: false);

            migrationBuilder.UpdateData(
                table: "CategoryTrees",
                keyColumn: "Id",
                keyValue: "39EA6315-0A74-40F6-A096-8E15CCC98579",
                column: "HasChildren",
                value: false);

            migrationBuilder.UpdateData(
                table: "CategoryTrees",
                keyColumn: "Id",
                keyValue: "55705350-7071-43A4-AFAF-2F30B3CE2718",
                column: "HasChildren",
                value: false);

            migrationBuilder.UpdateData(
                table: "CategoryTrees",
                keyColumn: "Id",
                keyValue: "5C30BACA-3C11-4677-8123-8EC2BE729667",
                column: "HasChildren",
                value: false);

            migrationBuilder.UpdateData(
                table: "CategoryTrees",
                keyColumn: "Id",
                keyValue: "8E971C0E-B99A-4931-AFD6-46E44D6ECE5A",
                column: "HasChildren",
                value: false);

            migrationBuilder.UpdateData(
                table: "CategoryTrees",
                keyColumn: "Id",
                keyValue: "975599CC-B967-4AD2-B4B8-9E00D889FB4D",
                column: "HasChildren",
                value: false);

            migrationBuilder.UpdateData(
                table: "CategoryTrees",
                keyColumn: "Id",
                keyValue: "C1EBEE10-97F6-44C8-9852-2F574515BF51",
                column: "HasChildren",
                value: false);

            migrationBuilder.UpdateData(
                table: "CategoryTrees",
                keyColumn: "Id",
                keyValue: "C8969ED0-C018-4FDC-AE55-C363BD95C853",
                column: "HasChildren",
                value: false);

            migrationBuilder.UpdateData(
                table: "CategoryTrees",
                keyColumn: "Id",
                keyValue: "ED0990CF-5BB7-4C36-A3BB-3AF606AD1974",
                column: "HasChildren",
                value: false);
        }
    }
}
