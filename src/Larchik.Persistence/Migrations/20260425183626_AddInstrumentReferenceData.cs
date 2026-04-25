using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Larchik.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInstrumentReferenceData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_instruments_currencies_currency_id",
                table: "instruments");

            migrationBuilder.CreateTable(
                name: "countries",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_countries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "exchanges",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exchanges", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "countries",
                columns: new[] { "id", "name" },
                values: new object[,]
                {
                    { "CH", "Switzerland" },
                    { "CN", "China" },
                    { "DE", "Germany" },
                    { "GB", "United Kingdom" },
                    { "HK", "Hong Kong" },
                    { "IE", "Ireland" },
                    { "KZ", "Kazakhstan" },
                    { "NL", "Netherlands" },
                    { "RU", "Russia" },
                    { "US", "United States" }
                });

            migrationBuilder.InsertData(
                table: "exchanges",
                columns: new[] { "id", "name" },
                values: new object[,]
                {
                    { "HKEX", "Hong Kong Exchange" },
                    { "LSE", "London Stock Exchange" },
                    { "MOEX", "Moscow Exchange" },
                    { "NASDAQ", "Nasdaq" },
                    { "NYSE", "New York Stock Exchange" },
                    { "SPBX", "SPB Exchange" },
                    { "TEST", "Test Exchange" }
                });

            migrationBuilder.Sql(
                """
                update instruments
                set country = case
                    when nullif(trim(country), '') is null then null
                    when upper(trim(country)) in ('RU', 'RUS', 'RUSSIA', 'РОССИЯ', 'RUSSIAN FEDERATION') then 'RU'
                    when upper(trim(country)) in ('US', 'USA', 'UNITED STATES', 'UNITED STATES OF AMERICA') then 'US'
                    when upper(trim(country)) in ('NL', 'NETHERLANDS') then 'NL'
                    when length(upper(trim(country))) = 2 then upper(trim(country))
                    else null
                end;

                update instruments
                set exchange = case
                    when nullif(trim(exchange), '') is null then null
                    when upper(trim(exchange)) in ('TQBR', 'TQTF', 'TQIF', 'TQCB', 'TQOB', 'CETS', 'MTQR', 'CNGD') then 'MOEX'
                    else left(upper(trim(exchange)), 16)
                end;

                update instrument_listing_histories
                set exchange = case
                    when nullif(trim(exchange), '') is null then null
                    when upper(trim(exchange)) in ('TQBR', 'TQTF', 'TQIF', 'TQCB', 'TQOB', 'CETS', 'MTQR', 'CNGD') then 'MOEX'
                    else left(upper(trim(exchange)), 16)
                end;

                insert into countries (id, name)
                select distinct country, country
                from instruments
                where country is not null
                on conflict (id) do nothing;

                insert into exchanges (id, name)
                select distinct exchange, exchange
                from (
                    select exchange from instruments
                    union
                    select exchange from instrument_listing_histories
                ) x
                where exchange is not null
                on conflict (id) do nothing;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "exchange",
                table: "instruments",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "country",
                table: "instruments",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "exchange",
                table: "instrument_listing_histories",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_instruments_country_id",
                table: "instruments",
                column: "country");

            migrationBuilder.CreateIndex(
                name: "ix_instruments_exchange_id",
                table: "instruments",
                column: "exchange");

            migrationBuilder.CreateIndex(
                name: "ix_instrument_listing_histories_exchange_id",
                table: "instrument_listing_histories",
                column: "exchange");

            migrationBuilder.AddForeignKey(
                name: "fk_instrument_listing_histories_exchanges_exchange_id",
                table: "instrument_listing_histories",
                column: "exchange",
                principalTable: "exchanges",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_instruments_countries_country_id",
                table: "instruments",
                column: "country",
                principalTable: "countries",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_instruments_currencies_currency_id",
                table: "instruments",
                column: "currency_id",
                principalTable: "currencies",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_instruments_exchanges_exchange_id",
                table: "instruments",
                column: "exchange",
                principalTable: "exchanges",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_instrument_listing_histories_exchanges_exchange_id",
                table: "instrument_listing_histories");

            migrationBuilder.DropForeignKey(
                name: "fk_instruments_countries_country_id",
                table: "instruments");

            migrationBuilder.DropForeignKey(
                name: "fk_instruments_currencies_currency_id",
                table: "instruments");

            migrationBuilder.DropForeignKey(
                name: "fk_instruments_exchanges_exchange_id",
                table: "instruments");

            migrationBuilder.DropTable(
                name: "countries");

            migrationBuilder.DropTable(
                name: "exchanges");

            migrationBuilder.DropIndex(
                name: "ix_instruments_country_id",
                table: "instruments");

            migrationBuilder.DropIndex(
                name: "ix_instruments_exchange_id",
                table: "instruments");

            migrationBuilder.DropIndex(
                name: "ix_instrument_listing_histories_exchange_id",
                table: "instrument_listing_histories");

            migrationBuilder.AlterColumn<string>(
                name: "exchange",
                table: "instruments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "country",
                table: "instruments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2)",
                oldMaxLength: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "exchange",
                table: "instrument_listing_histories",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "fk_instruments_currencies_currency_id",
                table: "instruments",
                column: "currency_id",
                principalTable: "currencies",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
