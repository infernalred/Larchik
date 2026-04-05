using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Larchik.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMoexSecIdAliases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                insert into instrument_aliases (id, instrument_id, alias_code, normalized_alias_code)
                select '9e9703f0-5cd5-4531-b9ba-5fed8ef3e001'::uuid, i.id, 'LQDT', 'LQDT'
                from instruments i
                where i.ticker = 'RU000A1014L8'
                  and not exists (
                      select 1
                      from instrument_aliases a
                      where a.normalized_alias_code = 'LQDT'
                  );

                insert into instrument_aliases (id, instrument_id, alias_code, normalized_alias_code)
                select '9e9703f0-5cd5-4531-b9ba-5fed8ef3e002'::uuid, i.id, 'SU26233RMFS5', 'SU26233RMFS5'
                from instruments i
                where i.ticker = 'RU000A101F94'
                  and not exists (
                      select 1
                      from instrument_aliases a
                      where a.normalized_alias_code = 'SU26233RMFS5'
                  );

                insert into instrument_aliases (id, instrument_id, alias_code, normalized_alias_code)
                select '9e9703f0-5cd5-4531-b9ba-5fed8ef3e003'::uuid, i.id, 'TMON', 'TMON'
                from instruments i
                where i.ticker = 'RU000A106DL2'
                  and not exists (
                      select 1
                      from instrument_aliases a
                      where a.normalized_alias_code = 'TMON'
                  );

                insert into instrument_aliases (id, instrument_id, alias_code, normalized_alias_code)
                select '9e9703f0-5cd5-4531-b9ba-5fed8ef3e004'::uuid, i.id, 'HEAD', 'HEAD'
                from instruments i
                where i.ticker = 'RU000A107662'
                  and not exists (
                      select 1
                      from instrument_aliases a
                      where a.normalized_alias_code = 'HEAD'
                  );

                insert into instrument_aliases (id, instrument_id, alias_code, normalized_alias_code)
                select '9e9703f0-5cd5-4531-b9ba-5fed8ef3e005'::uuid, i.id, 'YDEX', 'YDEX'
                from instruments i
                where i.ticker = 'RU000A107T19'
                  and not exists (
                      select 1
                      from instrument_aliases a
                      where a.normalized_alias_code = 'YDEX'
                  );

                insert into instrument_aliases (id, instrument_id, alias_code, normalized_alias_code)
                select '9e9703f0-5cd5-4531-b9ba-5fed8ef3e006'::uuid, i.id, 'SU26247RMFS5', 'SU26247RMFS5'
                from instruments i
                where i.ticker = 'RU000A108EF8'
                  and not exists (
                      select 1
                      from instrument_aliases a
                      where a.normalized_alias_code = 'SU26247RMFS5'
                  );

                insert into instrument_aliases (id, instrument_id, alias_code, normalized_alias_code)
                select '9e9703f0-5cd5-4531-b9ba-5fed8ef3e007'::uuid, i.id, 'SU26248RMFS3', 'SU26248RMFS3'
                from instruments i
                where i.ticker = 'RU000A108EH4'
                  and not exists (
                      select 1
                      from instrument_aliases a
                      where a.normalized_alias_code = 'SU26248RMFS3'
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                delete from instrument_aliases
                where id in (
                    '9e9703f0-5cd5-4531-b9ba-5fed8ef3e001'::uuid,
                    '9e9703f0-5cd5-4531-b9ba-5fed8ef3e002'::uuid,
                    '9e9703f0-5cd5-4531-b9ba-5fed8ef3e003'::uuid,
                    '9e9703f0-5cd5-4531-b9ba-5fed8ef3e004'::uuid,
                    '9e9703f0-5cd5-4531-b9ba-5fed8ef3e005'::uuid,
                    '9e9703f0-5cd5-4531-b9ba-5fed8ef3e006'::uuid,
                    '9e9703f0-5cd5-4531-b9ba-5fed8ef3e007'::uuid
                );
                """);
        }
    }
}
