using System.Text.Json.Nodes;
using AmharcAgent.Core.Contracts;
using Json.Schema;

namespace AmharcAgent.Infrastructure.Clock;

/// <summary>A distinct development producer boundary. It never mutates
/// ClockSnapshotV1, aliases its MatchId or declares recording time media time.</summary>
public sealed class W1GovernedClockProducer(
    W1SubjectBoundRuntime runtime, W1DependencyResolver resolver,
    W1Reference contractReference, W1Reference schemaReference,
    W1Reference observationClosureReference,
    string keyId, byte[] developmentSigningSeed,
    Func<string, int?> readLegacyRecordingCoordinate, W1DevelopmentTrustAnchor anchor)
{
    private readonly W1ObservationValidator _validator = new(resolver, schemaReference, anchor);
    public JsonObject Emit(string requestedCanonicalSubject) => runtime.CaptureCoherently(
        requestedCanonicalSubject, (captured, material) =>
        {
            _ = resolver.Resolve(contractReference);
            _ = resolver.Resolve(observationClosureReference);
            _ = resolver.Resolve(material);
            var schema = JsonSchema.FromText(resolver.Resolve(schemaReference).ToJsonString());
            var body = (JsonObject)captured.DeepClone();
            body.Remove("activityId");
            body["contractRef"] = Reference(contractReference);
            body["context"]!["trustBundle"] = Reference(observationClosureReference);
            body["activityRef"] = Reference(material);
            var localId = body["subject"]!["representation"]!["localId"]!.GetValue<string>();
            body["legacyRecording"] = new JsonObject {
                ["coordinate"] = "recordingElapsedSeconds", ["producerContract"] = "1.0",
                ["value"] = readLegacyRecordingCoordinate(localId),
                ["qualification"] = "legacy-producer-coordinate-not-media-time"
            };
            var digest = W1CanonicalJson.Digest(body.ToJsonString());
            body["attestation"] = new JsonObject {
                ["keyId"] = keyId, ["algorithm"] = "Ed25519", ["payloadSha256"] = digest,
                ["signatureBase64"] = W1CanonicalJson.Sign(digest, developmentSigningSeed)
            };
            if (!schema.Evaluate(body, new EvaluationOptions { RequireFormatValidation = true }).IsValid)
                throw new InvalidOperationException("W1_PRODUCER_SCHEMA_REFUSAL");
            var result = _validator.Validate(body.ToJsonString(), requestedCanonicalSubject);
            if (!result.Qualification.StartsWith("QUALIFIED", StringComparison.Ordinal))
                throw new InvalidOperationException("W1_PRODUCER_" + result.Qualification);
            return body;
        });

    private static JsonObject Reference(W1Reference r) => new() { ["id"] = r.Id, ["sha256"] = r.Sha256 };
}