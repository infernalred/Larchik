using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Larchik.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSelectionIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_fx_rates_source_date",
                table: "fx_rates",
                columns: new[] { "source", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_instrument_corporate_actions_instrument_id_effective_date",
                table: "instrument_corporate_actions",
                columns: new[] { "instrument_id", "effective_date" });

            migrationBuilder.CreateIndex(
                name: "ix_instruments_price_source_type_is_trading",
                table: "instruments",
                columns: new[] { "price_source", "type", "is_trading" });

            migrationBuilder.DropIndex(
                name: "ix_operations_portfolio_id_trade_date",
                table: "operations");

            migrationBuilder.CreateIndex(
                name: "ix_operations_portfolio_id_instrument_id_trade_date",
                table: "operations",
                columns: new[] { "portfolio_id", "instrument_id", "trade_date" });

            migrationBuilder.CreateIndex(
                name: "ix_operations_portfolio_id_trade_date_created_at",
                table: "operations",
                columns: new[] { "portfolio_id", "trade_date", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_fx_rates_source_date",
                table: "fx_rates");

            migrationBuilder.DropIndex(
                name: "ix_instrument_corporate_actions_instrument_id_effective_date",
                table: "instrument_corporate_actions");

            migrationBuilder.DropIndex(
                name: "ix_instruments_price_source_type_is_trading",
                table: "instruments");

            migrationBuilder.DropIndex(
                name: "ix_operations_portfolio_id_instrument_id_trade_date",
                table: "operations");

            migrationBuilder.DropIndex(
                name: "ix_operations_portfolio_id_trade_date_created_at",
                table: "operations");

            migrationBuilder.CreateIndex(
                name: "ix_operations_portfolio_id_trade_date",
                table: "operations",
                columns: new[] { "portfolio_id", "trade_date" });
        }
    }
}
