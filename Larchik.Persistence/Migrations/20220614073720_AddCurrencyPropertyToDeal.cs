using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Larchik.Persistence.Migrations
{
    public partial class AddCurrencyPropertyToDeal : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CurrencyId",
                table: "Deals",
                type: "character varying(5)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_CurrencyId",
                table: "Deals",
                column: "CurrencyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Deals_Currencies_CurrencyId",
                table: "Deals",
                column: "CurrencyId",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Deals_Currencies_CurrencyId",
                table: "Deals");

            migrationBuilder.DropIndex(
                name: "IX_Deals_CurrencyId",
                table: "Deals");

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyId",
                table: "Deals",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(5)");
        }
    }
}
