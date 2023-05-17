using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LopezAutoSales.Server.Migrations
{
    /// <inheritdoc />
    public partial class PaperworkFee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PaperworkFee",
                table: "Sales",
                type: "decimal(9,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaperworkFee",
                table: "Sales");
        }
    }
}
