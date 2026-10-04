using System.Text.Json.Nodes;

namespace AmharcAgent.Core.Contracts;

public sealed class W1VerifiedDevelopmentGrant
{
    internal W1VerifiedDevelopmentGrant(JsonObject grant, JsonObject evidence)
    {
        Grant = (JsonObject)grant.DeepClone();
        Evidence = (JsonObject)evidence.DeepClone();
    }
    internal JsonObject Grant { get; }
    private JsonObject Evidence { get; }
    public JsonObject Copy() => (JsonObject)Grant.DeepClone();
    public JsonObject CopyEvidence() => (JsonObject)Evidence.DeepClone();
}

public sealed class W1DevelopmentGrantVerifier(W1DevelopmentTrustAnchor anchor)
{
    public W1VerifiedDevelopmentGrant Verify(JsonObject bundle, string subject, string incarnation)
    {
        var closure = bundle["closure"];
        var grant = bundle["grant"] as JsonObject;
        var proof = bundle["governanceProof"];
        var digest = W1CanonicalJson.Digest(bundle.ToJsonString(), "governanceProof");
        if (anchor.Standing != "development/conformance-only" || !anchor.Subjects.Contains(subject) ||
            closure?["completeForDeclaredScope"]?.GetValue<bool>() != true ||
            closure["subject"]?.GetValue<string>() != subject ||
            closure["domain"]?.GetValue<string>() != anchor.Domain ||
            grant?["subject"]?.GetValue<string>() != subject ||
            grant["domain"]?.GetValue<string>() != anchor.Domain ||
            grant["incarnationId"]?.GetValue<string>() != incarnation ||
            string.IsNullOrWhiteSpace(grant["principalId"]?.GetValue<string>()) ||
            string.IsNullOrWhiteSpace(grant["bindingId"]?.GetValue<string>()) ||
            (bundle["conflictingGrants"] as JsonArray)?.Count != 0 ||
            proof?["payloadSha256"]?.GetValue<string>() != digest ||
            !W1CanonicalJson.Verify(digest, anchor.PublicKeyPem, proof["signatureBase64"]!.GetValue<string>()))
            throw new InvalidOperationException("W1_UNTRUSTED_OR_INCOMPLETE_CONTROL_PLANE");
        return new(grant, bundle);
    }
}