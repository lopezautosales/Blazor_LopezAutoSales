using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LopezAutoSales.Server.Migrations
{
    public partial class Car : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Images_Cars_CarVIN_CarDate",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Cars_CarVIN_CarDate",
                table: "Sales");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Cars_TradeInVIN_TradeInDate",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_CarVIN_CarDate",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_TradeInVIN_TradeInDate",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Images_CarVIN_CarDate",
                table: "Images");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Cars",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "BoughtPrice",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CarDate",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CarVIN",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "TradeInDate",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "TradeInVIN",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CarDate",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "CarVIN",
                table: "Images");

            migrationBuilder.AddColumn<string>(
                name: "CarId",
                table: "Sales",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TradeInId",
                table: "Sales",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CarId",
                table: "Images",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "VIN",
                table: "Cars",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<string>(
                name: "Id",
                table: "Cars",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "BoughtPrice",
                table: "Cars",
                type: "money",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Cars",
                table: "Cars",
                column: "Id");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "2301D884-221A-4E7D-B509-0113DCC043E1",
                column: "ConcurrencyStamp",
                value: "ccc973ac-11ac-412e-a518-37d4f0a954d8");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CarId",
                table: "Sales",
                column: "CarId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_TradeInId",
                table: "Sales",
                column: "TradeInId");

            migrationBuilder.CreateIndex(
                name: "IX_Images_CarId",
                table: "Images",
                column: "CarId");

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Cars_CarId",
                table: "Images",
                column: "CarId",
                principalTable: "Cars",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Cars_CarId",
                table: "Sales",
                column: "CarId",
                principalTable: "Cars",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Cars_TradeInId",
                table: "Sales",
                column: "TradeInId",
                principalTable: "Cars",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Images_Cars_CarId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Cars_CarId",
                table: "Sales");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Cars_TradeInId",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_CarId",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_TradeInId",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Images_CarId",
                table: "Images");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Cars",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "CarId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "TradeInId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CarId",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "BoughtPrice",
                table: "Cars");

            migrationBuilder.AddColumn<decimal>(
                name: "BoughtPrice",
                table: "Sales",
                type: "money",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CarDate",
                table: "Sales",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CarVIN",
                table: "Sales",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TradeInDate",
                table: "Sales",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TradeInVIN",
                table: "Sales",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CarDate",
                table: "Images",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CarVIN",
                table: "Images",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "VIN",
                table: "Cars",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Cars",
                table: "Cars",
                columns: new[] { "VIN", "Date" });

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "2301D884-221A-4E7D-B509-0113DCC043E1",
                column: "ConcurrencyStamp",
                value: "75670a77-6847-410c-8840-af7827dfd3cb");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CarVIN_CarDate",
                table: "Sales",
                columns: new[] { "CarVIN", "CarDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_TradeInVIN_TradeInDate",
                table: "Sales",
                columns: new[] { "TradeInVIN", "TradeInDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Images_CarVIN_CarDate",
                table: "Images",
                columns: new[] { "CarVIN", "CarDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Cars_CarVIN_CarDate",
                table: "Images",
                columns: new[] { "CarVIN", "CarDate" },
                principalTable: "Cars",
                principalColumns: new[] { "VIN", "Date" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Cars_CarVIN_CarDate",
                table: "Sales",
                columns: new[] { "CarVIN", "CarDate" },
                principalTable: "Cars",
                principalColumns: new[] { "VIN", "Date" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Cars_TradeInVIN_TradeInDate",
                table: "Sales",
                columns: new[] { "TradeInVIN", "TradeInDate" },
                principalTable: "Cars",
                principalColumns: new[] { "VIN", "Date" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
