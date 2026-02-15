using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Larchik.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBackgroundJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_runs");

            migrationBuilder.DropTable(
                name: "job_definitions");
        }
    }
}
