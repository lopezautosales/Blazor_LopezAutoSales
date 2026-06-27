using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LopezAutoSales.Server.Migrations
{
    /// <inheritdoc />
    public partial class SaleDateIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Sales_Date",
                table: "Sales",
                column: "Date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sales_Date",
                table: "Sales");
        }
    }
}
