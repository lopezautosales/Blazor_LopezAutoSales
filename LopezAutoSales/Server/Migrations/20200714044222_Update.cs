using Microsoft.EntityFrameworkCore.Migrations;

namespace LopezAutoSales.Server.Migrations
{
    public partial class Update : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsThumbnail",
                table: "Images",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsListed",
                table: "Cars",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "ListPrice",
                table: "Cars",
                type: "money",
                nullable: false,
                defaultValue: 0m);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsThumbnail",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "IsListed",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "ListPrice",
                table: "Cars");
        }
    }
}
