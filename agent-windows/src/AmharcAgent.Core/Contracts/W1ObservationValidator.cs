using System.Numerics;
using System.Text.Json.Nodes;
using Json.Schema;

namespace AmharcAgent.Core.Contracts;

public sealed record W1ObservationResult(string Qualification, JsonObject? Claim = null);
public sealed record W1DevelopmentTrustAnchor(
    string PublicKeyPem, IReadOnlySet<string> Subjects, string Domain,
    string Standing = "development/conformance-only");

/// <summary>Independent local validation: not a role appointment, Event acceptance
/// decision or continuity proof. Exact bytes are resolved without network fallback.</summary>
public sealed class W1ObservationValidator(
    W1DependencyResolver resolver, W1Reference schemaReference,
    W1DevelopmentTrustAnchor anchor)
{
    private readonly Dictionary<string, (BigInteger Generation, BigInteger Order, string Digest, int Seconds)> _heads = new();
    private readonly HashSet<string> _conflicts = new();
    private static string Text(JsonNode? n) => n?.GetValue<string>() ?? "";
    public W1ObservationResult Validate(string raw, string requestedSubject)
    {
        JsonObject s;
        try
        {
            _ = W1CanonicalJson.Canonicalize(raw);
            s = JsonNode.Parse(raw) as JsonObject ?? throw new FormatException();
            if (Text(s["contractVersion"]) == "1.0") return new("LEGACY_ASSURANCE_GAP");
            var schema = JsonSchema.FromText(resolver.Resolve(schemaReference).ToJsonString());
            if (!schema.Evaluate(s, new EvaluationOptions { RequireFormatValidation = true }).IsValid)
                return new("SCHEMA_INVALID");
        }
        catch (InvalidOperationException) { return new("CONTEXT_UNRESOLVED"); }
        catch (Exception e) when (e is FormatException or System.Text.Json.JsonException)
        { return new("SCHEMA_INVALID"); }
        var subject = s["subject"]!;
        var authority = s["authority"]!;
        var clock = s["clock"]!;
        var context = s["context"]!;
        JsonObject trust, format;
        try { trust = resolver.Resolve(W1DependencyResolver.Reference(context["trustBundle"]!)); }
        catch (InvalidOperationException) { return new("CONTEXT_UNRESOLVED"); }
        var hash = W1CanonicalJson.Digest(raw, "attestation");
        var proof = s["attestation"]!;
        if (Text(proof["keyId"]) != Text(trust["keyId"]) || Text(proof["payloadSha256"]) != hash ||
            !W1CanonicalJson.Verify(hash, Text(trust["principalPublicKey"]), Text(proof["signatureBase64"])))
            return new("SIGNATURE_REFUSAL");
        if (Text(subject["occurrenceId"]) != requestedSubject) return new("SUBJECT_REFUSAL");
        try
        {
            W1ControlledReferences.AdmitEnvelope(resolver, s, schemaReference);
            _ = resolver.Resolve(W1DependencyResolver.Reference(s["contractRef"]!));
            foreach (var name in new[] { "semantic", "identifier", "provenance" })
                _ = resolver.Resolve(W1DependencyResolver.Reference(context[name]!));
            format = resolver.Resolve(W1DependencyResolver.Reference(context["format"]!));
        }
        catch (InvalidOperationException) { return new("CONTEXT_UNRESOLVED"); }
        if (Text(authority["status"]) != "active")
        {
            if (Text(clock["state"]) != "withheld" || string.IsNullOrWhiteSpace(Text(authority["reason"])))
                return new("AUTHORITY_REFUSAL");
            if (Text(authority["reason"]) == "legacy-checkpoint-unknown-binding") return new("LEGACY_ASSURANCE_GAP");
            return new(Text(authority["status"]) == "conflict" ? "AUTHORITY_CONFLICT" : "AUTHORITY_GAP");
        }
        var grant = trust["grant"]!;
        if (Text(grant["subject"]) != requestedSubject || Text(grant["domain"]) != Text(clock["domain"]) ||
            Text(grant["principalId"]) != Text(authority["principalId"]) ||
            Text(grant["bindingId"]) != Text(authority["bindingId"]) ||
            Text(grant["incarnationId"]) != Text(authority["incarnationId"]) ||
            Text(authority["capability"]) != "snapshot" ||
            !((JsonArray)grant["capabilities"]!).Any(c => Text(c) == "snapshot")) return new("BINDING_REFUSAL");
        var capabilities = ((JsonArray)grant["capabilities"]!).Select(c => Text(c)).ToHashSet();
        if (Text(clock["state"]) == "running" && !capabilities.Contains("advance")) return new("CAPABILITY_REFUSAL");
        JsonObject resolution;
        try { resolution = resolver.Resolve(W1DependencyResolver.Reference(subject["resolutionRef"]!)); }
        catch (InvalidOperationException) { return new("SUBJECT_REFUSAL"); }
        if (W1DependencyResolver.Reference(subject["resolutionRef"]!) != W1DependencyResolver.Reference(trust["resolutionRef"]!) ||
            Text(resolution["occurrenceId"]) != requestedSubject ||
            Text(resolution["identityStanding"]) != Text(subject["identityStanding"]) ||
            Text(resolution["representation"]?["issuer"]) != Text(subject["representation"]?["issuer"]) ||
            Text(resolution["representation"]?["localId"]) != Text(subject["representation"]?["localId"]))
            return new("SUBJECT_REFUSAL");
        JsonObject history;
        try
        {
            var activityRef = W1DependencyResolver.Reference(s["activityRef"]!);
            if (!((JsonArray)trust["materialHistory"]!).Any(r => W1DependencyResolver.Reference(r!) == activityRef))
                return new("HISTORY_REFUSAL");
            history = resolver.Resolve(activityRef);
            if (Text(history["subject"]) != requestedSubject) return new("HISTORY_REFUSAL");
        }
        catch (InvalidOperationException) { return new("HISTORY_REFUSAL"); }
        var closure = trust["closure"]!;
        var governance = trust["governanceProof"]!;
        var grantHash = W1CanonicalJson.Digest(trust.ToJsonString(), "governanceProof");
        if (anchor.Standing != "development/conformance-only" ||
            !anchor.Subjects.Contains(requestedSubject) || anchor.Domain != Text(clock["domain"]) ||
            closure["completeForDeclaredScope"]?.GetValue<bool>() != true ||
            Text(closure["subject"]) != requestedSubject || Text(closure["domain"]) != Text(clock["domain"]) ||
            Text(governance["payloadSha256"]) != grantHash ||
            !W1CanonicalJson.Verify(grantHash, anchor.PublicKeyPem, Text(governance["signatureBase64"]))) return new("AUTHORITY_GAP");
        if (((JsonArray)trust["conflictingGrants"]!).Count != 0) return new("AUTHORITY_CONFLICT");
        if (!DateTimeOffset.TryParse(Text(s["observedAtUtc"]), out var observedAt) ||
            !DateTimeOffset.TryParse(Text(grant["effectiveFrom"]), out var from) ||
            !DateTimeOffset.TryParse(Text(grant["effectiveUntil"]), out var until)) return new("TEMPORAL_REFUSAL");
        if (observedAt < from || observedAt >= until) return new("AUTHORITY_GAP");
        if (Text(clock["phase"]) == "playing")
        {
            if (!((JsonArray)format["periods"]!).Any(p => Text(p?["key"]) == Text(clock["periodKey"])) ||
                clock["periodStart"] is null || clock["accumulated"] is null || clock["periodElapsed"] is null)
                return new("TEMPORAL_REFUSAL");
            if (clock["periodElapsed"]!.GetValue<int>() != clock["accumulated"]!.GetValue<int>() - clock["periodStart"]!.GetValue<int>())
                return new("TEMPORAL_REFUSAL");
        }
        if (Text(clock["state"]) == "running" && Text(clock["phase"]) != "playing") return new("TEMPORAL_REFUSAL");
        var scope = string.Join("\0", requestedSubject, Text(authority["principalId"]),
            Text(authority["bindingId"]), Text(authority["incarnationId"]));
        if (_conflicts.Contains(scope)) return new("AUTHORITY_CONFLICT");
        var order = BigInteger.Parse(Text(authority["sequence"]));
        var generation = BigInteger.Parse(Text(authority["generation"]));
        var seconds = clock["accumulated"]?.GetValue<int>() ?? 0;
        var outcome = Text(clock["method"]) == "utc-reconstructed" ? "QUALIFIED_RECONSTRUCTION" : "QUALIFIED";
        if (_heads.TryGetValue(scope, out var prior))
        {
            if (generation < prior.Generation || (generation == prior.Generation && order < prior.Order))
                return new("REORDERED_REFUSAL");
            if (generation == prior.Generation && order == prior.Order)
            {
                if (hash == prior.Digest) return new("REPLAY_NO_NEW_ACTIVITY");
                _conflicts.Add(scope); return new("AUTHORITY_CONFLICT");
            }
            if (seconds < prior.Seconds)
            {
                if (Text(history["kind"]) != "correction" || history["priorSeconds"]?.GetValue<int>() != prior.Seconds ||
                    history["newSeconds"]?.GetValue<int>() != seconds || Text(history["requiredCapability"]) != "correct" ||
                    !capabilities.Contains("correct")) return new("CORRECTION_REFUSAL");
                outcome = "QUALIFIED_CORRECTION";
            }
        }
        _heads[scope] = (generation, order, hash, seconds);
        return new(outcome, s);
    }
}