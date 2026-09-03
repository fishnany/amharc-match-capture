using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmharcAgent.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchClockRuntimeState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchClockRuntimeStates",
                columns: table => new
                {
                    MatchId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    MatchClockSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    RecordingElapsedSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    IsRunning = table.Column<bool>(type: "INTEGER", nullable: false),
                    CurrentPeriod = table.Column<int>(type: "INTEGER", nullable: false),
                    ClockMode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    PersistedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchClockRuntimeStates", x => x.MatchId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchClockRuntimeStates");
        }
    }
}
