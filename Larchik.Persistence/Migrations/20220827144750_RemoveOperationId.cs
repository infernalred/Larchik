using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Larchik.Persistence.Migrations
{
    public partial class RemoveOperationId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Deals_Operations_OperationId",
                table: "Deals");

            migrationBuilder.DropIndex(
                name: "IX_Deals_OperationId",
                table: "Deals");

            migrationBuilder.AlterColumn<string>(
                name: "OperationId",
                table: "Deals",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(25)");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "OperationId",
                table: "Deals",
                type: "character varying(25)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_OperationId",
                table: "Deals",
                column: "OperationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Deals_Operations_OperationId",
                table: "Deals",
                column: "OperationId",
                principalTable: "Operations",
                principalColumn: "Code",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
