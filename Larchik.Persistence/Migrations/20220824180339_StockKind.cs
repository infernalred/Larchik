using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Larchik.Persistence.Migrations
{
    public partial class StockKind : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Stocks_StockTypes_TypeId",
                table: "Stocks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_StockTypes",
                table: "StockTypes");

            migrationBuilder.DropIndex(
                name: "IX_Stocks_TypeId",
                table: "Stocks");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "StockTypes",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<string>(
                name: "TypeId",
                table: "Stocks",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(25)");

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Stocks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_StockTypes",
                table: "StockTypes",
                column: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_StockTypes",
                table: "StockTypes");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "StockTypes");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Stocks");

            migrationBuilder.AlterColumn<string>(
                name: "TypeId",
                table: "Stocks",
                type: "character varying(25)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StockTypes",
                table: "StockTypes",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_TypeId",
                table: "Stocks",
                column: "TypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Stocks_StockTypes_TypeId",
                table: "Stocks",
                column: "TypeId",
                principalTable: "StockTypes",
                principalColumn: "Code",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
