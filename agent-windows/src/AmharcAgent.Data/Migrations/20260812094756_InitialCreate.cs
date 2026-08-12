using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmharcAgent.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cameras",
                columns: table => new
                {
                    CameraId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Manufacturer = table.Column<string>(type: "TEXT", nullable: false),
                    Model = table.Column<string>(type: "TEXT", nullable: false),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 45, nullable: false),
                    RtspPort = table.Column<int>(type: "INTEGER", nullable: false),
                    HttpPort = table.Column<int>(type: "INTEGER", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Password = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: false),
                    LastConnectedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    StreamProfileName = table.Column<string>(type: "TEXT", nullable: true),
                    SerialNumber = table.Column<string>(type: "TEXT", nullable: true),
                    FirmwareVersion = table.Column<string>(type: "TEXT", nullable: true),
                    MacAddress = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cameras", x => x.CameraId);
                });

            migrationBuilder.CreateTable(
                name: "Matches",
                columns: table => new
                {
                    MatchId = table.Column<string>(type: "TEXT", nullable: false),
                    Sport = table.Column<string>(type: "TEXT", nullable: false),
                    Competition = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Season = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Round = table.Column<string>(type: "TEXT", nullable: true),
                    HomeTeam = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AwayTeam = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Venue = table.Column<string>(type: "TEXT", nullable: true),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    PeriodStructure = table.Column<string>(type: "TEXT", nullable: false),
                    CurrentPeriod = table.Column<int>(type: "INTEGER", nullable: false),
                    HomeGoals = table.Column<int>(type: "INTEGER", nullable: false),
                    HomeTwoPointScores = table.Column<int>(type: "INTEGER", nullable: false),
                    HomePoints = table.Column<int>(type: "INTEGER", nullable: false),
                    AwayGoals = table.Column<int>(type: "INTEGER", nullable: false),
                    AwayTwoPointScores = table.Column<int>(type: "INTEGER", nullable: false),
                    AwayPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.MatchId);
                });

            migrationBuilder.CreateTable(
                name: "MatchEvents",
                columns: table => new
                {
                    EventId = table.Column<string>(type: "TEXT", nullable: false),
                    MatchId = table.Column<string>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Team = table.Column<string>(type: "TEXT", nullable: true),
                    PlayerId = table.Column<string>(type: "TEXT", nullable: true),
                    PlayerNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    Period = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchClockSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    RecordingElapsedSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    SystemTimestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    Operator = table.Column<string>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", nullable: true),
                    ScoreBeforeState = table.Column<string>(type: "TEXT", nullable: true),
                    ScoreAfterState = table.Column<string>(type: "TEXT", nullable: true),
                    ScoreBefore = table.Column<string>(type: "TEXT", nullable: true),
                    ScoreAfter = table.Column<string>(type: "TEXT", nullable: true),
                    ClipRequested = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReviewStatus = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchEvents", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "RecordingSessions",
                columns: table => new
                {
                    RecordingId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    MatchId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    CameraId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    OutputDirectory = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    RtspUrl = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    StoppedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    SegmentDurationSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    SegmentCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FinalFilePath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Checksum = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecordingSessions", x => x.RecordingId);
                });

            migrationBuilder.CreateTable(
                name: "StreamDeckProfiles",
                columns: table => new
                {
                    ProfileId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Sport = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Buttons = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StreamDeckProfiles", x => x.ProfileId);
                });

            migrationBuilder.CreateTable(
                name: "StreamingDestinations",
                columns: table => new
                {
                    DestinationId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    Platform = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ServerUrl = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    StreamKey = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Resolution = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    FrameRate = table.Column<int>(type: "INTEGER", nullable: true),
                    BitRate = table.Column<int>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StreamingDestinations", x => x.DestinationId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Matches_Date",
                table: "Matches",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_Status",
                table: "Matches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MatchEvents_MatchId",
                table: "MatchEvents",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchEvents_MatchId_Period",
                table: "MatchEvents",
                columns: new[] { "MatchId", "Period" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchEvents_MatchId_SystemTimestamp",
                table: "MatchEvents",
                columns: new[] { "MatchId", "SystemTimestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_RecordingSessions_CameraId",
                table: "RecordingSessions",
                column: "CameraId");

            migrationBuilder.CreateIndex(
                name: "IX_RecordingSessions_MatchId",
                table: "RecordingSessions",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_RecordingSessions_MatchId_CameraId",
                table: "RecordingSessions",
                columns: new[] { "MatchId", "CameraId" });

            migrationBuilder.CreateIndex(
                name: "IX_StreamingDestinations_IsActive",
                table: "StreamingDestinations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_StreamingDestinations_Platform",
                table: "StreamingDestinations",
                column: "Platform");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Cameras");

            migrationBuilder.DropTable(
                name: "Matches");

            migrationBuilder.DropTable(
                name: "MatchEvents");

            migrationBuilder.DropTable(
                name: "RecordingSessions");

            migrationBuilder.DropTable(
                name: "StreamDeckProfiles");

            migrationBuilder.DropTable(
                name: "StreamingDestinations");
        }
    }
}
