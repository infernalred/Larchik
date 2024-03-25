using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Larchik.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrencyCategoryStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Currencies",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Stocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Ticker = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Isin = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    CurrencyId = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Stocks_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Stocks_Currencies_CurrencyId",
                        column: x => x.CurrencyId,
                        principalTable: "Currencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: new Guid("7e89d7d2-21e2-40ce-bef2-58c3b9408abb"),
                columns: new[] { "ConcurrencyStamp", "PasswordHash" },
                values: new object[] { "b2421be1-9894-4089-8dff-1bd4468bb763", "AQAAAAIAAYagAAAAEEPo90MGtb+9OlADfR4OxXXW47Ly9zgLyt1nba6ngDGEKNzuCV001hUb4WWP/Eq1+g==" });

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Валюта" },
                    { 2, "Финансы и банки" },
                    { 3, "Телекоммуникации" },
                    { 4, "Информационные технологии" },
                    { 5, "Энергетика" },
                    { 6, "Потребительские товары" },
                    { 7, "Недвижимость" },
                    { 8, "Сырьевая промышленность" },
                    { 9, "Электроэнергетика" }
                });

            migrationBuilder.InsertData(
                table: "Currencies",
                column: "Id",
                values: new object[]
                {
                    "EUR",
                    "RUB",
                    "USD"
                });

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_CategoryId",
                table: "Stocks",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_CurrencyId",
                table: "Stocks",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_Isin",
                table: "Stocks",
                column: "Isin",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_Ticker",
                table: "Stocks",
                column: "Ticker");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Stocks");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Currencies");

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: new Guid("7e89d7d2-21e2-40ce-bef2-58c3b9408abb"),
                columns: new[] { "ConcurrencyStamp", "PasswordHash" },
                values: new object[] { "83134201-abcc-49ec-8792-bede7a8e8251", "AQAAAAIAAYagAAAAEMAuwtaiWKuKak7jCU+CzruErW/+KfSy8xwPyco2Eq26q0aNwPza/6Ki8rdCQv4DXw==" });
        }
    }
}
