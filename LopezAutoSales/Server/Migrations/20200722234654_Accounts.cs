using Microsoft.EntityFrameworkCore.Migrations;

namespace LopezAutoSales.Server.Migrations
{
    public partial class Accounts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Sales_SaleId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Cars_TradeInId1",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_TradeInId1",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Payments_SaleId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "IsPaid",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "MonthlyPayment",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "TradeInId1",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "SaleId",
                table: "Payments");

            migrationBuilder.AlterColumn<int>(
                name: "TradeInId",
                table: "Sales",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TaxRate",
                table: "Sales",
                type: "decimal(5,3)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,5)");

            migrationBuilder.AlterColumn<decimal>(
                name: "SellingPrice",
                table: "Sales",
                type: "decimal(9,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "money");

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "Sales",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "DownPayment",
                table: "Sales",
                type: "decimal(9,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "money");

            migrationBuilder.AlterColumn<string>(
                name: "Buyer",
                table: "Sales",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccountId",
                table: "Sales",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasLien",
                table: "Sales",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "LienAmount",
                table: "Sales",
                type: "decimal(9,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TagAmount",
                table: "Sales",
                type: "decimal(9,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "Payments",
                type: "decimal(9,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "money");

            migrationBuilder.AddColumn<int>(
                name: "AccountId",
                table: "Payments",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Lienholder",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ListPrice",
                table: "Cars",
                type: "decimal(9,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "money");

            migrationBuilder.AlterColumn<decimal>(
                name: "BoughtPrice",
                table: "Cars",
                type: "decimal(9,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "money",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "Account",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsPaid = table.Column<bool>(nullable: false),
                    InitialDue = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    MonthlyPayment = table.Column<decimal>(type: "decimal(9,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Account", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "2301D884-221A-4E7D-B509-0113DCC043E1",
                column: "ConcurrencyStamp",
                value: "9bf5571f-a3b9-4b05-abfb-cdcb9ffc4ce0");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_AccountId",
                table: "Sales",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_TradeInId",
                table: "Sales",
                column: "TradeInId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_AccountId",
                table: "Payments",
                column: "AccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Account_AccountId",
                table: "Payments",
                column: "AccountId",
                principalTable: "Account",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Account_AccountId",
                table: "Sales",
                column: "AccountId",
                principalTable: "Account",
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
                name: "FK_Payments_Account_AccountId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Account_AccountId",
                table: "Sales");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Cars_TradeInId",
                table: "Sales");

            migrationBuilder.DropTable(
                name: "Account");

            migrationBuilder.DropIndex(
                name: "IX_Sales_AccountId",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_TradeInId",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Payments_AccountId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "HasLien",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "LienAmount",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "TagAmount",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "Payments");

            migrationBuilder.AlterColumn<string>(
                name: "TradeInId",
                table: "Sales",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TaxRate",
                table: "Sales",
                type: "decimal(5,5)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,3)");

            migrationBuilder.AlterColumn<decimal>(
                name: "SellingPrice",
                table: "Sales",
                type: "money",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,2)");

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "Sales",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string));

            migrationBuilder.AlterColumn<decimal>(
                name: "DownPayment",
                table: "Sales",
                type: "money",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,2)");

            migrationBuilder.AlterColumn<string>(
                name: "Buyer",
                table: "Sales",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string));

            migrationBuilder.AddColumn<bool>(
                name: "IsPaid",
                table: "Sales",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyPayment",
                table: "Sales",
                type: "money",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "TradeInId1",
                table: "Sales",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "Payments",
                type: "money",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,2)");

            migrationBuilder.AddColumn<int>(
                name: "SaleId",
                table: "Payments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Lienholder",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string));

            migrationBuilder.AlterColumn<decimal>(
                name: "ListPrice",
                table: "Cars",
                type: "money",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "BoughtPrice",
                table: "Cars",
                type: "money",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,2)",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "2301D884-221A-4E7D-B509-0113DCC043E1",
                column: "ConcurrencyStamp",
                value: "82d4f977-f5ff-4419-b7aa-bb8091b1514d");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_TradeInId1",
                table: "Sales",
                column: "TradeInId1");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_SaleId",
                table: "Payments",
                column: "SaleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Sales_SaleId",
                table: "Payments",
                column: "SaleId",
                principalTable: "Sales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Cars_TradeInId1",
                table: "Sales",
                column: "TradeInId1",
                principalTable: "Cars",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
