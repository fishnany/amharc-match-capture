using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmharcAgent.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPeriodStartMatchClockBoundary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PeriodStartTotalMatchElapsedSeconds",
                table: "MatchClockRuntimeStates",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PeriodStartTotalMatchElapsedSeconds",
                table: "MatchClockRuntimeStates");
        }
    }
}
