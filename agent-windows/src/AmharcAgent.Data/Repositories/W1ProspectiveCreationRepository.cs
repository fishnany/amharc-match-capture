using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AmharcAgent.Core.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AmharcAgent.Data.Repositories;

/// <summary>Opt-in development boundary. The issuer/actor/build/dependency context
/// must be supplied by the application composition root, not inferred from Match
/// labels, a caller UUID or the presence of the Capture executable.</summary>
public sealed class W1ProspectiveCreationRepository(AmharcDbContext db)
{
    public async Task<W1OccurrenceCreation> CreateAsync(
        string issuer, string operationKey, Match requestedMatch,
        string actor, string build, IReadOnlyDictionary<string, string> exactDependencies,
        CancellationToken ct = default)
    {
        foreach (var s in new[] { issuer, operationKey, actor, build })
            if (string.IsNullOrWhiteSpace(s) || s.Length > 512)
                throw new ArgumentException("An explicit bounded creation context is required.");
        if (exactDependencies.Count == 0 ||
            exactDependencies.Any(d => d.Key is "latest" or "default" ||
                d.Value.Length != 64 || d.Value.Any(c => !char.IsAsciiHexDigit(c) || char.IsUpper(c))))
            throw new ArgumentException("Exact controlled dependency digests are required.");

        // Request identity excludes representation IDs and housekeeping timestamps.
        // A retry with changed substantive input is a conflict, never silent reuse.
        var request = JsonSerializer.Serialize(new {
            requestedMatch.Sport, requestedMatch.Competition, requestedMatch.Season,
            requestedMatch.Round, requestedMatch.Date, requestedMatch.Venue,
            requestedMatch.HomeTeam, requestedMatch.AwayTeam, requestedMatch.PeriodStructure
        });
        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request))).ToLowerInvariant();
        await db.Database.OpenConnectionAsync(ct);
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        // BEGIN IMMEDIATE serializes read/create across processes, not just frontend
        // timing or a process-local semaphore. SQLite unique constraints are final guards.
        using var transaction = connection.BeginTransaction(deferred: false);
        await using var enlisted = await db.Database.UseTransactionAsync(transaction, ct);
        var existing = await db.Set<W1OccurrenceCreation>()
            .SingleOrDefaultAsync(x => x.Issuer == issuer && x.OperationKey == operationKey, ct);
        if (existing is not null)
        {
            if (existing.RequestSha256 != requestHash)
                throw new InvalidOperationException("CREATION_OPERATION_CONFLICT");
            await transaction.CommitAsync(ct);
            return existing;
        }
        var now = DateTimeOffset.UtcNow;
        requestedMatch.MatchId = Guid.NewGuid().ToString("D");
        requestedMatch.CreatedAt = requestedMatch.UpdatedAt = now;
        var occurrence = Guid.NewGuid().ToString("D");
        var activity = new {
            activityId = Guid.NewGuid().ToString("D"),
            kind = "logical-occurrence-creation",
            logicalOperationKey = new { issuer, operationKey },
            subject = occurrence,
            representation = new { issuer, localId = requestedMatch.MatchId },
            actor, process = Environment.ProcessId.ToString(), build,
            inputs = new { requestSha256 = requestHash },
            exactDependencies,
            outputs = new { occurrenceId = occurrence, localId = requestedMatch.MatchId },
            qualifiedTime = new { domain = "UTC", value = now.ToString("O") },
            effectivity = "development/conformance-only",
            identityStanding = "provisional",
            uncertainty = "No operational issuer/allocator appointment is conveyed",
            sourceBasis = "Explicit prospective logical creation; no historical backfill"
        };
        var result = new W1OccurrenceCreation {
            Issuer = issuer, OperationKey = operationKey, OccurrenceId = occurrence,
            LocalMatchId = requestedMatch.MatchId, RequestSha256 = requestHash,
            MaterialActivityJson = JsonSerializer.Serialize(activity),
            CreatedAt = now, UpdatedAt = now
        };
        db.Matches.Add(requestedMatch);
        db.Set<W1OccurrenceCreation>().Add(result);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }
}