using Larchik.Persistence.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Larchik.Persistence.Migrations
{
    [DbContext(typeof(LarchikContext))]
    [Migration("20260301123000_AddBrokerCode")]
    public partial class AddBrokerCode : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "brokers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE brokers
                SET code = CASE name
                    WHEN 'СберБанк' THEN 'sber'
                    WHEN 'ВТБ' THEN 'vtb'
                    WHEN 'Т-Банк' THEN 'tbank'
                    WHEN 'Альфа-Банк' THEN 'alfabank'
                    WHEN 'Газпромбанк' THEN 'gazprombank'
                    WHEN 'Россельхозбанк' THEN 'rshb'
                    WHEN 'Промсвязьбанк' THEN 'psb'
                    WHEN 'Совкомбанк' THEN 'sovcombank'
                    WHEN 'Райффайзенбанк' THEN 'raiffeisen'
                    WHEN 'БКС Мир инвестиций' THEN 'bcs'
                    WHEN 'ФИНАМ' THEN 'finam'
                    WHEN 'АТОН' THEN 'aton'
                    WHEN 'КИТ Финанс Брокер' THEN 'kitfinance'
                    WHEN 'Цифра брокер' THEN 'cifra'
                    WHEN 'Ак Барс Банк' THEN 'akbars'
                    WHEN 'Банк Уралсиб' THEN 'uralsib'
                    WHEN 'МТС Банк' THEN 'mtsbank'
                    WHEN 'Банк Санкт-Петербург' THEN 'bspb'
                    WHEN 'МКБ' THEN 'mkb'
                    WHEN 'Инвестиционная палата' THEN 'investpalata'
                    ELSE code
                END
                WHERE code IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_brokers_code",
                table: "brokers",
                column: "code",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_brokers_code",
                table: "brokers");

            migrationBuilder.DropColumn(
                name: "code",
                table: "brokers");
        }
    }
}
