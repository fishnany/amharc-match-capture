using AmharcAgent.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AmharcAgent.Data.Migrations;

[DbContext(typeof(AmharcDbContext))]
[Migration("20261004130000_AddW1ProspectiveIdentity")]
public sealed class AddW1ProspectiveIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            CREATE TABLE W1OccurrenceCreations (
              Issuer TEXT NOT NULL, OperationKey TEXT NOT NULL,
              OccurrenceId TEXT NOT NULL UNIQUE, LocalMatchId TEXT NOT NULL,
              RequestSha256 TEXT NOT NULL, MaterialActivityJson TEXT NOT NULL,
              CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL,
              PRIMARY KEY (Issuer, OperationKey), UNIQUE (Issuer, LocalMatchId)
            );
            """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new InvalidOperationException(
            "W1_IDENTITY_HISTORY_ROLLBACK_REFUSED: retain issued identifiers and correspondence; use an Owner-reviewed forward repair.");
}