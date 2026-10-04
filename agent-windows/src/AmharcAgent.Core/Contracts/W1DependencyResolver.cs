using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace AmharcAgent.Core.Contracts;

public sealed record W1Reference(string Id, string Sha256);

/// <summary>Exact, locally supplied bytes. Neither network fallback nor a moving
/// context/default is accepted. Ancillary governance is independently appointed.</summary>
public sealed class W1DependencyResolver
{
    private readonly Dictionary<W1Reference, byte[]> _bytes = new();

    public void Add(W1Reference reference, byte[] bytes)
    {
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (hash != reference.Sha256 || reference.Id is "latest" or "default")
            throw new InvalidOperationException("W1_DEPENDENCY_DIGEST_REFUSAL");
        if (_bytes.TryGetValue(reference, out var existing) && !existing.AsSpan().SequenceEqual(bytes))
            throw new InvalidOperationException("W1_DEPENDENCY_CONFLICT");
        _bytes[reference] = bytes.ToArray();
    }

    public JsonObject Resolve(W1Reference reference)
    {
        if (!_bytes.TryGetValue(reference, out var bytes))
            throw new InvalidOperationException("W1_DEPENDENCY_UNRESOLVED");
        var raw = new UTF8Encoding(false, true).GetString(bytes);
        // Hash-exact dependency containers can carry deliberately invalid
        // conformance inputs. Narrow envelope-number admission is separate.
        var value = JsonNode.Parse(raw) as JsonObject
            ?? throw new InvalidOperationException("W1_DEPENDENCY_STRUCTURE");
        var identity = value["identifier"]?.GetValue<string>() ?? value["$id"]?.GetValue<string>();
        if (identity != reference.Id) throw new InvalidOperationException("W1_DEPENDENCY_ID_REFUSAL");
        return value;
    }

    public static W1Reference Reference(JsonNode node) =>
        new(node["id"]!.GetValue<string>(), node["sha256"]!.GetValue<string>());
}