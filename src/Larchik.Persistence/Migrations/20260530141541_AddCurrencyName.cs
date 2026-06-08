using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Larchik.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrencyName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "currencies",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "currencies",
                keyColumn: "id",
                keyValue: "EUR",
                column: "name",
                value: "Евро");

            migrationBuilder.UpdateData(
                table: "currencies",
                keyColumn: "id",
                keyValue: "RUB",
                column: "name",
                value: "Российский рубль");

            migrationBuilder.UpdateData(
                table: "currencies",
                keyColumn: "id",
                keyValue: "USD",
                column: "name",
                value: "Доллар США");

            migrationBuilder.Sql("""
                UPDATE currencies
                SET name = id
                WHERE name = '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "name",
                table: "currencies");
        }
    }
}
