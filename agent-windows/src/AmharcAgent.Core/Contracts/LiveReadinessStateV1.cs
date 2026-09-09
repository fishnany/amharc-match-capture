using System.Text.Json.Serialization;

namespace AmharcAgent.Core.Contracts;

/// <summary>
/// Overall operational readiness classification for running a live match.
///
/// This is a projection over authoritative subsystem state. It does not own,
/// connect, start, stop, repair or mutate any subsystem.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LiveReadinessStatusV1
{
    Ready,
    Degraded,
    Blocked
}

/// <summary>
/// Severity of an individual live-readiness finding.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LiveReadinessSeverityV1
{
    Info,
    Warning,
    Blocking
}

/// <summary>
/// Stable identifier for a readiness dimension.
///
/// The dimension identifies the authoritative subsystem or operational concern
/// being evaluated by the readiness projection.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LiveReadinessDimensionV1
{
    Agent,
    Database,
    Match,
    Clock,
    Camera,
    Video,
    Audio,
    Ffmpeg,
    Recording,
    Storage,
    StreamDeck,
    StreamDeckOwnership,
    Joystick,
    Streaming,
    Broadcast
}

/// <summary>
/// One evaluated live-readiness dimension.
/// </summary>
public sealed record LiveReadinessCheckV1(
    LiveReadinessDimensionV1 Dimension,
    LiveReadinessStatusV1 Status,
    bool Required,
    string Summary,
    string? Detail = null);

/// <summary>
/// One operator-facing readiness finding.
///
/// Findings explain why the overall state is degraded or blocked without
/// becoming an independent source of subsystem truth.
/// </summary>
public sealed record LiveReadinessFindingV1(
    LiveReadinessDimensionV1 Dimension,
    LiveReadinessSeverityV1 Severity,
    string Code,
    string Message);

/// <summary>
/// Versioned AMHARC Capture live-readiness contract.
///
/// The contract answers whether a specific live-match operating context is
/// safe to proceed. It composes authoritative subsystem state and never owns
/// or mutates that state.
/// </summary>
public sealed record LiveReadinessStateV1(
    string ContractVersion,
    string? MatchId,
    LiveReadinessStatusV1 Status,
    bool Ready,
    IReadOnlyList<LiveReadinessCheckV1> Checks,
    IReadOnlyList<LiveReadinessFindingV1> Findings,
    DateTimeOffset ObservedAtUtc)
{
    public const string CurrentContractVersion = "1.0";

    public static LiveReadinessStateV1 Create(
        string? matchId,
        IEnumerable<LiveReadinessCheckV1> checks,
        IEnumerable<LiveReadinessFindingV1>? findings = null,
        DateTimeOffset? observedAtUtc = null)
    {
        var checkList =
            checks?.ToArray()
            ?? throw new ArgumentNullException(nameof(checks));

        var findingList =
            findings?.ToArray()
            ?? [];

        var blockingRequiredCheck =
            checkList.Any(
                check =>
                    check.Required &&
                    check.Status == LiveReadinessStatusV1.Blocked);

        var blockingFinding =
            findingList.Any(
                finding =>
                    finding.Severity == LiveReadinessSeverityV1.Blocking);

        var degraded =
            checkList.Any(
                check =>
                    check.Status == LiveReadinessStatusV1.Degraded) ||
            findingList.Any(
                finding =>
                    finding.Severity == LiveReadinessSeverityV1.Warning);

        var status =
            blockingRequiredCheck || blockingFinding
                ? LiveReadinessStatusV1.Blocked
                : degraded
                    ? LiveReadinessStatusV1.Degraded
                    : LiveReadinessStatusV1.Ready;

        return new LiveReadinessStateV1(
            CurrentContractVersion,
            matchId,
            status,
            status == LiveReadinessStatusV1.Ready,
            checkList,
            findingList,
            observedAtUtc ?? DateTimeOffset.UtcNow);
    }
}