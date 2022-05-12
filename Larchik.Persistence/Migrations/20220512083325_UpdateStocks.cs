using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Larchik.Persistence.Migrations
{
    public partial class UpdateStocks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CompanyName",
                table: "Stocks",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Ticker",
                table: "Stocks",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(6)",
                oldMaxLength: 6);

            migrationBuilder.AlterColumn<string>(
                name: "StockId",
                table: "Deals",
                type: "character varying(8)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(6)");

            migrationBuilder.AlterColumn<string>(
                name: "StockId",
                table: "Assets",
                type: "character varying(8)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(6)");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CompanyName",
                table: "Stocks",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(60)",
                oldMaxLength: 60);

            migrationBuilder.AlterColumn<string>(
                name: "Ticker",
                table: "Stocks",
                type: "character varying(6)",
                maxLength: 6,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(8)",
                oldMaxLength: 8);

            migrationBuilder.AlterColumn<string>(
                name: "StockId",
                table: "Deals",
                type: "character varying(6)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(8)");

            migrationBuilder.AlterColumn<string>(
                name: "StockId",
                table: "Assets",
                type: "character varying(6)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(8)");
        }
    }
}
