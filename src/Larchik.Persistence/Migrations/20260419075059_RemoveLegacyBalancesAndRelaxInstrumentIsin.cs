using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Larchik.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyBalancesAndRelaxInstrumentIsin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cash_balances");

            migrationBuilder.DropTable(
                name: "lots");

            migrationBuilder.DropColumn(
                name: "price",
                table: "instruments");

            migrationBuilder.AlterColumn<string>(
                name: "isin",
                table: "instruments",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(12)",
                oldMaxLength: 12);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "isin",
                table: "instruments",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(12)",
                oldMaxLength: 12,
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "price",
                table: "instruments",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cash_balances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_id = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    portfolio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cash_balances", x => x.id);
                    table.ForeignKey(
                        name: "fk_cash_balances_currencies_currency_id",
                        column: x => x.currency_id,
                        principalTable: "currencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_cash_balances_portfolios_portfolio_id",
                        column: x => x.portfolio_id,
                        principalTable: "portfolios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_id = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    instrument_id = table.Column<Guid>(type: "uuid", nullable: false),
                    portfolio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    method = table.Column<int>(type: "integer", nullable: false),
                    opened_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    remaining_quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lots", x => x.id);
                    table.ForeignKey(
                        name: "fk_lots_currencies_currency_id",
                        column: x => x.currency_id,
                        principalTable: "currencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_lots_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalTable: "instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_lots_portfolios_portfolio_id",
                        column: x => x.portfolio_id,
                        principalTable: "portfolios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cash_balances_currency_id",
                table: "cash_balances",
                column: "currency_id");

            migrationBuilder.CreateIndex(
                name: "ix_cash_balances_portfolio_id_currency_id",
                table: "cash_balances",
                columns: new[] { "portfolio_id", "currency_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lots_currency_id",
                table: "lots",
                column: "currency_id");

            migrationBuilder.CreateIndex(
                name: "ix_lots_instrument_id",
                table: "lots",
                column: "instrument_id");

            migrationBuilder.CreateIndex(
                name: "ix_lots_portfolio_id_instrument_id_method",
                table: "lots",
                columns: new[] { "portfolio_id", "instrument_id", "method" });
        }
    }
}
