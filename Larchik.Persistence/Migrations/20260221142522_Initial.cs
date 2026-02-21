using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Larchik.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    security_stamp = table.Column<string>(type: "text", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "brokers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_brokers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "currencies",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_currencies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "job_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    job_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    schedule_type = table.Column<int>(type: "integer", nullable: false),
                    schedule_value = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    last_run_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    next_run_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    max_attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 5),
                    retry_delay_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 15),
                    lock_timeout_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 5),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_role_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_asp_net_role_claims_asp_net_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "AspNetRoles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_asp_net_user_claims_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    provider_key = table.Column<string>(type: "text", nullable: false),
                    provider_display_name = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_logins", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "fk_asp_net_user_logins_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_asp_net_user_roles_asp_net_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "AspNetRoles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_asp_net_user_roles_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "fk_asp_net_user_tokens_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "portfolios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    broker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    reporting_currency_id = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_portfolios", x => x.id);
                    table.ForeignKey(
                        name: "fk_portfolios_brokers_broker_id",
                        column: x => x.broker_id,
                        principalTable: "brokers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_portfolios_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fx_rates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_currency_id = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    quote_currency_id = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    rate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fx_rates", x => x.id);
                    table.ForeignKey(
                        name: "fk_fx_rates_currencies_base_currency_id",
                        column: x => x.base_currency_id,
                        principalTable: "currencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_fx_rates_currencies_quote_currency_id",
                        column: x => x.quote_currency_id,
                        principalTable: "currencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "instruments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ticker = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    isin = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    figi = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    type = table.Column<int>(type: "integer", nullable: false),
                    currency_id = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    category_id = table.Column<int>(type: "integer", nullable: false),
                    exchange = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instruments", x => x.id);
                    table.ForeignKey(
                        name: "fk_instruments_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_instruments_currencies_currency_id",
                        column: x => x.currency_id,
                        principalTable: "currencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dedup_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    attempt = table.Column<int>(type: "integer", nullable: false),
                    max_attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 5),
                    available_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    locked_by = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    locked_until_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_runs", x => x.id);
                    table.ForeignKey(
                        name: "fk_job_runs_job_definitions_job_definition_id",
                        column: x => x.job_definition_id,
                        principalTable: "job_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cash_balances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    portfolio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_id = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
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
                name: "portfolio_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    portfolio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    nav_base = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    pnl_day_base = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    pnl_month_base = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    pnl_year_base = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    cash_base = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_portfolio_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "fk_portfolio_snapshots_portfolios_portfolio_id",
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
                    portfolio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instrument_id = table.Column<Guid>(type: "uuid", nullable: false),
                    method = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    remaining_quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    cost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency_id = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    opened_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "operations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    portfolio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instrument_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    fee = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency_id = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    trade_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    settlement_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_operations", x => x.id);
                    table.ForeignKey(
                        name: "fk_operations_currencies_currency_id",
                        column: x => x.currency_id,
                        principalTable: "currencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_operations_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalTable: "instruments",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_operations_portfolios_portfolio_id",
                        column: x => x.portfolio_id,
                        principalTable: "portfolios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "position_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    portfolio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instrument_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    cost_base = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    market_value_base = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unrealized_base = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    realized_base = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_position_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "fk_position_snapshots_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalTable: "instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_position_snapshots_portfolios_portfolio_id",
                        column: x => x.portfolio_id,
                        principalTable: "portfolios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instrument_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency_id = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prices", x => x.id);
                    table.ForeignKey(
                        name: "fk_prices_currencies_currency_id",
                        column: x => x.currency_id,
                        principalTable: "currencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_prices_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalTable: "instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "id", "concurrency_stamp", "name", "normalized_name" },
                values: new object[] { new Guid("e5165cd8-4c41-4cc2-8aad-47b879f9da38"), "d0ac7814-1576-4ea7-9614-234f86ff37b1", "Admin", "ADMIN" });

            migrationBuilder.InsertData(
                table: "AspNetUsers",
                columns: new[] { "id", "access_failed_count", "concurrency_stamp", "email", "email_confirmed", "lockout_enabled", "lockout_end", "normalized_email", "normalized_user_name", "password_hash", "phone_number", "phone_number_confirmed", "security_stamp", "two_factor_enabled", "user_name" },
                values: new object[] { new Guid("7e89d7d2-21e2-40ce-bef2-58c3b9408abb"), 0, "c53a3830-3f86-4505-bcdb-1d2d2f87c006", "admin@test.com", true, false, null, "ADMIN@TEST.COM", "ADMIN", "AQAAAAIAAYagAAAAELetNQlOXe6IFms9D+H9cktwcVgon6E7yho5xMfUV8vbI8lfSldk14mcajcwvxJeBQ==", null, false, "f3359b6674a7407793f4e0371c477b60", false, "admin" });

            migrationBuilder.InsertData(
                table: "categories",
                columns: new[] { "id", "name" },
                values: new object[,]
                {
                    { 1, "Валюта" },
                    { 2, "Финансы и банки" },
                    { 3, "Телекоммуникации" },
                    { 4, "Информационные технологии" },
                    { 5, "Энергетика" },
                    { 6, "Потребительские товары" },
                    { 7, "Недвижимость" },
                    { 8, "Валюта" },
                    { 9, "Электроэнергетика" },
                    { 10, "Сырьевая промышленность" }
                });

            migrationBuilder.InsertData(
                table: "currencies",
                column: "id",
                values: new object[]
                {
                    "EUR",
                    "RUB",
                    "USD"
                });

            migrationBuilder.InsertData(
                table: "AspNetUserRoles",
                columns: new[] { "role_id", "user_id" },
                values: new object[] { new Guid("e5165cd8-4c41-4cc2-8aad-47b879f9da38"), new Guid("7e89d7d2-21e2-40ce-bef2-58c3b9408abb") });

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_role_claims_role_id",
                table: "AspNetRoleClaims",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_claims_user_id",
                table: "AspNetUserClaims",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_logins_user_id",
                table: "AspNetUserLogins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_roles_role_id",
                table: "AspNetUserRoles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "normalized_user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_brokers_name",
                table: "brokers",
                column: "name",
                unique: true);

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
                name: "ix_fx_rates_base_currency_id_quote_currency_id_date",
                table: "fx_rates",
                columns: new[] { "base_currency_id", "quote_currency_id", "date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fx_rates_quote_currency_id",
                table: "fx_rates",
                column: "quote_currency_id");

            migrationBuilder.CreateIndex(
                name: "ix_instruments_category_id",
                table: "instruments",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_instruments_currency_id",
                table: "instruments",
                column: "currency_id");

            migrationBuilder.CreateIndex(
                name: "ix_instruments_figi",
                table: "instruments",
                column: "figi",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_instruments_isin",
                table: "instruments",
                column: "isin",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_instruments_ticker",
                table: "instruments",
                column: "ticker");

            migrationBuilder.CreateIndex(
                name: "ix_job_definitions_is_enabled_next_run_at",
                table: "job_definitions",
                columns: new[] { "is_enabled", "next_run_at" });

            migrationBuilder.CreateIndex(
                name: "ix_job_definitions_name",
                table: "job_definitions",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_job_runs_dedup_key",
                table: "job_runs",
                column: "dedup_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_job_runs_job_definition_id_created_at",
                table: "job_runs",
                columns: new[] { "job_definition_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_job_runs_locked_until_at",
                table: "job_runs",
                column: "locked_until_at");

            migrationBuilder.CreateIndex(
                name: "ix_job_runs_status_available_at",
                table: "job_runs",
                columns: new[] { "status", "available_at" });

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

            migrationBuilder.CreateIndex(
                name: "ix_operations_currency_id",
                table: "operations",
                column: "currency_id");

            migrationBuilder.CreateIndex(
                name: "ix_operations_instrument_id_trade_date",
                table: "operations",
                columns: new[] { "instrument_id", "trade_date" });

            migrationBuilder.CreateIndex(
                name: "ix_operations_portfolio_id_trade_date",
                table: "operations",
                columns: new[] { "portfolio_id", "trade_date" });

            migrationBuilder.CreateIndex(
                name: "ix_portfolio_snapshots_portfolio_id_date",
                table: "portfolio_snapshots",
                columns: new[] { "portfolio_id", "date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_portfolios_broker_id",
                table: "portfolios",
                column: "broker_id");

            migrationBuilder.CreateIndex(
                name: "ix_portfolios_user_id_name",
                table: "portfolios",
                columns: new[] { "user_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_position_snapshots_instrument_id",
                table: "position_snapshots",
                column: "instrument_id");

            migrationBuilder.CreateIndex(
                name: "ix_position_snapshots_portfolio_id_instrument_id_date",
                table: "position_snapshots",
                columns: new[] { "portfolio_id", "instrument_id", "date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_prices_currency_id",
                table: "prices",
                column: "currency_id");

            migrationBuilder.CreateIndex(
                name: "ix_prices_instrument_id_date_provider",
                table: "prices",
                columns: new[] { "instrument_id", "date", "provider" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "cash_balances");

            migrationBuilder.DropTable(
                name: "fx_rates");

            migrationBuilder.DropTable(
                name: "job_runs");

            migrationBuilder.DropTable(
                name: "lots");

            migrationBuilder.DropTable(
                name: "operations");

            migrationBuilder.DropTable(
                name: "portfolio_snapshots");

            migrationBuilder.DropTable(
                name: "position_snapshots");

            migrationBuilder.DropTable(
                name: "prices");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "job_definitions");

            migrationBuilder.DropTable(
                name: "portfolios");

            migrationBuilder.DropTable(
                name: "instruments");

            migrationBuilder.DropTable(
                name: "brokers");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "currencies");
        }
    }
}
