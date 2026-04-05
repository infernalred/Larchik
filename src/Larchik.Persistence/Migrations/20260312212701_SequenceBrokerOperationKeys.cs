using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Larchik.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SequenceBrokerOperationKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "broker_operation_key",
                table: "operations",
                type: "character varying(48)",
                maxLength: 48,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(35)",
                oldMaxLength: 35,
                oldNullable: true);

            migrationBuilder.Sql(
                """
                WITH operation_payloads AS (
                    SELECT
                        o.id,
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
                        ) AS occurrence
                    FROM operations o
                    LEFT JOIN instruments i ON i.id = o.instrument_id
                )
                UPDATE operations o
                SET broker_operation_key = concat('v2:', p.hash, ':', lpad(p.occurrence::text, 6, '0'))
                FROM operation_payloads p
                WHERE o.id = p.id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE operations
                SET broker_operation_key = NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "broker_operation_key",
                table: "operations",
                type: "character varying(35)",
                maxLength: 35,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(48)",
                oldMaxLength: 48,
                oldNullable: true);
        }
    }
}
