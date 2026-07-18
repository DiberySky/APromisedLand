using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace APromisedLand.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SampleSeeds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "CategoryTrees",
                columns: new[] { "Id", "HasChildren", "Name", "ParentId" },
                values: new object[,]
                {
                    { "55705350-7071-43A4-AFAF-2F30B3CE2718", false, "Sample Root", null },
                    { "27EE32B0-0F30-4331-AA85-61457B7A0912", false, "Sample 3", "55705350-7071-43A4-AFAF-2F30B3CE2718" },
                    { "39EA6315-0A74-40F6-A096-8E15CCC98579", false, "Sample 1", "55705350-7071-43A4-AFAF-2F30B3CE2718" },
                    { "C8969ED0-C018-4FDC-AE55-C363BD95C853", false, "Sample 2", "55705350-7071-43A4-AFAF-2F30B3CE2718" },
                    { "1B16336D-FB7F-42AA-AFD2-F78388883336", false, "Sample 3.3", "27EE32B0-0F30-4331-AA85-61457B7A0912" },
                    { "5C30BACA-3C11-4677-8123-8EC2BE729667", false, "Sample 3.2", "27EE32B0-0F30-4331-AA85-61457B7A0912" },
                    { "8E971C0E-B99A-4931-AFD6-46E44D6ECE5A", false, "Sample 3.1", "27EE32B0-0F30-4331-AA85-61457B7A0912" },
                    { "975599CC-B967-4AD2-B4B8-9E00D889FB4D", false, "Sample 2.2", "C8969ED0-C018-4FDC-AE55-C363BD95C853" },
                    { "C1EBEE10-97F6-44C8-9852-2F574515BF51", false, "Sample 1.1", "39EA6315-0A74-40F6-A096-8E15CCC98579" },
                    { "ED0990CF-5BB7-4C36-A3BB-3AF606AD1974", false, "Sample 2.1", "C8969ED0-C018-4FDC-AE55-C363BD95C853" },
                    { "35F02829-6490-467E-9D3E-C2EBF0EAA2B4", false, "Sample 3.3.1", "1B16336D-FB7F-42AA-AFD2-F78388883336" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "CategoryTrees",
                keyColumn: "Id",
                keyValue: "35F02829-6490-467E-9D3E-C2EBF0EAA2B4");

            migrationBuilder.DeleteData(
                table: "CategoryTrees",
                keyColumn: "Id",
                keyValue: "5C30BACA-3C11-4677-8123-8EC2BE729667");

            migrationBuilder.DeleteData(
                table: "CategoryTrees",
                keyColumn: "Id",
                keyValue: "8E971C0E-B99A-4931-AFD6-46E44D6ECE5A");

            migrationBuilder.DeleteData(
                table: "CategoryTrees",
                keyColumn: "Id",
                keyValue: "975599CC-B967-4AD2-B4B8-9E00D889FB4D");

            migrationBuilder.DeleteData(
                table: "CategoryTrees",
                keyColumn: "Id",
                keyValue: "C1EBEE10-97F6-44C8-9852-2F574515BF51");

            migrationBuilder.DeleteData(
                table: "CategoryTrees",
                keyColumn: "Id",
                keyValue: "ED0990CF-5BB7-4C36-A3BB-3AF606AD1974");

            migrationBuilder.DeleteData(
                table: "CategoryTrees",
                keyColumn: "Id",
                keyValue: "1B16336D-FB7F-42AA-AFD2-F78388883336");

            migrationBuilder.DeleteData(
                table: "CategoryTrees",
                keyColumn: "Id",
                keyValue: "39EA6315-0A74-40F6-A096-8E15CCC98579");

            migrationBuilder.DeleteData(
                table: "CategoryTrees",
                keyColumn: "Id",
                keyValue: "C8969ED0-C018-4FDC-AE55-C363BD95C853");

            migrationBuilder.DeleteData(
                table: "CategoryTrees",
                keyColumn: "Id",
                keyValue: "27EE32B0-0F30-4331-AA85-61457B7A0912");

            migrationBuilder.DeleteData(
                table: "CategoryTrees",
                keyColumn: "Id",
                keyValue: "55705350-7071-43A4-AFAF-2F30B3CE2718");
        }
    }
}
