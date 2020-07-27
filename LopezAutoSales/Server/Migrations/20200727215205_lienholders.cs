using Microsoft.EntityFrameworkCore.Migrations;

namespace LopezAutoSales.Server.Migrations
{
    public partial class lienholders : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Lienholder_Address_AddressId",
                table: "Lienholder");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Lienholder_LienholderId",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_LienholderId",
                table: "Sales");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Lienholder",
                table: "Lienholder");

            migrationBuilder.DeleteData(
                table: "Lienholder",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DropColumn(
                name: "LienholderId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "Lienholder");

            migrationBuilder.RenameTable(
                name: "Lienholder",
                newName: "Lienholders");

            migrationBuilder.RenameIndex(
                name: "IX_Lienholder_AddressId",
                table: "Lienholders",
                newName: "IX_Lienholders_AddressId");

            migrationBuilder.AddColumn<decimal>(
                name: "FinanceCharge",
                table: "Sales",
                type: "decimal(9,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "LienholderNormalizedName",
                table: "Sales",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Warranty",
                table: "Sales",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "Lienholders",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Lienholders",
                table: "Lienholders",
                column: "NormalizedName");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "2301D884-221A-4E7D-B509-0113DCC043E1",
                column: "ConcurrencyStamp",
                value: "f24177e0-a933-4ac4-9e0f-a20ca6dae665");

            migrationBuilder.InsertData(
                table: "Lienholders",
                columns: new[] { "NormalizedName", "AddressId", "Name" },
                values: new object[] { "LOPEZ AUTO SALES, INC.", 1, "Lopez Auto Sales, Inc." });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_LienholderNormalizedName",
                table: "Sales",
                column: "LienholderNormalizedName");

            migrationBuilder.AddForeignKey(
                name: "FK_Lienholders_Address_AddressId",
                table: "Lienholders",
                column: "AddressId",
                principalTable: "Address",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Lienholders_LienholderNormalizedName",
                table: "Sales",
                column: "LienholderNormalizedName",
                principalTable: "Lienholders",
                principalColumn: "NormalizedName",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Lienholders_Address_AddressId",
                table: "Lienholders");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Lienholders_LienholderNormalizedName",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_LienholderNormalizedName",
                table: "Sales");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Lienholders",
                table: "Lienholders");

            migrationBuilder.DeleteData(
                table: "Lienholders",
                keyColumn: "NormalizedName",
                keyValue: "LOPEZ AUTO SALES, INC.");

            migrationBuilder.DropColumn(
                name: "FinanceCharge",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "LienholderNormalizedName",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "Warranty",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "Lienholders");

            migrationBuilder.RenameTable(
                name: "Lienholders",
                newName: "Lienholder");

            migrationBuilder.RenameIndex(
                name: "IX_Lienholders_AddressId",
                table: "Lienholder",
                newName: "IX_Lienholder_AddressId");

            migrationBuilder.AddColumn<int>(
                name: "LienholderId",
                table: "Sales",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "Lienholder",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Lienholder",
                table: "Lienholder",
                column: "Id");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "2301D884-221A-4E7D-B509-0113DCC043E1",
                column: "ConcurrencyStamp",
                value: "9bf5571f-a3b9-4b05-abfb-cdcb9ffc4ce0");

            migrationBuilder.InsertData(
                table: "Lienholder",
                columns: new[] { "Id", "AddressId", "Name" },
                values: new object[] { 1, 1, "Lopez Auto Sales, Inc." });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_LienholderId",
                table: "Sales",
                column: "LienholderId");

            migrationBuilder.AddForeignKey(
                name: "FK_Lienholder_Address_AddressId",
                table: "Lienholder",
                column: "AddressId",
                principalTable: "Address",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Lienholder_LienholderId",
                table: "Sales",
                column: "LienholderId",
                principalTable: "Lienholder",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
