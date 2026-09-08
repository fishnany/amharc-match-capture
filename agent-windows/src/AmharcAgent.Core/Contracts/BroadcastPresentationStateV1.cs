using System.Text.Json.Serialization;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;

namespace AmharcAgent.Core.Contracts;

/// <summary>
/// Presentation-control state for the AMHARC broadcast graphics layer.
///
/// This contract deliberately contains presentation decisions only.
/// Canonical match truth such as score and clock state is carried separately
/// by ScoreState and ClockSnapshotV1.
/// </summary>
public sealed record BroadcastPresentationControlV1(
    string? ActiveTemplateId,
    bool ScoreboardVisible,
    [property: JsonConverter(typeof(OverlayOutputModeWireJsonConverter))]
    OverlayOutputMode OutputMode,
    string? ActiveGraphic,
    bool GraphicVisible);

/// <summary>
/// Versioned AMHARC broadcast presentation contract.
///
/// The contract composes canonical domain state rather than reconstructing it:
/// - Score is the canonical ScoreState produced by the scoring service.
/// - Clock is the canonical ClockSnapshotV1 produced by Capture.
/// - Presentation contains operator/broadcast presentation choices only.
///
/// Renderers must not calculate scores or independently advance the match clock.
/// </summary>
public sealed record BroadcastPresentationStateV1(
    string ContractVersion,
    string MatchId,
    ScoreState Score,
    ClockSnapshotV1 Clock,
    BroadcastPresentationControlV1 Presentation)
{
    public const string CurrentContractVersion = "1.0";

    /// <summary>
    /// Creates a broadcast presentation state from canonical score and clock
    /// contracts plus the current overlay presentation-control state.
    ///
    /// Legacy score and clock fields in OverlayState are intentionally ignored.
    /// They are retained temporarily only for compatibility with the Phase 1
    /// overlay implementation and are not authoritative broadcast state.
    /// </summary>
    public static BroadcastPresentationStateV1 FromCanonical(
        ScoreState score,
        ClockSnapshotV1 clock,
        OverlayState overlay)
    {
        ArgumentNullException.ThrowIfNull(score);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(overlay);

        if (!string.Equals(
                score.MatchId,
                clock.MatchId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Cannot compose broadcast presentation state because " +
                $"ScoreState match '{score.MatchId}' does not match " +
                $"ClockSnapshotV1 match '{clock.MatchId}'.");
        }

        return new BroadcastPresentationStateV1(
            CurrentContractVersion,
            score.MatchId,
            score,
            clock,
            new BroadcastPresentationControlV1(
                overlay.ActiveTemplateId,
                overlay.IsVisible,
                overlay.OutputMode,
                overlay.CurrentGraphic,
                overlay.GraphicVisible));
    }
}
