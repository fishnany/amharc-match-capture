using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using AmharcAgent.Core.Contracts;
using AmharcAgent.Core.Domain;
using AmharcAgent.Data;
using AmharcAgent.Data.Repositories;
using AmharcAgent.Infrastructure.Clock;
using Microsoft.EntityFrameworkCore;

namespace AmharcAgent.Api.W1;

/// <summary>One explicitly development-only bound subject. Governance evidence is
/// supplied externally; this composition never signs grants or appoints real authority.</summary>
public sealed class W1DevelopmentApplication
{
    private readonly object _gate = new();
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _time;
    private readonly W1DependencyResolver _dependencies = new();
    private readonly W1SqliteClockJournal _journal;
    private readonly string _issuer, _actor, _build, _anchorPem;
    private readonly Dictionary<string, string> _exact = new();
    private W1SubjectBoundRuntime? _runtime;
    private W1DevelopmentTrustAnchor? _anchor;
    private JsonObject? _identity, _grant;
    private W1GovernedClockProducer? _producer;
    private bool _conflict;

    public W1DevelopmentApplication(IConfiguration configuration, TimeProvider? time = null)
    {
        _configuration = configuration; _time = time ?? TimeProvider.System;
        string Required(string key) => configuration["W1:" + key] is { Length: > 0 } value ? value :
            throw new InvalidOperationException("W1_DEVELOPMENT_CONFIGURATION_UNRESOLVED");
        _issuer = Required("Issuer"); _actor = Required("Actor"); _build = Required("Build");
        _anchorPem = File.ReadAllText(Required("GovernancePublicKeyFile"));
        var directory = Required("DependencyDirectory");
        foreach (var path in Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories))
        {
            var bytes = File.ReadAllBytes(path); var d = JsonNode.Parse(bytes) as JsonObject;
            if (d?["identifier"] is not null || d?["$id"] is not null)
                _dependencies.Add(new(d["identifier"]?.GetValue<string>() ?? d["$id"]!.GetValue<string>(),
                    Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()), bytes);
        }
        var refs = _dependencies.Resolve(W1ControlledReferences.Manifest)["exactDependencies"]!;
        foreach (var name in new[] { "semantic", "identifier", "provenance", "format", "schema", "contract" })
        {
            var r = W1DependencyResolver.Reference(refs[name]!); _ = _dependencies.Resolve(r);
            _exact[r.Id] = r.Sha256;
        }
        _journal = new(Required("ScratchJournalPath"));
    }

    public async Task<Match> CreateAsync(AmharcDbContext db, Match request, string operationKey, CancellationToken ct)
    {
        var issued = await new W1ProspectiveCreationRepository(db).CreateAsync(
            _issuer, operationKey, request, _actor, _build, _exact, ct);
        return await db.Matches.SingleOrDefaultAsync(m => m.MatchId == issued.LocalMatchId, ct)
            ?? throw new InvalidOperationException("W1_ISSUED_REPRESENTATION_DELETED");
    }

    public async Task<object> PrepareAsync(AmharcDbContext db, string localId, JsonObject resolution, CancellationToken ct)
    {
        var issued = await db.Set<W1OccurrenceCreation>().SingleOrDefaultAsync(
            x => x.Issuer == _issuer && x.LocalMatchId == localId, ct)
            ?? throw new InvalidOperationException("W1_PROSPECTIVE_CORRESPONDENCE_UNRESOLVED");
        if (resolution["occurrenceId"]?.GetValue<string>() != issued.OccurrenceId ||
            resolution["representation"]?["issuer"]?.GetValue<string>() != _issuer ||
            resolution["representation"]?["localId"]?.GetValue<string>() != localId ||
            resolution["identityStanding"]?.GetValue<string>() != "provisional")
            throw new InvalidOperationException("W1_SUBJECT_REFUSAL");
        lock (_gate)
        {
            if (_runtime is not null) throw new InvalidOperationException("W1_ALREADY_BOUND");
            var resolutionRef = Add(resolution);
            _identity = new JsonObject {
                ["occurrenceId"] = issued.OccurrenceId, ["representation"] = resolution["representation"]!.DeepClone(),
                ["identityStanding"] = "provisional", ["resolutionRef"] = Ref(resolutionRef)
            };
            var refs = _dependencies.Resolve(W1ControlledReferences.Manifest)["exactDependencies"]!;
            var context = new JsonObject();
            foreach (var name in new[] { "semantic", "identifier", "provenance", "format" })
                context[name] = refs[name]!.DeepClone();
            // Not eligible until activation establishes an exact externally signed
            // historical trust reference. Restarts retain the original exact context.
            var stored = _journal.Read(issued.OccurrenceId);
            context["trustBundle"] = stored is null
                ? Ref(new("urn:amharc:development:pending-trust:" + issued.OccurrenceId, new string('0', 64)))
                : JsonNode.Parse(stored.ContextJson)!["context"]!["trustBundle"]!.DeepClone();
            _anchor = new(_anchorPem, new HashSet<string> { issued.OccurrenceId }, "official-match-accumulated");
            _runtime = new(_identity, context, _journal, _actor, _build,
                _configuration["W1:InitialPeriodKey"] ?? throw new InvalidOperationException("W1_FORMAT_UNRESOLVED"),
                _dependencies, _time);
            return new { subject = issued.OccurrenceId, incarnationId = _runtime.IncarnationId,
                resolutionRef, standing = "development/conformance-only" };
        }
    }

    public void Activate(string subject, JsonObject externallySignedBundle, string operationKey)
    {
        lock (_gate)
        {
            var runtime = Bound(subject);
            var proof = externallySignedBundle["governanceProof"]!;
            var digest = W1CanonicalJson.Digest(externallySignedBundle.ToJsonString(), "governanceProof");
            if (externallySignedBundle["conflictingGrants"] is JsonArray { Count: > 0 } &&
                externallySignedBundle["closure"]?["subject"]?.GetValue<string>() == subject &&
                externallySignedBundle["closure"]?["domain"]?.GetValue<string>() == _anchor!.Domain &&
                externallySignedBundle["grant"]?["subject"]?.GetValue<string>() == subject &&
                proof["payloadSha256"]?.GetValue<string>() == digest &&
                W1CanonicalJson.Verify(digest, _anchor!.PublicKeyPem, proof["signatureBase64"]!.GetValue<string>()))
            { _conflict = true; throw new InvalidOperationException("W1_AUTHORITY_CONFLICT"); }
            if (W1DependencyResolver.Reference(externallySignedBundle["resolutionRef"]!) !=
                W1DependencyResolver.Reference(_identity!["resolutionRef"]!))
                throw new InvalidOperationException("W1_SUBJECT_REFUSAL");
            var verified = new W1DevelopmentGrantVerifier(_anchor!).Verify(
                externallySignedBundle, subject, runtime.IncarnationId);
            var historicalReference = Add(externallySignedBundle);
            runtime.AdoptVerifiedDevelopmentGrant(subject, verified, operationKey, historicalReference);
            _grant = (JsonObject)externallySignedBundle.DeepClone();
        }
    }

    public void Command(string subject, string operation, string key, int? seconds = null, string? period = null, string basis = "")
    {
        lock (_gate)
        {
            var runtime = Bound(subject);
            if (operation == "tick") runtime.Tick(subject);
            else if (operation == "recover") runtime.Restore(subject, key, new(_anchor!), _dependencies);
            else runtime.Transition(subject, operation, key, seconds, period, basis: basis);
        }
    }

    public object History(string subject)
    {
        lock (_gate)
        {
            Bound(subject);
            var checkpoint = _journal.Read(subject) ?? throw new InvalidOperationException("W1_HISTORY_UNRESOLVED");
            var head = W1MaterialChain.CurrentHead(_journal, checkpoint, _dependencies);
            var records = _journal.ReadActivities(subject);
            return new { head = Ref(head), membership = records.Select(a => Ref(W1MaterialChain.Reference(a))).ToArray() };
        }
    }

    public void InstallObservationClosure(string subject, JsonObject signedClosure)
    {
        lock (_gate)
        {
            var runtime = Bound(subject);
            if (_grant is null || !JsonNode.DeepEquals(signedClosure["grant"], _grant["grant"]))
                throw new InvalidOperationException("W1_BINDING_REFUSAL");
            _ = new W1DevelopmentGrantVerifier(_anchor!).Verify(signedClosure, subject, runtime.IncarnationId);
            var checkpoint = _journal.Read(subject) ?? throw new InvalidOperationException("W1_HISTORY_UNRESOLVED");
            var head = W1MaterialChain.CurrentHead(_journal, checkpoint, _dependencies);
            if (signedClosure["currentMaterialHead"] is null ||
                W1DependencyResolver.Reference(signedClosure["currentMaterialHead"]!) != head)
                throw new InvalidOperationException("W1_HISTORY_HEAD_REFUSAL");
            if (W1MaterialChain.Validate(head, subject, (JsonObject)JsonNode.Parse(checkpoint.ContextJson)!["context"]!,
                _dependencies, new(_anchor!), (JsonArray)signedClosure["materialHistory"]!) != "QUALIFIED")
                throw new InvalidOperationException("W1_LEGACY_ASSURANCE_GAP");
            var closureRef = Add(signedClosure);
            var refs = _dependencies.Resolve(W1ControlledReferences.Manifest)["exactDependencies"]!;
            // Explicit synthetic producer key supplied by local development configuration.
            // Never include private material in dependency exports or observation responses.
            var seed = Convert.FromBase64String(File.ReadAllText(
                _configuration["W1:DevelopmentSigningSeedFile"]
                    ?? throw new InvalidOperationException("W1_DEVELOPMENT_SIGNING_KEY_UNRESOLVED")).Trim());
            _producer = new(runtime, _dependencies, W1DependencyResolver.Reference(refs["contract"]!),
                W1DependencyResolver.Reference(refs["schema"]!), closureRef,
                signedClosure["keyId"]!.GetValue<string>(), seed, _ => null, _anchor!);
        }
    }

    public JsonObject Observe(string subject)
    {
        lock (_gate)
        {
            Bound(subject);
            return (_producer ?? throw new InvalidOperationException("W1_OBSERVATION_CLOSURE_UNRESOLVED")).Emit(subject);
        }
    }
    public object Dependencies() { lock (_gate) return _dependencies.Export(); }
    private W1SubjectBoundRuntime Bound(string subject)
    {
        if (_runtime is null || _runtime.Subject != subject) throw new InvalidOperationException("W1_SUBJECT_REFUSAL");
        if (_conflict) throw new InvalidOperationException("W1_AUTHORITY_CONFLICT");
        return _runtime;
    }
    private W1Reference Add(JsonObject value)
    {
        var bytes = Encoding.UTF8.GetBytes(value.ToJsonString());
        var r = new W1Reference(value["identifier"]!.GetValue<string>(),
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        _dependencies.Add(r, bytes); return r;
    }
    public static JsonObject Ref(W1Reference r) => new() { ["id"] = r.Id, ["sha256"] = r.Sha256 };
}