namespace AmharcAgent.Core.Domain;

/// <summary>Prospective correspondence, not a replacement for Match.MatchId.
/// Retained independently of deletion of a local representation.</summary>
public sealed class W1OccurrenceCreation
{
    public string Issuer { get; set; } = "";
    public string OperationKey { get; set; } = "";
    public string OccurrenceId { get; set; } = "";
    public string LocalMatchId { get; set; } = "";
    public string RequestSha256 { get; set; } = "";
    public string MaterialActivityJson { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}