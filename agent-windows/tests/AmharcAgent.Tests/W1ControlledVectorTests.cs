using System.Security.Cryptography;
using System.Text.Json.Nodes;
using AmharcAgent.Core.Contracts;
using Xunit;

namespace AmharcAgent.Tests;

public sealed class W1ControlledVectorTests
{
    internal static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, ".git"))) d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("Genuine candidate source root missing");
    }
    internal static string DependenciesDirectory() => Path.Combine(Root(), "governance", "wave-1", "v1.0.0");
    internal static W1DependencyResolver LoadDependencies(bool omitSemantic = false)
    {
        var r = new W1DependencyResolver();
        foreach (var path in Directory.GetFiles(DependenciesDirectory(), "*.json", SearchOption.AllDirectories))
        {
            if (omitSemantic && Path.GetFileName(path) == "W1_SEMANTIC_PROFILE.json") continue;
            var bytes = File.ReadAllBytes(path);
            var document = JsonNode.Parse(bytes)!;
            var id = document["identifier"]?.GetValue<string>() ?? document["$id"]!.GetValue<string>();
            r.Add(new(id, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()), bytes);
        }
        return r;
    }
    internal static W1Reference Schema()
    {
        var bytes = File.ReadAllBytes(Path.Combine(DependenciesDirectory(), "W1_CLOCK_ENVELOPE_SCHEMA.json"));
        return new(JsonNode.Parse(bytes)!["$id"]!.GetValue<string>(),
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }
    [Fact]
    public void ControlledSemantic_All39_ActualManagedValidationAndReplayState()
    {
        var vectors = JsonNode.Parse(File.ReadAllText(Path.Combine(DependenciesDirectory(), "W1_CONFORMANCE_VECTORS.json")))!;
        var cases = (JsonArray)vectors["cases"]!;
        Assert.Equal(39, cases.Count);
        var baseline = cases[0]!["input"]!["snapshot"]!;
        var reconstructed = cases.First(c => c!["id"]!.GetValue<string>().StartsWith("V30"))!["input"]!["snapshot"]!;
        var key = File.ReadAllText(Path.Combine(DependenciesDirectory(), "fixtures", "governance-public-key.pem"));
        var results = new List<object>();
        var failures = new List<string>();
        foreach (var item in cases)
        {
            var input = item!["input"]!;
            var expected = item["expected"]!.GetValue<string>();
            var requested = input["requestedSubject"]!.GetValue<string>();
            var subjects = input["controlPlaneClosureEstablished"]?.GetValue<bool>() == false
                ? new HashSet<string>() : new HashSet<string> { baseline["subject"]!["occurrenceId"]!.GetValue<string>() };
            var validator = new W1ObservationValidator(
                LoadDependencies(input["availableDependencies"]?.GetValue<bool>() == false),
                Schema(), new(key, subjects, "official-match-accumulated"));
            if (input["prior"] is not null || input["replay"]?.GetValue<bool>() == true)
                Assert.Equal("QUALIFIED", validator.Validate(baseline.ToJsonString(), baseline["subject"]!["occurrenceId"]!.GetValue<string>()).Qualification);
            if (input["sameOrderPriorDifferentPayload"]?.GetValue<bool>() == true)
                Assert.Equal("QUALIFIED_RECONSTRUCTION", validator.Validate(reconstructed.ToJsonString(), requested).Qualification);
            var observed = validator.Validate(input["snapshot"]!.ToJsonString(), requested).Qualification;
            var id = item["id"]!.GetValue<string>();
            results.Add(new { id, expected, observed, pass = expected == observed });
            if (expected != observed) failures.Add($"{id}: expected {expected}; observed {observed}");
        }
        File.WriteAllText(Path.Combine(Directory.GetParent(Root())!.FullName, "capture-semantic-results.json"),
            System.Text.Json.JsonSerializer.Serialize(new { role = "Capture managed product validator", results },
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}