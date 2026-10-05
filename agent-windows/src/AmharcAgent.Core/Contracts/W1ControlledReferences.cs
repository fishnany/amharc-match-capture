using System.Text.Json.Nodes;

namespace AmharcAgent.Core.Contracts;

public static class W1ControlledReferences
{
    public static readonly W1Reference Manifest = new(
        "urn:amharc:w1:dependency-manifest:1.0.0",
        "53d737c6b1363e87beba89370af7a78db13bf0d80ecbffddc4705d934fe9bf08");
    public static void AdmitEnvelope(W1DependencyResolver resolver, JsonObject envelope, W1Reference schema)
    {
        var refs = resolver.Resolve(Manifest)["exactDependencies"]!;
        if (W1DependencyResolver.Reference(refs["schema"]!) != schema ||
            W1DependencyResolver.Reference(refs["contract"]!) != W1DependencyResolver.Reference(envelope["contractRef"]!))
            throw new InvalidOperationException("W1_CONTROLLED_CONTEXT_REFUSAL");
        foreach (var name in new[] { "semantic", "identifier", "provenance" })
            if (W1DependencyResolver.Reference(refs[name]!) != W1DependencyResolver.Reference(envelope["context"]![name]!))
                throw new InvalidOperationException("W1_CONTROLLED_CONTEXT_REFUSAL");
        AdmitFormat(resolver, envelope["context"]!["format"]!);
    }
    public static void AdmitFormat(W1DependencyResolver resolver, JsonNode reference)
    {
        var baseline = resolver.Resolve(Manifest)["exactDependencies"]!["format"]!;
        if (W1DependencyResolver.Reference(reference) == W1DependencyResolver.Reference(baseline)) return;
        var application = new W1MatchSetupAdmission(resolver);
        // Clock Envelope references deliberately remain their unchanged id/hash
        // shape. The exact content-addressed definition carries its own version.
        var definition = resolver.Resolve(W1DependencyResolver.Reference(reference));
        if (!application.FormatValid(definition))
            throw new InvalidOperationException("W1_CONTROLLED_CONTEXT_REFUSAL");
    }
}