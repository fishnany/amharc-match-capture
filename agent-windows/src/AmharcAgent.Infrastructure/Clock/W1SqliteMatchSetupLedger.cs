using System.Text.Json.Nodes;
using AmharcAgent.Core.Contracts;
using Microsoft.Data.Sqlite;

namespace AmharcAgent.Infrastructure.Clock;

/// <summary>Additive, non-cascading synthetic setup/non-reuse ledger beside the
/// existing W1 journal. BEGIN IMMEDIATE and unique keys protect across processes.</summary>
public sealed class W1SqliteMatchSetupLedger
{
    private readonly string _connection;
    private readonly string _captureIssuer, _actor, _build;
    private readonly JsonObject _allocator;
    private readonly Func<Guid> _allocate;
    public W1SqliteMatchSetupLedger(string path, string captureIssuer,
        JsonObject allocatorApplicabilityRef, string actor, string build, Func<Guid>? conformanceUuidSource = null)
    {
        if (!captureIssuer.StartsWith("urn:amharc:test:", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(actor) || string.IsNullOrWhiteSpace(build))
            throw new InvalidOperationException("OPERATIONAL_CONTEXT_UNAPPOINTED");
        _connection = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
        _captureIssuer = captureIssuer; _allocator = (JsonObject)allocatorApplicabilityRef.DeepClone();
        _actor = actor; _build = build;
        // A deterministic synthetic fixture seam does not replace the default
        // platform CSPRNG-backed allocation in the application composition.
        _allocate = conformanceUuidSource ?? Guid.NewGuid;
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA synchronous=FULL;
            CREATE TABLE IF NOT EXISTS w1_setup_subjects(
              occurrence_id TEXT PRIMARY KEY,
              tagger_issuer TEXT NOT NULL,
              tagger_local_id TEXT NOT NULL,
              capture_local_id TEXT NOT NULL UNIQUE,
              context_json TEXT NOT NULL,
              activated INTEGER NOT NULL DEFAULT 0,
              UNIQUE(tagger_issuer,tagger_local_id));
            CREATE TABLE IF NOT EXISTS w1_setup_operations(
              allocator_context TEXT NOT NULL,
              issuer TEXT NOT NULL,
              local_id TEXT NOT NULL,
              operation_key TEXT NOT NULL,
              request_digest TEXT NOT NULL,
              request_bytes TEXT NOT NULL,
              response_json TEXT NOT NULL,
              closure_json TEXT NOT NULL,
              PRIMARY KEY(allocator_context,issuer,local_id,operation_key));
            CREATE TABLE IF NOT EXISTS w1_setup_revisions(
              occurrence_id TEXT NOT NULL,
              revision INTEGER NOT NULL,
              accepted_context TEXT NOT NULL,
              activity_json TEXT NOT NULL,
              PRIMARY KEY(occurrence_id,revision));
            """;
        command.ExecuteNonQuery();
    }
    private SqliteConnection Open()
    {
        var db = new SqliteConnection(_connection); db.Open();
        using var c = db.CreateCommand(); c.CommandText = "PRAGMA busy_timeout=10000; PRAGMA synchronous=FULL;";
        c.ExecuteNonQuery(); return db;
    }
    private static string Text(JsonNode? n) => W1MatchSetupAdmission.Text(n);
    private string Namespace => W1CanonicalJson.Digest(_allocator.ToJsonString());
    private static SqliteCommand Command(SqliteConnection db, SqliteTransaction? tx,
        string sql, params (string Key, object? Value)[] args)
    {
        var command = db.CreateCommand(); command.Transaction = tx; command.CommandText = sql;
        foreach (var arg in args) command.Parameters.AddWithValue(arg.Key, arg.Value ?? DBNull.Value);
        return command;
    }
    public JsonObject? Read(string occurrenceId)
    {
        using var db = Open();
        using var c = Command(db, null, "SELECT context_json FROM w1_setup_subjects WHERE occurrence_id=$s", ("$s", occurrenceId));
        return c.ExecuteScalar() is string raw ? (JsonObject)JsonNode.Parse(raw)! : null;
    }
    public void LockForClockActivity(string occurrenceId)
    {
        using var db = Open(); using var tx = db.BeginTransaction(deferred: false);
        using var c = Command(db, tx, "UPDATE w1_setup_subjects SET activated=1 WHERE occurrence_id=$s", ("$s", occurrenceId));
        if (c.ExecuteNonQuery() != 1) throw new InvalidOperationException("CANONICAL_MISMATCH");
        tx.Commit();
    }
    public JsonArray Closure()
    {
        using var db = Open(); using var c = Command(db, null, "SELECT closure_json FROM w1_setup_operations ORDER BY rowid");
        using var reader = c.ExecuteReader(); var result = new JsonArray();
        while (reader.Read()) foreach (var document in (JsonArray)JsonNode.Parse(reader.GetString(0))!)
            result.Add(document!.DeepClone());
        return result;
    }
    public (int Allocations, int Receipts, int Revisions) Counts()
    {
        using var db = Open();
        int Count(string table) { using var c = Command(db, null, "SELECT COUNT(*) FROM " + table); return Convert.ToInt32(c.ExecuteScalar()); }
        return (Count("w1_setup_subjects"), Count("w1_setup_operations"), Count("w1_setup_revisions"));
    }
    public JsonObject Apply(W1AdmittedSetup admission, Action<string>? fault = null)
    {
        var request = admission.Request;
        var issuer = Text(request["taggerRepresentation"]?["issuer"]);
        var local = Text(request["taggerRepresentation"]?["localId"]);
        var key = Text(request["logicalOperationKey"]);
        var commitAttempted = false;
        try
        {
            using var db = Open(); using var tx = db.BeginTransaction(deferred: false);
            using (var lookup = Command(db, tx,
                "SELECT request_digest,response_json FROM w1_setup_operations WHERE allocator_context=$a AND issuer=$i AND local_id=$l AND operation_key=$k",
                ("$a", Namespace), ("$i", issuer), ("$l", local), ("$k", key)))
            {
                using var reader = lookup.ExecuteReader();
                if (reader.Read())
                {
                    if (reader.GetString(0) != admission.RequestDigest) return Failure(request, admission.RequestDigest, "HANDOFF_CONFLICT");
                    var historical = (JsonObject)JsonNode.Parse(reader.GetString(1))!;
                    historical["idempotencyStanding"] = "DUPLICATE_HISTORICAL";
                    return historical;
                }
            }
            JsonObject? prior = null; var activated = false;
            using (var lookup = Command(db, tx,
                "SELECT context_json,activated FROM w1_setup_subjects WHERE tagger_issuer=$i AND tagger_local_id=$l",
                ("$i", issuer), ("$l", local)))
            {
                using var reader = lookup.ExecuteReader();
                if (reader.Read()) { prior = (JsonObject)JsonNode.Parse(reader.GetString(0))!; activated = reader.GetBoolean(1); }
            }
            var intent = request["intent"]!;
            var operation = Text(intent["kind"]);
            // A supplied expected occurrence is a verifiable expectation, not
            // allocation authority or an ignorable CREATE_OR_RESOLVE hint.
            // This existing-correspondence check precedes every governed write.
            if (prior is not null && intent["expectedOccurrenceId"] is not null &&
                Text(intent["expectedOccurrenceId"]) != Text(prior["canonicalOccurrenceId"]))
                return Failure(request, admission.RequestDigest, "CANONICAL_MISMATCH");
            if (operation is "RESOLVE_EXISTING" or "CORRECT_SETUP")
            {
                if (prior is null || Text(intent["expectedOccurrenceId"]) != Text(prior["canonicalOccurrenceId"]))
                    return Failure(request, admission.RequestDigest, "CANONICAL_MISMATCH");
                if (!JsonNode.DeepEquals(intent["priorAssignmentRef"], prior["assignmentRef"]))
                    return Failure(request, admission.RequestDigest, "REVISION_CONFLICT");
            }
            var correcting = operation == "CORRECT_SETUP";
            if (correcting && activated) return Failure(request, admission.RequestDigest, "FORMAT_LOCKED_AFTER_ACTIVITY");
            if (prior is not null && !correcting && !JsonNode.DeepEquals(prior["assignmentRef"], request["assignmentRef"]))
                return Failure(request, admission.RequestDigest, "HANDOFF_CONFLICT");
            if (correcting && (!JsonNode.DeepEquals(request["assignment"]?["supersedesRef"], prior!["assignmentRef"]) ||
                Text(request["setup"]?["id"]) != Text(prior["setup"]?["id"]) ||
                !Version.TryParse(Text(request["setup"]?["version"]), out var newVersion) ||
                !Version.TryParse(Text(prior["setup"]?["version"]), out var priorVersion) || newVersion <= priorVersion))
                return Failure(request, admission.RequestDigest, "REVISION_CONFLICT");
            var created = prior is null;
            var subject = prior is null ? _allocate().ToString("D") : Text(prior["canonicalOccurrenceId"]);
            var captureLocal = prior is null ? Guid.NewGuid().ToString("D") : Text(prior["captureRepresentation"]?["localId"]);
            var revision = prior is null ? 1 : prior["setupRevision"]!.GetValue<int>() + (correcting ? 1 : 0);
            var context = new JsonObject {
                ["canonicalOccurrenceId"] = subject,
                ["taggerRepresentation"] = request["taggerRepresentation"]!.DeepClone(),
                ["captureRepresentation"] = new JsonObject { ["issuer"] = _captureIssuer, ["localId"] = captureLocal },
                ["assignmentRef"] = request["assignmentRef"]!.DeepClone(), ["assignment"] = request["assignment"]!.DeepClone(),
                ["formatRef"] = request["formatRef"]!.DeepClone(), ["format"] = admission.Format.DeepClone(),
                ["setup"] = request["setup"]!.DeepClone(), ["setupRevision"] = revision,
                ["identityStanding"] = "provisional", ["handlingApplicability"] = "synthetic/test-local only"
            };
            var closure = new JsonArray();
            JsonObject Document(string kind, Action<JsonObject> fields)
            {
                var d = new JsonObject { ["identifier"] = "urn:amharc:test:w1-3b:" + kind + ":" + Guid.NewGuid().ToString("D"),
                    ["version"] = "1.0.0", ["standing"] = "TEST_ONLY / NO OPERATIONAL APPOINTMENT" };
                fields(d); closure.Add(d.DeepClone()); return d;
            }
            JsonObject Reference(JsonObject d) => new() { ["id"] = d["identifier"]!.DeepClone(),
                ["version"] = d["version"]!.DeepClone(), ["sha256"] = W1CanonicalJson.Digest(d.ToJsonString()) };
            var tagger = created ? Document("tagger-resolution", d => {
                d["occurrenceId"] = subject; d["representation"] = request["taggerRepresentation"]!.DeepClone();
                d["identityStanding"] = "provisional"; }) : (JsonObject)prior!["taggerResolution"]!.DeepClone();
            var capture = created ? Document("capture-resolution", d => {
                d["occurrenceId"] = subject; d["representation"] = context["captureRepresentation"]!.DeepClone();
                d["identityStanding"] = "provisional"; }) : (JsonObject)prior!["captureResolution"]!.DeepClone();
            context["taggerResolution"] = tagger.DeepClone(); context["captureResolution"] = capture.DeepClone();
            var activity = Document("setup-result", d => {
                d["subject"] = subject; d["logicalOperationKey"] = key; d["actor"] = _actor; d["build"] = _build;
                d["kind"] = created ? "allocation-and-setup" : correcting ? "setup-correction" : "representation-resolution";
                d["inputRequestDigest"] = admission.RequestDigest; d["inputContext"] = prior?.DeepClone();
                d["outputContext"] = context.DeepClone(); d["allocatorApplicabilityRef"] = _allocator.DeepClone();
                d["qualifiedTime"] = new JsonObject { ["domain"] = "UTC", ["value"] = DateTimeOffset.UtcNow.ToString("O") };
                d["uncertainty"] = "No operational appointment or live-clock authority is conveyed";
            });
            var receipt = Document("receipt", d => {
                d["subject"] = subject; d["logicalOperationKey"] = key; d["requestDigest"] = admission.RequestDigest;
                d["resultActivityRef"] = Reference(activity); d["durability"] = "SQLite FULL synchronous committed transaction";
            });
            var response = new JsonObject {
                ["messageType"] = "MATCH_SETUP_RESPONSE", ["environment"] = request["environment"]!.DeepClone(),
                ["contractRef"] = request["contractRef"]!.DeepClone(), ["logicalOperationKey"] = key,
                ["requestDigest"] = admission.RequestDigest, ["status"] = "SUCCEEDED",
                ["canonicalOccurrenceId"] = subject, ["identityStanding"] = "provisional",
                ["taggerResolutionRef"] = Reference(tagger), ["captureResolutionRef"] = Reference(capture),
                ["allocatorApplicabilityRef"] = _allocator.DeepClone(),
                ["formatAcceptance"] = new JsonObject { ["formatRef"] = request["formatRef"]!.DeepClone(),
                    ["assignmentRef"] = request["assignmentRef"]!.DeepClone(), ["acceptanceActivityRef"] = Reference(activity) },
                ["resultActivityRef"] = Reference(activity), ["receiptRef"] = Reference(receipt),
                ["idempotencyStanding"] = created ? "CREATED_ONCE" : correcting ? "CORRECTED" : "RESOLVED_EXISTING",
                ["failure"] = null, ["clockAuthorityConferred"] = false
            };
            if (created)
            {
                using var c = Command(db, tx, "INSERT INTO w1_setup_subjects VALUES($s,$i,$l,$c,$j,0)",
                    ("$s", subject), ("$i", issuer), ("$l", local), ("$c", captureLocal), ("$j", context.ToJsonString()));
                c.ExecuteNonQuery();
            }
            else if (correcting)
            {
                using var c = Command(db, tx, "UPDATE w1_setup_subjects SET context_json=$j WHERE occurrence_id=$s",
                    ("$s", subject), ("$j", context.ToJsonString())); c.ExecuteNonQuery();
            }
            fault?.Invoke("after-subject-before-receipt");
            if (created || correcting)
            {
                using var c = Command(db, tx, "INSERT INTO w1_setup_revisions VALUES($s,$r,$j,$a)",
                    ("$s", subject), ("$r", revision), ("$j", context.ToJsonString()), ("$a", activity.ToJsonString()));
                c.ExecuteNonQuery();
            }
            using (var c = Command(db, tx, "INSERT INTO w1_setup_operations VALUES($a,$i,$l,$k,$h,$b,$r,$c)",
                ("$a", Namespace), ("$i", issuer), ("$l", local), ("$k", key), ("$h", admission.RequestDigest),
                ("$b", System.Text.Encoding.UTF8.GetString(W1CanonicalJson.Canonicalize(request.ToJsonString()))),
                ("$r", response.ToJsonString()), ("$c", closure.ToJsonString()))) c.ExecuteNonQuery();
            fault?.Invoke("before-commit");
            commitAttempted = true; tx.Commit();
            fault?.Invoke("after-commit-before-response");
            return response;
        }
        catch (Exception e) when (e is SqliteException or IOException)
        {
            return Failure(request, admission.RequestDigest, commitAttempted ? "PERSISTENCE_INDETERMINATE" : "ALLOCATOR_UNAVAILABLE");
        }
    }
    public static JsonObject Failure(JsonObject request, string digest, string code)
    {
        var indeterminate = code == "PERSISTENCE_INDETERMINATE";
        var classification = code switch {
            "ORIGIN_UNVERIFIED" => "AUTHENTICATION",
            "FORMAT_APPLICABILITY_UNPROVEN" or "OPERATIONAL_CONTEXT_UNAPPOINTED" => "APPLICABILITY",
            "ALLOCATOR_UNAVAILABLE" or "PERSISTENCE_INDETERMINATE" => "STORAGE",
            "CANONICAL_MISMATCH" or "TAGGER_REPRESENTATION_MISMATCH" => "IDENTITY",
            "HANDOFF_CONFLICT" or "REVISION_CONFLICT" or "FORMAT_LOCKED_AFTER_ACTIVITY" => "CONFLICT",
            "UNSUPPORTED_CONTRACT" or "UNSUPPORTED_SPORT" => "COMPATIBILITY", _ => "STRUCTURE"
        };
        return new JsonObject {
            ["messageType"] = "MATCH_SETUP_RESPONSE", ["environment"] = request["environment"]?.DeepClone() ?? JsonValue.Create("CONFORMANCE"),
            ["contractRef"] = new JsonObject { ["id"] = "urn:amharc:w1:match-setup-handoff-contract:1.0.0", ["version"] = "1.0.0",
                ["sha256"] = "2067a62d96be8cb62bf5893ed4882bc736df59ef4f75365c08c448b835f9fedd" },
            ["logicalOperationKey"] = request["logicalOperationKey"]?.DeepClone() ?? JsonValue.Create("invalid-request"),
            ["requestDigest"] = digest, ["status"] = indeterminate ? "INDETERMINATE" : "REFUSED",
            ["canonicalOccurrenceId"] = null, ["identityStanding"] = null, ["taggerResolutionRef"] = null,
            ["captureResolutionRef"] = null, ["allocatorApplicabilityRef"] = null, ["formatAcceptance"] = null,
            ["resultActivityRef"] = null, ["receiptRef"] = null,
            ["idempotencyStanding"] = indeterminate ? "UNKNOWN_COMMIT" : code == "HANDOFF_CONFLICT" ? "CONFLICT" : "NOT_APPLIED",
            ["failure"] = new JsonObject { ["classification"] = classification, ["reasonCode"] = code, ["detail"] = code },
            ["clockAuthorityConferred"] = false
        };
    }
}