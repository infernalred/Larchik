using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Larchik.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInstrumentPriceSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "price_source",
                table: "instruments",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.Sql(
                """
                update instruments
                set price_source = case
                    when not is_trading then null
                    when type in (1, 2, 3, 4)
                         and (
                             upper(coalesce(country, '')) = 'RU'
                             or upper(coalesce(isin, '')) like 'RU%'
                             or upper(coalesce(exchange, '')) in ('TQBR', 'TQTF', 'TQIF', 'TQCB', 'TQOB', 'CETS', 'MTQR', 'CNGD')
                         ) then 'MOEX'
                    when type in (1, 2, 3, 4)
                         and coalesce(figi, '') <> '' then 'TBANK'
                    else null
                end;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "price_source",
                table: "instruments");
        }
    }
}
