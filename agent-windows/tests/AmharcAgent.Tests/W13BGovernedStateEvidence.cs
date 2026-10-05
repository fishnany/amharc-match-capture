using System.Text.Json.Nodes;
using AmharcAgent.Core.Contracts;
using Microsoft.Data.Sqlite;

namespace AmharcAgent.Tests;

/// <summary>Test-only complete logical SQLite state, not file/WAL byte identity.
/// Clock context rows contain persisted clock/order/checkpoint state; activity
/// rows contain material history. Empty tables are recorded explicitly.</summary>
internal static class W13BGovernedStateEvidence
{
    public static JsonObject RecoveryComposition(JsonObject checkpoint, JsonObject context,
        W1DependencyResolver dependencies)
    {
        // Independently read the same actual state-bearing objects on each
        // invocation; NEVER clone the prior evidence object to manufacture after.
        var state = new JsonObject {
            ["checkpoint"] = JsonNode.Parse(checkpoint.ToJsonString()),
            ["boundContext"] = JsonNode.Parse(context.ToJsonString()),
            ["resolverClosureSha256"] = W1CanonicalJson.Digest(
                System.Text.Json.JsonSerializer.Serialize(dependencies.Export())),
            ["boundary"] = "Actual local resolution + pure recovery policy composition; no material runtime or persistence participating",
            ["persistedStoresParticipating"] = 0
        };
        return new JsonObject { ["logicalState"] = state,
            ["sha256"] = W1CanonicalJson.Digest(state.ToJsonString()) };
    }
    public static JsonObject Snapshot(string databasePath)
    {
        var tables = new JsonObject();
        using var db = new SqliteConnection(new SqliteConnectionStringBuilder {
            DataSource = databasePath, Mode = SqliteOpenMode.ReadOnly
        }.ToString());
        db.Open();
        using var transaction = db.BeginTransaction(deferred: true);
        foreach (var table in new[] {
            "w1_setup_subjects", "w1_setup_operations", "w1_setup_revisions",
            "W1ClockContexts", "W1ClockActivities"
        })
        {
            using var command = db.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"SELECT * FROM \"{table}\"";
            using var reader = command.ExecuteReader();
            var columns = new JsonArray();
            for (var i = 0; i < reader.FieldCount; i++) columns.Add(reader.GetName(i));
            var rows = new List<JsonArray>();
            while (reader.Read())
            {
                var row = new JsonArray();
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row.Add(reader.IsDBNull(i) ? null : reader.GetValue(i) switch {
                        string value => JsonValue.Create(value),
                        long value => JsonValue.Create(value),
                        double value => JsonValue.Create(value),
                        byte[] value => JsonValue.Create(Convert.ToHexString(value)),
                        _ => throw new InvalidOperationException("Unsupported SQLite evidence value")
                    });
                }
                rows.Add(row);
            }
            var sorted = new JsonArray(rows.OrderBy(r => r.ToJsonString(), StringComparer.Ordinal)
                .Select(r => (JsonNode)r).ToArray());
            var body = new JsonObject { ["columns"] = columns, ["rows"] = sorted };
            tables[table] = new JsonObject { ["sha256"] = W1CanonicalJson.Digest(body.ToJsonString()),
                ["rowCount"] = sorted.Count, ["logicalState"] = body };
        }
        var state = new JsonObject {
            ["tables"] = tables,
            ["scope"] = "All setup-ledger rows and persisted governed clock/order/checkpoint/material history. No live runtime instantiated for handoff fixtures."
        };
        return new JsonObject {
            ["identity"] = "urn:amharc:test:w1-3b:logical-governed-sqlite-state:1.0.0",
            ["sha256"] = W1CanonicalJson.Digest(state.ToJsonString()), ["logicalState"] = state
        };
    }
}