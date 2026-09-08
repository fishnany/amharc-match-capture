using System.Text.Json;
using AmharcAgent.Core.Contracts;
using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using FluentAssertions;
using Xunit;

namespace AmharcAgent.Tests;

public class BroadcastPresentationStateV1Tests
{
    private static ScoreState GaelicFootballScore(
        string matchId = "match-1") =>
        new(
            MatchId: matchId,
            Sport: Sport.GaelicFootball,
            ScoringModel: ScoringModel.GoalsTwoPointOnePoint,
            HomeGoals: 1,
            HomeTwoPointScores: 2,
            HomePoints: 5,
            AwayGoals: 0,
            AwayTwoPointScores: 1,
            AwayPoints: 8,
            UpdatedAt:
                new DateTimeOffset(
                    2026,
                    9,
                    8,
                    10,
                    0,
                    0,
                    TimeSpan.Zero));

    private static ClockSnapshotV1 Clock(
        string matchId = "match-1") =>
        new(
            ContractVersion: "1.0",
            MatchId: matchId,
            Period: 2,
            PeriodClockSeconds: 245,
            TotalMatchElapsedSeconds: 2345,
            IsRunning: true,
            RecordingElapsedSeconds: 2500,
            ObservedAtUtc:
                new DateTimeOffset(
                    2026,
                    9,
                    8,
                    10,
                    0,
                    1,
                    TimeSpan.Zero),
            Authority:
                new ClockAuthorityV1(
                    "AMHARC Capture",
                    "capture-instance-1"),
            AuthorityEpoch: 123456,
            Sequence: 42);

    private static OverlayState LegacyOverlay() =>
        new(
            ActiveTemplateId: "standard-scoreboard",
            IsVisible: true,
            OutputMode: OverlayOutputMode.Programme,
            CurrentGraphic: "goal",
            GraphicVisible: true,

            // Deliberately incorrect legacy values.
            // They must never override canonical broadcast truth.
            HomeGoals: 99,
            HomePoints: 98,
            AwayGoals: 97,
            AwayPoints: 96,
            MatchClockSeconds: 9999,
            CurrentPeriod: 9);

    [Fact]
    public void FromCanonical_PreservesCanonicalScoreState()
    {
        var score =
            GaelicFootballScore();

        var result =
            BroadcastPresentationStateV1.FromCanonical(
                score,
                Clock(),
                LegacyOverlay());

        result.Score.Should().BeSameAs(score);

        result.Score.HomeGoals.Should().Be(1);
        result.Score.HomeTwoPointScores.Should().Be(2);
        result.Score.HomePoints.Should().Be(5);

        result.Score.AwayGoals.Should().Be(0);
        result.Score.AwayTwoPointScores.Should().Be(1);
        result.Score.AwayPoints.Should().Be(8);

        result.Score.HomeTotal.Should().Be(12);
        result.Score.AwayTotal.Should().Be(10);
    }

    [Fact]
    public void FromCanonical_PreservesCanonicalClockSnapshot()
    {
        var clock =
            Clock();

        var result =
            BroadcastPresentationStateV1.FromCanonical(
                GaelicFootballScore(),
                clock,
                LegacyOverlay());

        result.Clock.Should().BeSameAs(clock);

        result.Clock.Period.Should().Be(2);
        result.Clock.PeriodClockSeconds.Should().Be(245);
        result.Clock.TotalMatchElapsedSeconds.Should().Be(2345);
        result.Clock.IsRunning.Should().BeTrue();
        result.Clock.AuthorityEpoch.Should().Be(123456);
        result.Clock.Sequence.Should().Be(42);
    }

    [Fact]
    public void FromCanonical_MapsPresentationControlWithoutMatchTruth()
    {
        var result =
            BroadcastPresentationStateV1.FromCanonical(
                GaelicFootballScore(),
                Clock(),
                LegacyOverlay());

        result.Presentation.ActiveTemplateId
            .Should()
            .Be("standard-scoreboard");

        result.Presentation.ScoreboardVisible
            .Should()
            .BeTrue();

        result.Presentation.OutputMode
            .Should()
            .Be(OverlayOutputMode.Programme);

        result.Presentation.ActiveGraphic
            .Should()
            .Be("goal");

        result.Presentation.GraphicVisible
            .Should()
            .BeTrue();
    }

    [Fact]
    public void FromCanonical_IgnoresLegacyOverlayScoreAndClockFields()
    {
        var result =
            BroadcastPresentationStateV1.FromCanonical(
                GaelicFootballScore(),
                Clock(),
                LegacyOverlay());

        // Legacy OverlayState contains deliberately impossible values:
        // 99 goals, 98 points, period 9 and clock 9999.
        //
        // The broadcast contract must expose only canonical state.
        result.Score.HomeGoals.Should().Be(1);
        result.Score.HomePoints.Should().Be(5);
        result.Clock.Period.Should().Be(2);
        result.Clock.PeriodClockSeconds.Should().Be(245);
    }

    [Fact]
    public void FromCanonical_RejectsMismatchedMatchAuthority()
    {
        var act =
            () =>
                BroadcastPresentationStateV1.FromCanonical(
                    GaelicFootballScore("match-score"),
                    Clock("match-clock"),
                    LegacyOverlay());

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                "*ScoreState match 'match-score'*ClockSnapshotV1 match 'match-clock'*");
    }

    [Fact]
    public void ContractVersion_IsExplicitAndStable()
    {
        var result =
            BroadcastPresentationStateV1.FromCanonical(
                GaelicFootballScore(),
                Clock(),
                LegacyOverlay());

        BroadcastPresentationStateV1
            .CurrentContractVersion
            .Should()
            .Be("1.0");

        result.ContractVersion
            .Should()
            .Be("1.0");

        result.MatchId
            .Should()
            .Be("match-1");
    }


    [Fact]
    public void BroadcastPresentationState_SerializesCanonicalWireVocabulary()
    {
        var score = new ScoreState(
            MatchId: "m1",
            Sport: Sport.GaelicFootball,
            ScoringModel: ScoringModel.GoalsTwoPointOnePoint,
            HomeGoals: 1,
            HomeTwoPointScores: 2,
            HomePoints: 3,
            AwayGoals: 0,
            AwayTwoPointScores: 1,
            AwayPoints: 4,

            UpdatedAt: DateTimeOffset.UtcNow);

        var clock = new ClockSnapshotV1(
            ContractVersion: "1.0",
            MatchId: "m1",
            Period: 1,
            PeriodClockSeconds: 120,
            TotalMatchElapsedSeconds: 120,
            IsRunning: true,
            RecordingElapsedSeconds: 120,
            ObservedAtUtc:
                new DateTimeOffset(
                    2026,
                    9,
                    8,
                    10,
                    0,
                    0,
                    TimeSpan.Zero),
            Authority:
                new ClockAuthorityV1(
                    "amharc-match-capture",
                    "capture-instance-1"),
            AuthorityEpoch: 1,
            Sequence: 1);

        var state =
            new BroadcastPresentationStateV1(
                BroadcastPresentationStateV1.CurrentContractVersion,
                "m1",
                score,
                clock,
                new BroadcastPresentationControlV1(
                    ActiveTemplateId: "scoreboard",
                    ScoreboardVisible: true,
                    OutputMode: OverlayOutputMode.OperatorPreview,
                    ActiveGraphic: null,
                    GraphicVisible: false));

        var json =
            JsonSerializer.Serialize(state);

        json.Should().Contain(
            "\"Sport\":\"gaelic-football\"");

        json.Should().Contain(
            "\"ScoringModel\":\"goals-two-point-one-point\"");

        json.Should().Contain(
            "\"OutputMode\":\"operator-preview\"");
    }

    [Theory]
    [InlineData(OverlayOutputMode.Clean, "clean")]
    [InlineData(OverlayOutputMode.Programme, "programme")]
    [InlineData(OverlayOutputMode.OverlayOnly, "overlay-only")]
    [InlineData(OverlayOutputMode.OperatorPreview, "operator-preview")]
    public void OverlayOutputMode_RoundTripsCanonicalWireValue(
        OverlayOutputMode mode,
        string wireValue)
    {
        var options = new JsonSerializerOptions();

        options.Converters.Add(
            new OverlayOutputModeWireJsonConverter());

        var json =
            JsonSerializer.Serialize(
                mode,
                options);

        json.Should().Be(
            $"\"{wireValue}\"");

        JsonSerializer.Deserialize<OverlayOutputMode>(
                json,
                options)
            .Should()
            .Be(mode);
    }}
