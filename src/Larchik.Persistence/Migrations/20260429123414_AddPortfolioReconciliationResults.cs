using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Larchik.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPortfolioReconciliationResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "portfolio_reconciliation_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    portfolio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    statement_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    source = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    reporting_currency_id = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reason_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tolerance_base = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    actual_nav_base = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    actual_cash_base = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    actual_positions_value_base = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    target_nav_base = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    target_cash_base = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    target_positions_value_base = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    nav_delta = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    cash_delta = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    positions_delta = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_portfolio_reconciliation_results", x => x.id);
                    table.ForeignKey(
                        name: "fk_portfolio_reconciliation_results_portfolios_portfolio_id",
                        column: x => x.portfolio_id,
                        principalTable: "portfolios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_portfolio_reconciliation_results_portfolio_id_statement_dat",
                table: "portfolio_reconciliation_results",
                columns: new[] { "portfolio_id", "statement_date", "source", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_portfolio_reconciliation_results_status_statement_date",
                table: "portfolio_reconciliation_results",
                columns: new[] { "status", "statement_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "portfolio_reconciliation_results");
        }
    }
}
