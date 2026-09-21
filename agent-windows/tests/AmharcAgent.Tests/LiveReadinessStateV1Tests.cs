using System.Text.Json;
using AmharcAgent.Core.Contracts;
using FluentAssertions;
using Xunit;

namespace AmharcAgent.Tests;

public class LiveReadinessStateV1Tests
{
    [Fact]
    public void Create_WhenAllRequiredChecksAreReady_ReturnsReady()
    {
        var state =
            LiveReadinessStateV1.Create(
                matchId: "match-1",
                checks:
                [
                    new(
                        LiveReadinessDimensionV1.Agent,
                        LiveReadinessStatusV1.Ready,
                        Required: true,
                        Summary: "Agent ready"),
                    new(
                        LiveReadinessDimensionV1.Camera,
                        LiveReadinessStatusV1.Ready,
                        Required: true,
                        Summary: "Camera ready"),
                    new(
                        LiveReadinessDimensionV1.Broadcast,
                        LiveReadinessStatusV1.Blocked,
                        Required: false,
                        Summary: "Broadcast unavailable")
                ],
                observedAtUtc:
                    new DateTimeOffset(
                        2026, 9, 9, 12, 0, 0, TimeSpan.Zero));

        state.ContractVersion.Should().Be("1.0");
        state.MatchId.Should().Be("match-1");
        state.Status.Should().Be(LiveReadinessStatusV1.Ready);
        state.Ready.Should().BeTrue();
    }

    [Fact]
    public void Create_WhenRequiredCheckIsBlocked_ReturnsBlocked()
    {
        var state =
            LiveReadinessStateV1.Create(
                matchId: "match-1",
                checks:
                [
                    new(
                        LiveReadinessDimensionV1.Storage,
                        LiveReadinessStatusV1.Blocked,
                        Required: true,
                        Summary: "Insufficient storage")
                ]);

        state.Status.Should().Be(LiveReadinessStatusV1.Blocked);
        state.Ready.Should().BeFalse();
    }

    [Fact]
    public void Create_WhenWarningExists_ReturnsDegraded()
    {
        var state =
            LiveReadinessStateV1.Create(
                matchId: "match-1",
                checks:
                [
                    new(
                        LiveReadinessDimensionV1.Camera,
                        LiveReadinessStatusV1.Ready,
                        Required: true,
                        Summary: "Camera ready")
                ],
                findings:
                [
                    new(
                        LiveReadinessDimensionV1.Audio,
                        LiveReadinessSeverityV1.Warning,
                        Code: "audio.unverified",
                        Message: "Audio path has not been verified.")
                ]);

        state.Status.Should().Be(LiveReadinessStatusV1.Degraded);
        state.Ready.Should().BeFalse();
    }

    [Fact]
    public void Create_WhenBlockingFindingExists_ReturnsBlocked()
    {
        var state =
            LiveReadinessStateV1.Create(
                matchId: "match-1",
                checks:
                [
                    new(
                        LiveReadinessDimensionV1.Camera,
                        LiveReadinessStatusV1.Ready,
                        Required: true,
                        Summary: "Camera ready")
                ],
                findings:
                [
                    new(
                        LiveReadinessDimensionV1.Ffmpeg,
                        LiveReadinessSeverityV1.Blocking,
                        Code: "ffmpeg.missing",
                        Message: "FFmpeg runtime is unavailable.")
                ]);

        state.Status.Should().Be(LiveReadinessStatusV1.Blocked);
        state.Ready.Should().BeFalse();
    }

    [Fact]
    public void Serialization_UsesStringEnumsAndStableWireNames()
    {
        var state =
            LiveReadinessStateV1.Create(
                matchId: "match-1",
                checks:
                [
                    new(
                        LiveReadinessDimensionV1.StreamDeckOwnership,
                        LiveReadinessStatusV1.Degraded,
                        Required: true,
                        Summary: "Ownership conflicted",
                        Detail: "Another process owns the device.")
                ],
                findings:
                [
                    new(
                        LiveReadinessDimensionV1.StreamDeckOwnership,
                        LiveReadinessSeverityV1.Warning,
                        Code: "streamdeck.conflict",
                        Message: "Exclusive ownership is not established.")
                ],
                observedAtUtc:
                    new DateTimeOffset(
                        2026, 9, 9, 12, 30, 0, TimeSpan.Zero));

        var json =
            JsonSerializer.Serialize(state);

        json.Should().Contain("\"ContractVersion\":\"1.0\"");
        json.Should().Contain("\"Status\":\"Degraded\"");
        json.Should().Contain("\"Dimension\":\"StreamDeckOwnership\"");
        json.Should().Contain("\"Severity\":\"Warning\"");
        json.Should().Contain("\"Code\":\"streamdeck.conflict\"");
    }

    [Fact]
    public void Create_WhenChecksAreNull_Throws()
    {
        var action =
            () => LiveReadinessStateV1.Create(
                matchId: null,
                checks: null!);

        action.Should().Throw<ArgumentNullException>();
    }
}