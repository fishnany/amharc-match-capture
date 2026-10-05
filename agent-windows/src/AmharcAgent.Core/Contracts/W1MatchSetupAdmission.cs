using System.Text.Json.Nodes;
using Json.Schema;

namespace AmharcAgent.Core.Contracts;

public sealed record W1SetupScope(string AuthenticatedIssuer, bool OriginVerified,
    bool AllocatorEligible, Func<JsonObject, bool> ApplicableBasis,
    string Environment = "CONFORMANCE");

public sealed record W1AdmittedSetup(JsonObject Request, JsonObject Format, string RequestDigest);

/// <summary>Offline exact controlled application boundary. Scope is supplied by the
/// authenticated composition, never accepted as a caller's JSON assertion.</summary>
public sealed class W1MatchSetupAdmission
{
    public static readonly W1Reference Manifest = new(
        "urn:amharc:w1:match-setup-dependency-manifest:1.0.0",
        "4d50181d62f900d619ddb2fc45c26d4457b0121380ca041aa297c132957c7e62");
    private readonly W1DependencyResolver _resolver;
    private readonly Dictionary<string, JsonObject> _members = new();
    private readonly JsonSchema _requestSchema, _formatSchema;
    private readonly EvaluationOptions _schemaOptions;
    public W1MatchSetupAdmission(W1DependencyResolver resolver)
    {
        _resolver = resolver;
        var manifest = resolver.Resolve(Manifest);
        foreach (var member in (JsonObject)manifest["controlledMembers"]!)
        {
            var reference = (JsonObject)member.Value!["reference"]!;
            _members[member.Key] = (JsonObject)reference.DeepClone();
            _ = Resolve(reference);
        }
        foreach (var baseline in (JsonObject)manifest["existingBaselines"]!)
            _ = Resolve(baseline.Value!);
        var schema = _members.Values.Single(r => Text(r["id"]) == "urn:amharc:w1:match-setup-handoff-schema:1.0.0");
        var format = _members.Values.Single(r => Text(r["id"]) == "urn:amharc:w1:match-format-definition-schema:1.0.0");
        _requestSchema = JsonSchema.FromText(Resolve(schema).ToJsonString());
        _formatSchema = JsonSchema.FromText(Resolve(format).ToJsonString());
        _schemaOptions = new() { RequireFormatValidation = true };
        _schemaOptions.SchemaRegistry.Register(_requestSchema);
        _schemaOptions.SchemaRegistry.Register(_formatSchema);
    }
    public static string Text(JsonNode? n) => n?.GetValue<string>() ?? "";
    public JsonObject Resolve(JsonNode reference)
    {
        var document = _resolver.Resolve(W1DependencyResolver.Reference(reference));
        var version = Text(document["version"]);
        if (version.Length != 0 && version != Text(reference["version"]))
            throw new InvalidOperationException("W1_DEPENDENCY_VERSION_REFUSAL");
        return document;
    }
    /// <summary>Recovery composition resolves the checkpoint's complete exact
    /// reference before the pure policy compares it with the bound context.
    /// No identity-only fallback, network lookup or vector-specific digest rule.</summary>
    public JsonObject ResolveRecoveryFormat(JsonNode? reference)
    {
        try
        {
            if (reference is not JsonObject ||
                Text(reference["id"]).Length == 0 ||
                Text(reference["version"]).Length == 0 ||
                Text(reference["sha256"]).Length == 0)
                throw new InvalidOperationException("W1_DEPENDENCY_STRUCTURE");
            var format = Resolve(reference);
            if (Text(format["version"]) != Text(reference["version"]))
                throw new InvalidOperationException("W1_DEPENDENCY_VERSION_REFUSAL");
            return format;
        }
        catch (InvalidOperationException)
        {
            throw new InvalidOperationException("RECOVERY_FORMAT_UNRESOLVED");
        }
    }
    public void AdmitRecovery(JsonObject checkpoint, JsonObject boundContext, bool newIncarnationEligible)
    {
        var format = ResolveRecoveryFormat(checkpoint["formatRef"]);
        if (!FormatValid(format)) throw new InvalidOperationException("FORMAT_INVALID");
        new W1OrderedMatchFormat(format).AdmitRecovery(checkpoint, boundContext, newIncarnationEligible);
    }
    private void ExactControlled(JsonNode? reference, string identity, string code)
    {
        var expected = _members.Values.Single(r => Text(r["id"]) == identity);
        if (!JsonNode.DeepEquals(reference, expected)) throw new InvalidOperationException(code);
    }
    public bool FormatValid(JsonObject format) => _formatSchema.Evaluate(format, _schemaOptions).IsValid;
    public bool RequestValid(JsonObject request) => _requestSchema.Evaluate(request, _schemaOptions).IsValid;
    public W1AdmittedSetup Admit(string raw, W1SetupScope scope)
    {
        var digest = W1CanonicalJson.Digest(raw);
        var request = JsonNode.Parse(raw) as JsonObject ?? throw new InvalidOperationException("INVALID_REQUEST");
        ExactControlled(request["contractRef"], "urn:amharc:w1:match-setup-handoff-contract:1.0.0", "UNSUPPORTED_CONTRACT");
        if (request["compatibility"]?["requiredHandoffMajor"]?.GetValue<int>() != 1)
            throw new InvalidOperationException("UNSUPPORTED_CONTRACT");
        if (!scope.OriginVerified || Text(request["taggerRepresentation"]?["issuer"]) != scope.AuthenticatedIssuer)
            throw new InvalidOperationException("ORIGIN_UNVERIFIED");
        if (scope.Environment != "CONFORMANCE" || Text(request["environment"]) != "CONFORMANCE")
            throw new InvalidOperationException("OPERATIONAL_CONTEXT_UNAPPOINTED");
        if (!scope.AllocatorEligible) throw new InvalidOperationException("ALLOCATOR_UNAVAILABLE");
        if (!JsonNode.DeepEquals(request["taggerRepresentation"], request["assignment"]?["taggerRepresentation"]))
            throw new InvalidOperationException("TAGGER_REPRESENTATION_MISMATCH");
        ExactControlled(request["sportMappingRef"], "urn:amharc:w1:sport-code-mapping:1.0.0", "UNSUPPORTED_SPORT");
        var mapping = Resolve(request["sportMappingRef"]!);
        var pair = ((JsonArray)mapping["mappings"]!).SingleOrDefault(p =>
            Text(p?["hubLabel"]) == Text(request["hubSportLabel"]));
        if (pair is null || Text(request["sport"]) != Text(mapping["sport"]) ||
            Text(request["code"]) != Text(pair["code"]))
            throw new InvalidOperationException("UNSUPPORTED_SPORT");
        JsonObject format;
        if (Text(request["formatDelivery"]?["mode"]) == "INLINE")
        {
            var delivered = request["formatDelivery"]!["document"] as JsonObject
                ?? throw new InvalidOperationException("FORMAT_INVALID");
            if (!FormatValid(delivered)) throw new InvalidOperationException("FORMAT_INVALID");
            if (W1CanonicalJson.Digest(delivered.ToJsonString()) != Text(request["formatRef"]?["sha256"]))
                throw new InvalidOperationException("FORMAT_DIGEST_MISMATCH");
            format = delivered;
        }
        else
        {
            try { format = Resolve(request["formatRef"]!); }
            catch (InvalidOperationException) { throw new InvalidOperationException("FORMAT_UNRESOLVED"); }
        }
        if (!FormatValid(format)) throw new InvalidOperationException("FORMAT_INVALID");
        if (!RequestValid(request)) throw new InvalidOperationException("INVALID_REQUEST");
        ExactControlled(request["compatibility"]?["applicationProfileRef"],
            "urn:amharc:w1:match-format-application:1.0.0", "UNSUPPORTED_CONTRACT");
        ExactControlled(request["compatibility"]?["requestSchemaRef"],
            "urn:amharc:w1:match-setup-handoff-schema:1.0.0", "UNSUPPORTED_CONTRACT");
        if (Text(format["sport"]) != Text(request["sport"]) || Text(format["code"]) != Text(request["code"]) ||
            Text(format["matchContext"]) != Text(request["matchContext"]))
            throw new InvalidOperationException("SPORT_CONTEXT_MISMATCH");
        var assignment = (JsonObject)request["assignment"]!;
        if (!JsonNode.DeepEquals(assignment["formatRef"], request["formatRef"]) ||
            Text(assignment["identifier"]) != Text(request["assignmentRef"]?["id"]) ||
            Text(assignment["version"]) != Text(request["assignmentRef"]?["version"]) ||
            W1CanonicalJson.Digest(assignment.ToJsonString()) != Text(request["assignmentRef"]?["sha256"]))
            throw new InvalidOperationException("ASSIGNMENT_REF_MISMATCH");
        if (Text(assignment["standing"]) != "TEST_ONLY" || Text(format["standing"]) != "TEST_ONLY" ||
            !scope.ApplicableBasis(request)) throw new InvalidOperationException("FORMAT_APPLICABILITY_UNPROVEN");
        return new(request, format, digest);
    }
}