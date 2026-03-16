using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Larchik.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInstrumentListingHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "source_currency_id",
                table: "prices",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "instrument_listing_histories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instrument_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticker = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    figi = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    currency_id = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    exchange = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instrument_listing_histories", x => x.id);
                    table.ForeignKey(
                        name: "fk_instrument_listing_histories_currencies_currency_id",
                        column: x => x.currency_id,
                        principalTable: "currencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_instrument_listing_histories_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalTable: "instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_instrument_listing_histories_currency_id",
                table: "instrument_listing_histories",
                column: "currency_id");

            migrationBuilder.CreateIndex(
                name: "ix_instrument_listing_histories_instrument_id_effective_from",
                table: "instrument_listing_histories",
                columns: new[] { "instrument_id", "effective_from" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_instrument_listing_histories_instrument_id_effective_to",
                table: "instrument_listing_histories",
                columns: new[] { "instrument_id", "effective_to" });

            migrationBuilder.Sql("""
                update prices
                set source_currency_id = currency_id
                where source_currency_id is null;
                """);

            migrationBuilder.Sql("""
                insert into instrument_listing_histories (
                    id,
                    instrument_id,
                    ticker,
                    figi,
                    currency_id,
                    exchange,
                    effective_from,
                    effective_to,
                    created_at,
                    updated_at
                )
                select
                    i.id,
                    i.id,
                    i.ticker,
                    i.figi,
                    i.currency_id,
                    i.exchange,
                    timestamp with time zone '1900-01-01 00:00:00+00',
                    null,
                    now(),
                    now()
                from instruments i
                where not exists (
                    select 1
                    from instrument_listing_histories h
                    where h.instrument_id = i.id
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "instrument_listing_histories");

            migrationBuilder.DropColumn(
                name: "source_currency_id",
                table: "prices");
        }
    }
}
