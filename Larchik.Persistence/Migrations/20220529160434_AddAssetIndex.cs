using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Larchik.Persistence.Migrations
{
    public partial class AddAssetIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Assets_AccountId",
                table: "Assets");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_AccountId_StockId",
                table: "Assets",
                columns: new[] { "AccountId", "StockId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Assets_AccountId_StockId",
                table: "Assets");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_AccountId",
                table: "Assets",
                column: "AccountId");
        }
    }
}
