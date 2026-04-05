using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Larchik.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBrokerOperationKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "broker_operation_key",
                table: "operations",
                type: "character varying(35)",
                maxLength: 35,
                nullable: true);

            migrationBuilder.Sql(
                """
                WITH operation_payloads AS (
                    SELECT
                        o.id,
                        o.portfolio_id,
                        md5(
                            concat(
                                'v1|',
                                o.type::text, '|',
                                btrim(coalesce(nullif(i.isin, ''), nullif(i.ticker, ''), '')), '|',
                                to_char(o.quantity, 'FM999999999999990.000000'), '|',
                                to_char(o.price, 'FM999999999999990.000000'), '|',
                                to_char(o.fee, 'FM999999999999990.0000'), '|',
                                o.currency_id, '|',
                                to_char((o.trade_date at time zone 'UTC')::date, 'YYYY-MM-DD'), '|',
                                coalesce(to_char((o.settlement_date at time zone 'UTC')::date, 'YYYY-MM-DD'), ''), '|',
                                btrim(coalesce(o.note, ''))
                            )
                        ) AS hash,
                        row_number() OVER (
                            PARTITION BY
                                o.portfolio_id,
                                md5(
                                    concat(
                                        'v1|',
                                        o.type::text, '|',
                                        btrim(coalesce(nullif(i.isin, ''), nullif(i.ticker, ''), '')), '|',
                                        to_char(o.quantity, 'FM999999999999990.000000'), '|',
                                        to_char(o.price, 'FM999999999999990.000000'), '|',
                                        to_char(o.fee, 'FM999999999999990.0000'), '|',
                                        o.currency_id, '|',
                                        to_char((o.trade_date at time zone 'UTC')::date, 'YYYY-MM-DD'), '|',
                                        coalesce(to_char((o.settlement_date at time zone 'UTC')::date, 'YYYY-MM-DD'), ''), '|',
                                        btrim(coalesce(o.note, ''))
                                    )
                                )
                            ORDER BY o.created_at, o.id
                        ) AS rn
                    FROM operations o
                    LEFT JOIN instruments i ON i.id = o.instrument_id
                )
                UPDATE operations o
                SET broker_operation_key = 'v1:' || p.hash
                FROM operation_payloads p
                WHERE o.id = p.id
                  AND p.rn = 1;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_operations_portfolio_id_broker_operation_key",
                table: "operations",
                columns: new[] { "portfolio_id", "broker_operation_key" },
                unique: true,
                filter: "\"broker_operation_key\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_operations_portfolio_id_broker_operation_key",
                table: "operations");

            migrationBuilder.DropColumn(
                name: "broker_operation_key",
                table: "operations");
        }
    }
}
