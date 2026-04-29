using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Larchik.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReconciliationSeverity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "alert_required",
                table: "portfolio_reconciliation_results",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "severity",
                table: "portfolio_reconciliation_results",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_portfolio_reconciliation_results_alert_required_statement_d",
                table: "portfolio_reconciliation_results",
                columns: new[] { "alert_required", "statement_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_portfolio_reconciliation_results_alert_required_statement_d",
                table: "portfolio_reconciliation_results");

            migrationBuilder.DropColumn(
                name: "alert_required",
                table: "portfolio_reconciliation_results");

            migrationBuilder.DropColumn(
                name: "severity",
                table: "portfolio_reconciliation_results");
        }
    }
}
