using System.Numerics;
using System.Text.Json.Nodes;
using AmharcAgent.Core.Contracts;
using AmharcAgent.Core.Interfaces;

namespace AmharcAgent.Infrastructure.Clock;

/// <summary>Opt-in, isolated W1 runtime. Every consequential operation captures
/// subject, context, incarnation, order, state and history under one lock.
/// External verified development governance is required; execution grants nothing.</summary>
public sealed class W1SubjectBoundRuntime
{
    private readonly object _gate = new();
    private readonly IW1ClockJournal _journal;
    private readonly TimeProvider _time;
    private readonly string _actor;
    private readonly string _build;
    private readonly JsonObject _identity;
    private JsonObject _context;
    private readonly JsonObject _format;
    private readonly W1OrderedMatchFormat? _orderedFormat;
    private readonly JsonObject? _matchSetup;
    private JsonObject _clock = new() {
        ["domain"] = "official-match-accumulated", ["unit"] = "seconds",
        ["accumulated"] = 0, ["phase"] = "playing", ["periodKey"] = "p1",
        ["periodStart"] = 0, ["periodElapsed"] = 0, ["state"] = "paused",
        ["method"] = "monotonic-live", ["uncertaintySeconds"] = null
    };
    private JsonObject? _grant;
    private JsonObject? _grantEvidence;
    private BigInteger _sequence;
    private long _revision;
    private long _lastMonotonic;
    private string? _historyId;
    private string _status = "gap";
    private bool _pendingRecovery;
    public string IncarnationId { get; } = Guid.NewGuid().ToString("D");
    public string Subject => _identity["occurrenceId"]!.GetValue<string>();

    public W1SubjectBoundRuntime(JsonObject resolvedIdentity, JsonObject exactContext,
        IW1ClockJournal journal, string actor, string build, string initialPeriodKey,
        W1DependencyResolver dependencies, TimeProvider? time = null, JsonObject? matchSetup = null)
    {
        if (string.IsNullOrWhiteSpace(actor) || string.IsNullOrWhiteSpace(build))
            throw new ArgumentException("Explicit process/build attribution is required.");
        _identity = (JsonObject)resolvedIdentity.DeepClone();
        _context = (JsonObject)exactContext.DeepClone();
        var controlled = dependencies.Resolve(W1ControlledReferences.Manifest)["exactDependencies"]!;
        foreach (var name in new[] { "semantic", "identifier", "provenance" })
        {
            var reference = W1DependencyResolver.Reference(_context[name]!);
            if (reference != W1DependencyResolver.Reference(controlled[name]!))
                throw new InvalidOperationException("W1_CONTROLLED_CONTEXT_REFUSAL");
            _ = dependencies.Resolve(reference);
        }
        W1ControlledReferences.AdmitFormat(dependencies, _context["format"]!);
        _format = dependencies.Resolve(W1DependencyResolver.Reference(_context["format"]!));
        if (_format["catalogueFamily"] is not null)
        {
            _orderedFormat = new(_format);
            _matchSetup = matchSetup is null ? throw new InvalidOperationException("W1_MATCH_SETUP_UNRESOLVED") :
                (JsonObject)matchSetup.DeepClone();
            if (W1DependencyResolver.Reference(_matchSetup["formatRef"]!) != W1DependencyResolver.Reference(_context["format"]!) ||
                W1MatchSetupAdmission.Text(_matchSetup["canonicalOccurrenceId"]) != Subject ||
                initialPeriodKey != _orderedFormat.InitialPeriod)
                throw new InvalidOperationException("W1_MATCH_SETUP_CONTEXT_REFUSAL");
            _clock["phase"] = "preplay";
            _clock["periodChain"] = new JsonArray(new JsonObject { ["periodKey"] = initialPeriodKey, ["periodStartSeconds"] = 0 });
        }
        var resolution = dependencies.Resolve(W1DependencyResolver.Reference(_identity["resolutionRef"]!));
        if (resolution["occurrenceId"]!.GetValue<string>() != Subject ||
            !JsonNode.DeepEquals(resolution["representation"], _identity["representation"]) ||
            resolution["identityStanding"]!.GetValue<string>() != _identity["identityStanding"]!.GetValue<string>())
            throw new InvalidOperationException("W1_SUBJECT_RESOLUTION_REFUSAL");
        _journal = journal; _actor = actor; _build = build;
        _time = time ?? TimeProvider.System;
        ArgumentException.ThrowIfNullOrWhiteSpace(initialPeriodKey);
        if (!((JsonArray)_format["periods"]!).Any(p => p!["key"]!.GetValue<string>() == initialPeriodKey))
            throw new InvalidOperationException("W1_FORMAT_UNRESOLVED");
        _clock["periodKey"] = initialPeriodKey;
        _lastMonotonic = _time.GetTimestamp();
    }

    private void SubjectGuard(string requestedSubject)
    {
        if (!StringComparer.Ordinal.Equals(requestedSubject, Subject))
            throw new InvalidOperationException("W1_SUBJECT_REFUSAL");
    }

    public void AdoptVerifiedDevelopmentGrant(string subject, W1VerifiedDevelopmentGrant verifiedGrant, string operationKey,
        W1Reference? initialHistoricalTrust = null)
    {
        lock (_gate)
        {
            SubjectGuard(subject);
            var grant = verifiedGrant.Copy();
            if (
                grant["subject"]?.GetValue<string>() != Subject ||
                grant["domain"]?.GetValue<string>() != "official-match-accumulated" ||
                grant["incarnationId"]?.GetValue<string>() != IncarnationId)
                throw new InvalidOperationException("W1_AUTHORITY_GAP");
            var previous = _grant;
            var previousEvidence = _grantEvidence;
            var previousStatus = _status;
            var previousContext = (JsonObject)_context.DeepClone();
            if (_grant is not null &&
                (_grant["principalId"]!.GetValue<string>() != grant["principalId"]!.GetValue<string>() ||
                 _grant["bindingId"]!.GetValue<string>() != grant["bindingId"]!.GetValue<string>()))
                throw new InvalidOperationException("W1_UNSUPPORTED_BINDING_TRANSFER");
            _grant = (JsonObject)grant.DeepClone();
            _grantEvidence = verifiedGrant.CopyEvidence();
            _status = "active";
            if (_journal.Read(Subject) is not null && _revision == 0)
            {
                try { Require("recover"); }
                catch { _grant = previous; _grantEvidence = previousEvidence; _status = previousStatus; throw; }
                _pendingRecovery = true;
                _status = "gap";
                return;
            }
            try
            {
                if (initialHistoricalTrust is not null && _revision == 0)
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(_grantEvidence.ToJsonString());
                    var actual = new W1Reference(_grantEvidence["identifier"]!.GetValue<string>(),
                        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant());
                    if (actual != initialHistoricalTrust) throw new InvalidOperationException("W1_HISTORICAL_CONTEXT_REFUSAL");
                    _context["trustBundle"] = JsonSerializerNode(actual);
                }
                Require("snapshot"); Material("binding activation", operationKey, _clock, "explicit development grant");
            }
            catch { _grant = previous; _grantEvidence = previousEvidence; _status = previousStatus; _context = previousContext; throw; }
        }
    }

    private void Require(string capability)
    {
        if (_pendingRecovery && capability != "recover")
            throw new InvalidOperationException("W1_PENDING_QUALIFIED_RECOVERY");
        if (_grant is null) throw new InvalidOperationException("W1_AUTHORITY_GAP");
        var now = _time.GetUtcNow();
        if (now < DateTimeOffset.Parse(_grant["effectiveFrom"]!.GetValue<string>()) ||
            now >= DateTimeOffset.Parse(_grant["effectiveUntil"]!.GetValue<string>()))
        {
            _status = "gap";
            _clock["state"] = "withheld";
            throw new InvalidOperationException("W1_AUTHORITY_GAP");
        }
        if (!((JsonArray)_grant["capabilities"]!).Any(c => c?.GetValue<string>() == capability))
            throw new InvalidOperationException("W1_CAPABILITY_REFUSAL");
    }

    public void Tick(string subject)
    {
        lock (_gate)
        {
            SubjectGuard(subject); Require("advance");
            if (_clock["state"]!.GetValue<string>() != "running") return;
            var now = _time.GetTimestamp();
            var elapsed = (int)_time.GetElapsedTime(_lastMonotonic, now).TotalSeconds;
            if (elapsed <= 0) return;
            _clock["accumulated"] = checked(_clock["accumulated"]!.GetValue<int>() + elapsed);
            _clock["periodElapsed"] = _clock["accumulated"]!.GetValue<int>() - _clock["periodStart"]!.GetValue<int>();
            _lastMonotonic += checked((long)elapsed * _time.TimestampFrequency);
        }
    }

    public void Transition(string subject, string operation, string operationKey,
        int? correctedSeconds = null, string? periodKey = null,
        IReadOnlySet<string>? applicablePeriodKeys = null, string basis = "", string? extraTimeCondition = null)
    {
        lock (_gate)
        {
            SubjectGuard(subject);
            var capability = operation switch {
                "correct" => "correct", "pause" or "resume" => "pause-resume",
                "period" => "period-transition", "complete" when _orderedFormat is not null => "period-transition",
                "checkpoint" => "checkpoint",
                _ => throw new ArgumentException("Unsupported bounded W1 operation")
            };
            Require(capability);
            if (_orderedFormat is not null)
            {
                var completed = _clock["phase"]!.GetValue<string>() == "completed";
                if (completed && operation is "resume" or "period" or "complete" or "correct")
                    throw new InvalidOperationException("ALREADY_COMPLETED");
                if (operation is "period" or "complete")
                    _orderedFormat.Admit(_clock["periodKey"]!.GetValue<string>(),
                        operation == "period" ? "NEXT_PERIOD" : "COMPLETE", periodKey, completed, true, extraTimeCondition);
            }
            var next = (JsonObject)_clock.DeepClone();
            switch (operation)
            {
                case "correct":
                    if (correctedSeconds is null || correctedSeconds < next["periodStart"]!.GetValue<int>() ||
                        string.IsNullOrWhiteSpace(basis)) throw new InvalidOperationException("W1_TEMPORAL_REFUSAL");
                    next["accumulated"] = correctedSeconds.Value;
                    next["periodElapsed"] = correctedSeconds.Value - next["periodStart"]!.GetValue<int>();
                    break;
                case "pause": next["state"] = "paused"; break;
                case "resume":
                    Require("advance"); next["state"] = "running";
                    if (_orderedFormat is not null) next["phase"] = "playing";
                    break;
                case "period":
                    if (periodKey is null || !((JsonArray)_format["periods"]!).Any(p => p!["key"]!.GetValue<string>() == periodKey))
                        throw new InvalidOperationException("W1_FORMAT_UNRESOLVED");
                    next["periodKey"] = periodKey;
                    next["periodStart"] = next["accumulated"]!.GetValue<int>();
                    next["periodElapsed"] = 0;
                    if (_orderedFormat is not null)
                    {
                        next["state"] = "paused"; next["phase"] = "interval";
                        ((JsonArray)next["periodChain"]!).Add(new JsonObject {
                            ["periodKey"] = periodKey, ["periodStartSeconds"] = next["periodStart"]!.DeepClone() });
                    }
                    break;
                case "complete": next["state"] = "paused"; next["phase"] = "completed"; break;
            }
            Material(operation, operationKey, next, basis);
            _lastMonotonic = _time.GetTimestamp();
        }
    }

    private void Material(string operation, string operationKey, JsonObject next, string basis)
    {
        var input = new JsonObject {
            ["operation"] = operation, ["basis"] = basis,
            ["correctedSeconds"] = operation == "correct" ? next["accumulated"]!.DeepClone() : null,
            ["periodKey"] = operation == "period" ? next["periodKey"]!.DeepClone() : null
        };
        var inputHash = W1CanonicalJson.Digest(input.ToJsonString());
        var retry = _journal.FindOperation(Subject, operationKey);
        if (retry is not null)
        {
            if (retry.InputSha256 != inputHash) throw new InvalidOperationException("W1_ACTIVITY_RETRY_CONFLICT");
            return;
        }
        var id = Guid.NewGuid().ToString("D");
        var activity = new JsonObject {
            ["identifier"] = "urn:amharc:w1:activity:" + id,
            ["activityId"] = id, ["subject"] = Subject,
            ["representation"] = _identity.DeepClone(), ["logicalOperationKey"] = operationKey,
            ["actorProcess"] = _actor, ["build"] = _build, ["kind"] = operation == "correct" ? "correction" : operation,
            ["priorSeconds"] = _clock["accumulated"]!.DeepClone(), ["newSeconds"] = next["accumulated"]!.DeepClone(),
            ["requiredCapability"] = operation == "correct" ? "correct" : null,
            ["inputRevision"] = _revision.ToString(), ["inputActivity"] = _historyId,
            ["parentRef"] = _historyId is null ? null : JsonSerializerNode(
                W1MaterialChain.Reference(_journal.ReadActivity(_historyId)
                    ?? throw new InvalidOperationException("W1_HISTORY_UNRESOLVED"))),
            ["baselineStanding"] = _historyId is null ? "governed-development-baseline" : null,
            ["priorState"] = _clock.DeepClone(), ["newState"] = next.DeepClone(),
            ["exactContext"] = _context.DeepClone(), ["historicalGrant"] = _grant?.DeepClone(),
            ["historicalGrantEvidence"] = _grantEvidence?.DeepClone(),
            ["incarnationId"] = IncarnationId, ["sequence"] = _sequence.ToString(),
            ["time"] = new JsonObject { ["domain"] = "UTC", ["value"] = _time.GetUtcNow().ToString("O") },
            ["effectivity"] = "development/conformance-only", ["uncertainty"] = "no historical continuity inferred",
            ["sourceBasis"] = basis, ["handlingApplicability"] = "synthetic/test-local only"
        };
        var context = new JsonObject {
            ["subject"] = _identity.DeepClone(), ["context"] = _context.DeepClone(),
            ["incarnationId"] = IncarnationId, ["sequence"] = _sequence.ToString(),
            ["clock"] = next.DeepClone(), ["activityId"] = id,
            ["historicalGrant"] = _grant?.DeepClone(), ["status"] = _status,
            ["historicalGrantEvidence"] = _grantEvidence?.DeepClone()
        };
        if (_matchSetup is not null) context["matchSetup"] = _matchSetup.DeepClone();
        var material = new W1JournalActivity(id, Subject, operationKey, inputHash, activity.ToJsonString());
        context["activityRef"] = JsonSerializerNode(W1MaterialChain.Reference(material));
        _journal.Commit(_revision, new(Subject, _revision + 1, "1.0.0", context.ToJsonString(), id),
            material);
        _revision++; _historyId = id; _clock = (JsonObject)next.DeepClone();
    }

    public void Restore(string subject, string operationKey,
        W1DevelopmentGrantVerifier historicalVerifier, W1DependencyResolver historicalDependencies)
    {
        lock (_gate)
        {
            SubjectGuard(subject); Require("recover");
            var stored = _journal.Read(Subject) ?? throw new InvalidOperationException("W1_LEGACY_ASSURANCE_GAP");
            if (stored.WriterVersion != "1.0.0") throw new InvalidOperationException("W1_LEGACY_ASSURANCE_GAP");
            var c = JsonNode.Parse(stored.ContextJson)!;
            W1MatchSetupAdmission? recoveryAdmission = null;
            if (_orderedFormat is not null)
            {
                // Resolve persisted checkpoint bytes, not the already resolved
                // current binding. No governed/in-memory recovery mutation yet.
                recoveryAdmission = new(historicalDependencies);
                _ = recoveryAdmission.ResolveRecoveryFormat(c["matchSetup"]?["formatRef"]);
                if (!JsonNode.DeepEquals(c["matchSetup"], _matchSetup))
                    throw new InvalidOperationException("CHECKPOINT_CONTEXT_MISMATCH");
            }
            var head = W1MaterialChain.CurrentHead(_journal, stored, historicalDependencies);
            if (c["activityRef"] is null || W1DependencyResolver.Reference(c["activityRef"]!) != head)
                throw new InvalidOperationException("W1_RECOVERY_HEAD_DIGEST_REFUSAL");
            if (W1MaterialChain.Validate(head, Subject, _context, historicalDependencies, historicalVerifier)
                != "QUALIFIED")
                throw new InvalidOperationException("W1_LEGACY_ASSURANCE_GAP");
            if (c["subject"]?["occurrenceId"]?.GetValue<string>() != Subject ||
                c["context"]!.ToJsonString() != _context.ToJsonString() ||
                _journal.ReadActivity(stored.ActivityId) is not { } activity ||
                activity.Subject != Subject || c["activityId"]?.GetValue<string>() != stored.ActivityId)
                throw new InvalidOperationException("W1_RECOVERY_CONTEXT_REFUSAL");
            foreach (var reference in ((JsonObject)c["context"]!).Select(x => x.Value))
                _ = historicalDependencies.Resolve(W1DependencyResolver.Reference(reference!));
            var oldGrant = historicalVerifier.Verify(
                (JsonObject)c["historicalGrantEvidence"]!, Subject,
                c["incarnationId"]!.GetValue<string>()).Copy();
            var oldActivity = JsonNode.Parse(activity.Json)!;
            if (!JsonNode.DeepEquals(oldActivity["newState"], c["clock"]) ||
                !JsonNode.DeepEquals(oldActivity["exactContext"], c["context"]) ||
                !JsonNode.DeepEquals(oldActivity["historicalGrantEvidence"], c["historicalGrantEvidence"]) ||
                oldActivity["incarnationId"]!.GetValue<string>() != c["incarnationId"]!.GetValue<string>() ||
                oldActivity["activityId"]!.GetValue<string>() != stored.ActivityId)
                throw new InvalidOperationException("W1_RECOVERY_RELATIONSHIP_REFUSAL");
            var oldTime = DateTimeOffset.Parse(oldActivity["time"]!["value"]!.GetValue<string>());
            if (oldTime < DateTimeOffset.Parse(oldGrant["effectiveFrom"]!.GetValue<string>()) ||
                oldTime >= DateTimeOffset.Parse(oldGrant["effectiveUntil"]!.GetValue<string>()))
                throw new InvalidOperationException("W1_HISTORICAL_ELIGIBILITY_REFUSAL");
            var next = (JsonObject)c["clock"]!.DeepClone();
            if (_orderedFormat is not null)
            {
                var checkpoint = (JsonObject)c["matchSetup"]!.DeepClone();
                checkpoint["subject"] = Subject;
                checkpoint["historyFormatRef"] = _matchSetup["formatRef"]!.DeepClone();
                checkpoint["historyAssignmentRef"] = _matchSetup["assignmentRef"]!.DeepClone();
                checkpoint["currentPeriodKey"] = next["periodKey"]!.DeepClone();
                checkpoint["periodChain"] = next["periodChain"]!.DeepClone();
                checkpoint["accumulatedSeconds"] = next["accumulated"]!.DeepClone();
                checkpoint["periodStartSeconds"] = next["periodStart"]!.DeepClone();
                checkpoint["periodElapsedSeconds"] = next["periodElapsed"]!.DeepClone();
                var context = (JsonObject)_matchSetup.DeepClone();
                context["subject"] = Subject;
                recoveryAdmission!.AdmitRecovery(checkpoint, context, true);
            }
            next["state"] = "paused"; next["method"] = "utc-reconstructed";
            next["uncertaintySeconds"] = null;
            var oldRevision = _revision;
            var oldHistoryId = _historyId;
            var oldClock = _clock;
            var oldStatus = _status;
            _revision = stored.Revision; _historyId = stored.ActivityId;
            _clock = (JsonObject)c["clock"]!.DeepClone();
            _status = "active";
            try { Material("recovery", operationKey, next, "stored qualified state; no wall-time continuity or progress inferred"); }
            catch { _revision = oldRevision; _historyId = oldHistoryId; _clock = oldClock; _status = oldStatus; throw; }
            _pendingRecovery = false;
        }
    }

    public JsonObject Capture(string subject)
    {
        lock (_gate)
        {
            SubjectGuard(subject); Require("snapshot");
            var clock = (JsonObject)_clock.DeepClone();
            // Period lineage is checkpoint/material evidence, not an extension of
            // the unchanged normative Clock Envelope.
            clock.Remove("periodChain");
            return new JsonObject {
                ["subject"] = _identity.DeepClone(), ["context"] = _context.DeepClone(),
                ["authority"] = new JsonObject {
                    ["status"] = _status, ["principalId"] = _grant!["principalId"]!.DeepClone(),
                    ["bindingId"] = _grant["bindingId"]!.DeepClone(), ["incarnationId"] = IncarnationId,
                    ["generation"] = "1", ["sequence"] = (++_sequence).ToString(), ["capability"] = "snapshot",
                    ["reason"] = _status == "active" ? null : "eligibility-not-established"
                },
                ["clock"] = clock, ["observedAtUtc"] = _time.GetUtcNow().UtcDateTime.ToString("O"),
                ["activityId"] = _historyId
            };
        }
    }

    public W1Reference MaterialHead(string subject)
    {
        lock (_gate)
        {
            SubjectGuard(subject);
            var activity = _historyId is null ? null : _journal.ReadActivity(_historyId);
            if (activity is null) throw new InvalidOperationException("W1_HISTORY_UNRESOLVED");
            return new("urn:amharc:w1:activity:" + activity.Id,
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(activity.Json))).ToLowerInvariant());
        }
    }

    private static JsonObject JsonSerializerNode(W1Reference r) =>
        new() { ["id"] = r.Id, ["sha256"] = r.Sha256 };

    public T CaptureCoherently<T>(string subject, Func<JsonObject, W1Reference, T> produce)
    {
        lock (_gate)
        {
            SubjectGuard(subject);
            var sequence = _sequence;
            try { return produce(Capture(subject), MaterialHead(subject)); }
            catch { _sequence = sequence; throw; }
        }
    }
}