using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Larchik.Persistence.Migrations
{
    public partial class DealTypeId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Deals_TypeId",
                table: "Deals",
                column: "TypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Deals_DealTypes_TypeId",
                table: "Deals",
                column: "TypeId",
                principalTable: "DealTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Deals_DealTypes_TypeId",
                table: "Deals");

            migrationBuilder.DropIndex(
                name: "IX_Deals_TypeId",
                table: "Deals");
        }
    }
}
