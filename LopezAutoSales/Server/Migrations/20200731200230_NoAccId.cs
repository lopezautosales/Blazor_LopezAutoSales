using Microsoft.EntityFrameworkCore.Migrations;

namespace LopezAutoSales.Server.Migrations
{
    public partial class NoAccId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "Sales");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "2301D884-221A-4E7D-B509-0113DCC043E1",
                column: "ConcurrencyStamp",
                value: "6e734a4f-0014-41c9-a199-b5f746db0666");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccountId",
                table: "Sales",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "2301D884-221A-4E7D-B509-0113DCC043E1",
                column: "ConcurrencyStamp",
                value: "53f49ad1-4904-4e1c-9c0e-b79c74480678");
        }
    }
}
