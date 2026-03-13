using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Larchik.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInstrumentAliasesAndCorporateActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "instrument_aliases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instrument_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alias_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    normalized_alias_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instrument_aliases", x => x.id);
                    table.ForeignKey(
                        name: "fk_instrument_aliases_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalTable: "instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "instrument_corporate_actions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instrument_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    factor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    effective_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instrument_corporate_actions", x => x.id);
                    table.ForeignKey(
                        name: "fk_instrument_corporate_actions_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalTable: "instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_instrument_aliases_instrument_id",
                table: "instrument_aliases",
                column: "instrument_id");

            migrationBuilder.CreateIndex(
                name: "ix_instrument_aliases_normalized_alias_code",
                table: "instrument_aliases",
                column: "normalized_alias_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_instrument_corporate_actions_instrument_id_type_effective_d",
                table: "instrument_corporate_actions",
                columns: new[] { "instrument_id", "type", "effective_date" },
                unique: true);

            migrationBuilder.Sql("""
                insert into instrument_aliases (id, instrument_id, alias_code, normalized_alias_code)
                select '0d74d460-c5b2-4736-b993-6ac51e836f8b'::uuid, i.id, 'US91822M1062', 'US91822M1062'
                from instruments i
                where i.ticker = 'VEON'
                  and not exists (
                      select 1
                      from instrument_aliases a
                      where a.normalized_alias_code = 'US91822M1062'
                  );

                insert into instrument_aliases (id, instrument_id, alias_code, normalized_alias_code)
                select '93393046-b9bc-4f19-b8db-0f0fb81fa537'::uuid, i.id, 'VEONold', 'VEONOLD'
                from instruments i
                where i.ticker = 'VEON'
                  and not exists (
                      select 1
                      from instrument_aliases a
                      where a.normalized_alias_code = 'VEONOLD'
                  );

                insert into instrument_corporate_actions (id, instrument_id, type, factor, effective_date, note)
                select
                    'b0af630e-7316-44a9-b2fd-81a56d0e22d8'::uuid,
                    i.id,
                    12,
                    0.04,
                    timestamptz '2023-03-09 00:00:00+00',
                    'System continuity: VEON ADR ratio change 1:25 on 2023-03-09'
                from instruments i
                where i.ticker = 'VEON'
                  and not exists (
                      select 1
                      from instrument_corporate_actions c
                      where c.instrument_id = i.id
                        and c.type = 12
                        and c.effective_date = timestamptz '2023-03-09 00:00:00+00'
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                delete from instrument_corporate_actions
                where id = 'b0af630e-7316-44a9-b2fd-81a56d0e22d8'::uuid;

                delete from instrument_aliases
                where id in (
                    '0d74d460-c5b2-4736-b993-6ac51e836f8b'::uuid,
                    '93393046-b9bc-4f19-b8db-0f0fb81fa537'::uuid
                );
                """);

            migrationBuilder.DropTable(
                name: "instrument_aliases");

            migrationBuilder.DropTable(
                name: "instrument_corporate_actions");
        }
    }
}
