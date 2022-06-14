using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Larchik.Persistence.Migrations
{
    public partial class StockIdIsNotRequired : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Deals_Stocks_StockId",
                table: "Deals");

            migrationBuilder.AlterColumn<string>(
                name: "StockId",
                table: "Deals",
                type: "character varying(8)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(8)");

            migrationBuilder.AddForeignKey(
                name: "FK_Deals_Stocks_StockId",
                table: "Deals",
                column: "StockId",
                principalTable: "Stocks",
                principalColumn: "Ticker");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Deals_Stocks_StockId",
                table: "Deals");

            migrationBuilder.AlterColumn<string>(
                name: "StockId",
                table: "Deals",
                type: "character varying(8)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(8)",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Deals_Stocks_StockId",
                table: "Deals",
                column: "StockId",
                principalTable: "Stocks",
                principalColumn: "Ticker",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
