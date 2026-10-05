using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using AmharcAgent.Core.Contracts;
using AmharcAgent.Infrastructure.Clock;

namespace AmharcAgent.Api.W1;

/// <summary>Explicit synthetic composition; no real appointments or network
/// dependency retrieval. Incoming scope requires proof over the complete request.</summary>
public sealed class W1MatchSetupApplication
{
    private readonly W1DependencyResolver _dependencies = new();
    private readonly W1MatchSetupAdmission _admission;
    private readonly string _originIssuer, _originPublicKey;
    public W1SqliteMatchSetupLedger Ledger { get; }
    public W1MatchSetupApplication(IConfiguration configuration)
    {
        string Required(string key) => configuration["W1:" + key] is { Length: > 0 } value ? value :
            throw new InvalidOperationException("W1_MATCH_SETUP_CONFIGURATION_UNRESOLVED");
        _originIssuer = Required("TaggerOriginIssuer");
        if (!_originIssuer.StartsWith("urn:amharc:test:", StringComparison.Ordinal))
            throw new InvalidOperationException("OPERATIONAL_CONTEXT_UNAPPOINTED");
        _originPublicKey = File.ReadAllText(Required("TaggerOriginPublicKeyFile"));
        foreach (var file in Directory.GetFiles(Required("DependencyDirectory"), "*.json", SearchOption.AllDirectories))
        {
            var bytes = File.ReadAllBytes(file); var document = JsonNode.Parse(bytes) as JsonObject;
            var id = document?["identifier"]?.GetValue<string>() ?? document?["$id"]?.GetValue<string>();
            if (id is not null) _dependencies.Add(new(id, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()), bytes);
        }
        _admission = new(_dependencies);
        var allocator = (JsonObject)JsonNode.Parse(Required("SetupAllocatorApplicabilityRef"))!;
        var eligibility = _admission.Resolve(allocator);
        if (W1MatchSetupAdmission.Text(eligibility["role"]) != "allocator-applicability" ||
            !W1MatchSetupAdmission.Text(eligibility["standing"]).StartsWith("TEST_ONLY", StringComparison.Ordinal))
            throw new InvalidOperationException("OPERATIONAL_CONTEXT_UNAPPOINTED");
        Ledger = new(Required("ScratchJournalPath"), Required("Issuer"), allocator, Required("Actor"), Required("Build"));
    }
    public JsonObject Receive(string bytes, string issuer, string signature)
    {
        var digest = W1CanonicalJson.Digest(bytes);
        var request = (JsonObject)JsonNode.Parse(bytes)!;
        try
        {
            var verified = issuer == _originIssuer && W1CanonicalJson.Verify(digest, _originPublicKey, signature);
            var scope = new W1SetupScope(_originIssuer, verified, true, ApplicableBasis);
            return Ledger.Apply(_admission.Admit(bytes, scope));
        }
        catch (InvalidOperationException e)
        {
            return W1SqliteMatchSetupLedger.Failure(request, digest, e.Message);
        }
    }
    public JsonObject RequireSyntheticSubject(string subject)
    {
        var setup = Ledger.Read(subject);
        if (setup is null ||
            W1MatchSetupAdmission.Text(setup["canonicalOccurrenceId"]) != subject ||
            W1MatchSetupAdmission.Text(setup["handlingApplicability"]) != "synthetic/test-local only" ||
            W1MatchSetupAdmission.Text(setup["taggerRepresentation"]?["issuer"]) != _originIssuer ||
            W1MatchSetupAdmission.Text(setup["captureRepresentation"]?["issuer"]) !=
                W1MatchSetupAdmission.Text(setup["captureResolution"]?["representation"]?["issuer"]) ||
            W1MatchSetupAdmission.Text(setup["captureResolution"]?["standing"])
                .StartsWith("TEST_ONLY", StringComparison.Ordinal) != true ||
            W1MatchSetupAdmission.Text(setup["taggerResolution"]?["standing"])
                .StartsWith("TEST_ONLY", StringComparison.Ordinal) != true)
            throw new InvalidOperationException("W1_ASSURANCE_SYNTHETIC_CONTEXT_REFUSAL");
        return setup;
    }
    private bool ApplicableBasis(JsonObject request)
    {
        try
        {
            foreach (var reference in new[] { request["originProofRef"], request["requestActivityRef"],
                request["compatibility"]?["taggerBuildRef"], request["assignment"]?["effectivity"]?["fromActivityRef"] })
                if (reference is null || !W1MatchSetupAdmission.Text(_admission.Resolve(reference)["standing"])
                    .StartsWith("TEST_ONLY", StringComparison.Ordinal)) return false;
            foreach (var member in (JsonObject)request["assignment"]!["basis"]!)
                if (member.Key.EndsWith("Ref", StringComparison.Ordinal) && member.Value is not null &&
                    !W1MatchSetupAdmission.Text(_admission.Resolve(member.Value)["standing"])
                        .StartsWith("TEST_ONLY", StringComparison.Ordinal)) return false;
            var eligibility = request["assignment"]?["extraTimeEligibility"]?["basisRef"];
            if (eligibility is not null) _ = _admission.Resolve(eligibility);
            return true;
        }
        catch (InvalidOperationException) { return false; }
    }
}