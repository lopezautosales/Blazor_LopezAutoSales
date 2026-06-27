using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LopezAutoSales.Server.Migrations
{
    /// <inheritdoc />
    public partial class AccountRepossession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRepossessed",
                table: "Accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "RepossessedDate",
                table: "Accounts",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRepossessed",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "RepossessedDate",
                table: "Accounts");
        }
    }
}
