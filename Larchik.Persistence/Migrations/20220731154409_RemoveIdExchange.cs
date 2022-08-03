using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Larchik.Persistence.Migrations
{
    public partial class RemoveIdExchange : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Exchanges",
                table: "Exchanges");

            migrationBuilder.DropIndex(
                name: "IX_Exchanges_Code_Date",
                table: "Exchanges");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "Exchanges");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Exchanges",
                table: "Exchanges",
                columns: new[] { "Code", "Date" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Exchanges",
                table: "Exchanges");

            migrationBuilder.AddColumn<long>(
                name: "Id",
                table: "Exchanges",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Exchanges",
                table: "Exchanges",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Exchanges_Code_Date",
                table: "Exchanges",
                columns: new[] { "Code", "Date" });
        }
    }
}
