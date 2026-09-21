namespace AmharcAgent.Core.Models;

public sealed record ProtectedCredential(string Username, string Secret)
{
    public override string ToString() => "ProtectedCredential { Username = [redacted], Secret = [redacted] }";
}
