using Microsoft.EntityFrameworkCore.Migrations;

namespace LopezAutoSales.Server.Migrations
{
    public partial class Picture : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Images");

            migrationBuilder.CreateTable(
                name: "Pictures",
                columns: table => new
                {
                    Id = table.Column<string>(nullable: false),
                    CarId = table.Column<string>(nullable: true),
                    URL = table.Column<string>(nullable: true),
                    IsThumbnail = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pictures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pictures_Cars_CarId",
                        column: x => x.CarId,
                        principalTable: "Cars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "2301D884-221A-4E7D-B509-0113DCC043E1",
                column: "ConcurrencyStamp",
                value: "ba731def-a5e5-4e07-ba24-8e73e06d597a");

            migrationBuilder.CreateIndex(
                name: "IX_Pictures_CarId",
                table: "Pictures",
                column: "CarId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Pictures");

            migrationBuilder.CreateTable(
                name: "Images",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CarId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    IsThumbnail = table.Column<bool>(type: "bit", nullable: false),
                    URL = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Images", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Images_Cars_CarId",
                        column: x => x.CarId,
                        principalTable: "Cars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "2301D884-221A-4E7D-B509-0113DCC043E1",
                column: "ConcurrencyStamp",
                value: "8e06a26c-03f3-4bce-9a9a-acd76aed610e");

            migrationBuilder.CreateIndex(
                name: "IX_Images_CarId",
                table: "Images",
                column: "CarId");
        }
    }
}
